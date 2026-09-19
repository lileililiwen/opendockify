using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Quota.AspNetCore.Contracts;
using Platform.Quota.Contracts;
using Platform.RateLimiting;

namespace OpenDockify.Api;

/// <summary>
/// Per-user + IP rate and quota gates over platform contracts.
/// Policies: generate / preview / finalize / ai-polish. Quotas: per-user
/// daily AI and document counts. 429s are RFC9457 with Retry-After.
/// </summary>
public sealed class NotifyRateGate(
    IRateLimiter limiter,
    IQuotaStore quotas,
    IConfiguration configuration)
{
    public async Task<RateLimitDecision> CheckRateAsync(string policy, HttpContext http, CancellationToken ct)
    {
        var subject = RateSubject(http);
        return await limiter.CheckAsync(new RateLimitKey(policy, subject), ct);
    }

    public static string RateSubject(HttpContext http)
    {
        var user = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.User.FindFirst("sub")?.Value
            ?? http.User.FindFirst("token_id")?.Value;
        if (!string.IsNullOrWhiteSpace(user))
        {
            return $"user:{user}";
        }

        return $"ip:{http.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    public static IResult RateLimitedResult(HttpContext http, RateLimitDecision decision)
    {
        http.Response.Headers.RetryAfter = decision.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Results.Json(
            new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc6585#section-4",
                title = "Too Many Requests",
                status = 429,
                code = "rate_limited",
                detail = "Rate limit exceeded. Slow down and retry later.",
            },
            statusCode: 429,
            contentType: "application/problem+json");
    }

    public static IResult RateLimitedResult(RateLimitDecision decision)
    {
        return Results.Json(
            new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc6585#section-4",
                title = "Too Many Requests",
                status = 429,
                code = "rate_limited",
                detail = "Rate limit exceeded. Slow down and retry later.",
                retryAfter = decision.RetryAfterSeconds,
            },
            statusCode: 429,
            contentType: "application/problem+json");
    }

    public async Task<(bool Allowed, IResult? Rejection)> CheckAiQuotaAsync(HttpContext http, CancellationToken ct)
    {
        var subject = QuotaSubjectFor(http);
        var limit = AiDailyLimit();
        if (limit <= 0)
        {
            return (true, null);
        }

        var window = DailyWindow();
        var decision = await quotas.CheckAsync(subject, new QuotaResource("ai-polish"), window, limit, 1, ct);
        if (decision.Allowed)
        {
            return (true, null);
        }

        return (false, QuotaExceeded(http, "Ai:MaxItemsPerDay", window.EndsAt));
    }

    public async Task<(bool Allowed, IResult? Rejection)> CheckDocumentQuotaAsync(HttpContext http, CancellationToken ct)
    {
        var subject = QuotaSubjectFor(http);
        var limit = DocumentDailyLimit();
        if (limit <= 0)
        {
            return (true, null);
        }

        var window = DailyWindow();
        var decision = await quotas.CheckAsync(subject, new QuotaResource("documents"), window, limit, 1, ct);
        if (decision.Allowed)
        {
            return (true, null);
        }

        return (false, QuotaExceeded(http, "Documents:MaxItemsPerDay", window.EndsAt));
    }

    public async Task ConsumeAiAsync(HttpContext http, CancellationToken ct)
    {
        var subject = QuotaSubjectFor(http);
        var limit = AiDailyLimit();
        if (limit <= 0)
        {
            return;
        }

        var window = DailyWindow();
        var key = $"{http.TraceIdentifier}:ai";
        var reserved = await quotas.ReserveAsync(subject, new QuotaResource("ai-polish"), window, limit, key, 1, TimeSpan.FromMinutes(5), ct);
        if (reserved.Status == QuotaLifecycleStatus.Reserved)
        {
            await quotas.SettleAsync(key, ct);
        }
    }

    public async Task ConsumeDocumentAsync(HttpContext http, CancellationToken ct)
    {
        var subject = QuotaSubjectFor(http);
        var limit = DocumentDailyLimit();
        if (limit <= 0)
        {
            return;
        }

        var window = DailyWindow();
        var key = $"{http.TraceIdentifier}:doc";
        var reserved = await quotas.ReserveAsync(subject, new QuotaResource("documents"), window, limit, key, 1, TimeSpan.FromMinutes(5), ct);
        if (reserved.Status == QuotaLifecycleStatus.Reserved)
        {
            await quotas.SettleAsync(key, ct);
        }
    }

    private static QuotaSubject QuotaSubjectFor(HttpContext http)
    {
        var user = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.User.FindFirst("sub")?.Value
            ?? http.User.FindFirst("token_id")?.Value;
        return new QuotaSubject(string.IsNullOrWhiteSpace(user) ? $"ip:{http.Connection.RemoteIpAddress?.ToString() ?? "unknown"}" : $"user:{user}");
    }

    private int AiDailyLimit()
    {
        return int.TryParse(configuration["Ai:MaxItemsPerDay"], out var v) && v > 0 ? v : 0;
    }

    private int DocumentDailyLimit()
    {
        return int.TryParse(configuration["Documents:MaxItemsPerDay"], out var v) && v > 0 ? v : 0;
    }

    private static QuotaWindow DailyWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        return new QuotaWindow(start, start.AddDays(1));
    }

    private static IResult QuotaExceeded(HttpContext http, string limitKey, DateTimeOffset resetAt)
    {
        var retryAfter = Math.Max(1, (int)(resetAt - DateTimeOffset.UtcNow).TotalSeconds);
        http.Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Results.Json(
            new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc6585#section-4",
                title = "Quota Exceeded",
                status = 429,
                code = "quota_exceeded",
                detail = $"Quota {limitKey} exceeded.",
                resetAt,
            },
            statusCode: 429,
            contentType: "application/problem+json");
    }
}

/// <summary>Quota subject: authenticated user else client IP.</summary>
public sealed class NotifyQuotaSubjectResolver : IQuotaSubjectResolver
{
    public Task<QuotaSubjectResolution> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var user = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst("token_id")?.Value;
        if (!string.IsNullOrWhiteSpace(user))
        {
            return Task.FromResult(new QuotaSubjectResolution(new QuotaSubject($"user:{user}")));
        }

        var ip = context.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(ip))
        {
            return Task.FromResult(QuotaSubjectResolution.Missing);
        }

        return Task.FromResult(new QuotaSubjectResolution(new QuotaSubject($"ip:{ip}")));
    }
}

/// <summary>
/// Quota resources: AI polish and document generate/finalize consume per-day
/// quotas when configured; everything else is exempt.
/// </summary>
public sealed class NotifyQuotaResourceResolver(IConfiguration configuration) : IQuotaResourceResolver
{
    public Task<IReadOnlyList<QuotaRequest>> ResolveAsync(HttpContext context, QuotaSubject subject, CancellationToken cancellationToken = default)
    {
        var path = context.Request.Path.ToString();
        var method = context.Request.Method;
        if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<QuotaRequest>>(Array.Empty<QuotaRequest>());
        }

        var window = DailyWindow();
        if (path.StartsWith("/api/ai/", StringComparison.OrdinalIgnoreCase))
        {
            var limit = Limit("Ai:MaxItemsPerDay");
            if (limit <= 0)
            {
                return Task.FromResult<IReadOnlyList<QuotaRequest>>(Array.Empty<QuotaRequest>());
            }

            return Task.FromResult<IReadOnlyList<QuotaRequest>>([new QuotaRequest(new QuotaResource("ai-polish"), limit, 1, window)]);
        }

        if (path.StartsWith("/api/documents/generate", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/documents/finalize", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/automation/finalize", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/automation/preview", StringComparison.OrdinalIgnoreCase))
        {
            var limit = Limit("Documents:MaxItemsPerDay");
            if (limit <= 0)
            {
                return Task.FromResult<IReadOnlyList<QuotaRequest>>(Array.Empty<QuotaRequest>());
            }

            return Task.FromResult<IReadOnlyList<QuotaRequest>>([new QuotaRequest(new QuotaResource("documents"), limit, 1, window)]);
        }

        return Task.FromResult<IReadOnlyList<QuotaRequest>>(Array.Empty<QuotaRequest>());
    }

    private int Limit(string key)
    {
        return int.TryParse(configuration[key], out var v) && v > 0 ? v : 0;
    }

    private static QuotaWindow DailyWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        return new QuotaWindow(start, start.AddDays(1));
    }
}
