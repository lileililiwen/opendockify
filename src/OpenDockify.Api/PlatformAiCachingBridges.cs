using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenDockify.AiAssist;
using OpenDockify.AiAssist.Services;
using OpenDockify.SystemConfig.Services;
using Platform.Ai;
using Platform.Ai.Contracts;
using Platform.Ai.Ollama;
using Platform.Ai.OpenAiCompatible;
using Platform.Caching.Contracts;
using Platform.Caching.DependencyInjection;
using Platform.Caching.Hybrid;
using Platform.Caching.Hybrid.DependencyInjection;
using Platform.Caching.Keys;
using Platform.Quota.Contracts;

namespace OpenDockify.Api;

/// <summary>
/// Per-user per-day AI token budget over the platform quota store
/// (<c>Ai:MaxTokensPerDay</c>, default 20000, 0 disables). Token counts are
/// estimated as <c>chars / 4</c>; the gate is advisory (check before the
/// provider call, settle actuals after success) and fails closed with
/// <c>429 + reset time</c> without calling any provider.
/// </summary>
public sealed class AiTokenBudget(IQuotaStore quotas, ISystemConfigReader config)
{
    private static readonly QuotaResource _resource = new("ai-tokens");

    public const int DefaultMaxTokensPerDay = 20000;

    public const int SystemPromptReserveTokens = 128;

    public static int EstimateTokens(string? text)
    {
        return string.IsNullOrEmpty(text) ? 0 : EstimateCharCount(text.Length);
    }

    public static int EstimateCharCount(int chars)
    {
        return chars <= 0 ? 0 : Math.Max(1, chars / 4);
    }

    public static QuotaSubject SubjectFor(Guid userId)
    {
        return new($"user:{userId:N}");
    }

    public static QuotaWindow DailyWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        return new QuotaWindow(start, start.AddDays(1));
    }

    public async Task<int> LimitAsync(CancellationToken ct)
    {
        var limit = await config.GetAsync<int>(SettingKeys.AiMaxTokensPerDay, ct);
        return limit <= 0 ? 0 : limit;
    }

    public async Task<(bool Allowed, DateTimeOffset ResetAt, long Remaining)> CheckAsync(
        Guid userId,
        int estimatedTokens,
        CancellationToken ct)
    {
        var limit = await LimitAsync(ct);
        if (limit <= 0)
        {
            return (true, DailyWindow().EndsAt, long.MaxValue);
        }

        var window = DailyWindow();
        var decision = await quotas.CheckAsync(SubjectFor(userId), _resource, window, limit, Math.Max(0, estimatedTokens), ct);
        return (decision.Allowed, window.EndsAt, decision.Remaining);
    }

    public async Task<long> RemainingAsync(Guid userId, CancellationToken ct)
    {
        var limit = await LimitAsync(ct);
        if (limit <= 0)
        {
            return long.MaxValue;
        }

        var window = DailyWindow();
        var snapshot = await quotas.GetSnapshotAsync(SubjectFor(userId), _resource, window, limit, ct);
        return snapshot.Remaining;
    }

    public async Task ConsumeAsync(Guid userId, int actualTokens, CancellationToken ct)
    {
        var limit = await LimitAsync(ct);
        if (limit <= 0 || actualTokens <= 0)
        {
            return;
        }

        var key = $"ai-tokens:{userId:N}:{Guid.NewGuid():N}";
        var reserved = await quotas.ReserveAsync(
            SubjectFor(userId), _resource, DailyWindow(), limit, key, actualTokens, TimeSpan.FromMinutes(5), ct);
        if (reserved.Status == QuotaLifecycleStatus.Reserved)
        {
            await quotas.SettleAsync(key, ct);
        }
    }
}

/// <summary>
/// Platform policy adapter so <see cref="AiClient"/> itself refuses
/// over-budget requests before any provider is touched. The gateway passes
/// the caller and the estimated input cost via request metadata.
/// </summary>
public sealed class AiTokenBudgetPolicy(AiTokenBudget budgets) : IAiFeaturePolicy
{
    public const string UserMetadataKey = "ai.user";

    public const string EstimatedTokensMetadataKey = "ai.estimated-tokens";

    public async ValueTask<AiPolicyDecision> EvaluateAsync(AiTextRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Metadata is null
            || !request.Metadata.TryGetValue(UserMetadataKey, out var userValue)
            || !Guid.TryParseExact(userValue, "N", out var userId)
            || !request.Metadata.TryGetValue(EstimatedTokensMetadataKey, out var estimateValue)
            || !int.TryParse(estimateValue, CultureInfo.InvariantCulture, out var estimated))
        {
            return AiPolicyDecision.Allow();
        }

        var (allowed, _, _) = await budgets.CheckAsync(userId, estimated, cancellationToken);
        return allowed
            ? AiPolicyDecision.Allow()
            : AiPolicyDecision.Reject(AiFailureCategory.QuotaExceeded, "AI token budget exceeded for today.");
    }
}

/// <summary>
/// Policy-gated generation gateway (the <see cref="ILlmClient"/> used by
/// <see cref="AiAssistService"/>). Routes to local Ollama by default and to
/// an OpenAI-compatible endpoint only when <c>Ai:Provider</c> selects it;
/// remote <c>http:</c> endpoints fail closed unless they target loopback with
/// <c>Ai:AllowInsecureHttp</c>. Identical successful generations are served
/// from the hybrid cache (SHA-256 key, 10-minute TTL).
/// </summary>
public sealed partial class PlatformGatewayLlmClient(
    ISystemConfigReader config,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ICacheStore cache,
    CacheKeyBuilder keys,
    AiTokenBudget budgets,
    IHttpContextAccessor httpContext,
    ILogger<PlatformGatewayLlmClient> logger) : ILlmClient
{
    private static readonly AiFeatureKey _feature = AiFeatureKey.Create("polish");

#pragma warning disable S1075 // Local-first default; deployer-overridable via Ai:Endpoint, never a secret or remote host.
    private static readonly Uri _defaultOllamaBaseAddress = new("http://localhost:11434/api/");
#pragma warning restore S1075

    public async Task<LlmResult> CompleteAsync(
        string systemPrompt,
        string userContent,
        CancellationToken cancellationToken)
    {
        var provider = (await config.GetAsync<string>(SettingKeys.AiProvider, cancellationToken) ?? "ollama")
            .Trim().ToLowerInvariant();
        var endpoint = await config.GetAsync<string>(SettingKeys.AiEndpoint, cancellationToken);
        var apiKey = await config.GetAsync<string>(SettingKeys.AiApiKey, cancellationToken);
        var model = await config.GetAsync<string>(SettingKeys.AiModel, cancellationToken);
        var timeoutSeconds = await config.GetAsync<int?>(SettingKeys.AiTimeoutSeconds, cancellationToken) ?? 30;

        if (string.IsNullOrWhiteSpace(model))
        {
            return LlmResult.Failure("AI model is not configured.");
        }

        var timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));
        var userId = ResolveCurrentUserId();
        var estimated = AiTokenBudget.EstimateTokens(systemPrompt)
            + AiTokenBudget.EstimateTokens(userContent)
            + AiTokenBudget.SystemPromptReserveTokens;

        IAiTextProvider textProvider;
        try
        {
            textProvider = provider switch
            {
                "ollama" => CreateOllamaProvider(endpoint),
                "openai-compatible" => CreateOpenAiCompatibleProvider(endpoint, apiKey),
                _ => throw new InvalidOperationException($"Unknown AI provider '{provider}'."),
            };
        }
        catch (InvalidOperationException ex)
        {
            return LlmResult.Failure(ex.Message);
        }

        var cacheKey = keys.ForApplication($"polish:{RenderCacheService.Sha256Hex($"{systemPrompt}\n{userContent}")}");
        var cached = await cache.GetAsync<string>(cacheKey, cancellationToken);
        if (cached.Status == CacheReadStatus.Hit && !string.IsNullOrEmpty(cached.Value))
        {
            Log.PolishCacheHit(logger, RenderCacheService.SafePrefix(cacheKey.Value), cached.Value.Length);
            return LlmResult.Success(cached.Value);
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (userId.HasValue)
        {
            metadata[AiTokenBudgetPolicy.UserMetadataKey] = userId.Value.ToString("N");
            metadata[AiTokenBudgetPolicy.EstimatedTokensMetadataKey] = estimated.ToString(CultureInfo.InvariantCulture);
        }

        var request = new AiTextRequest(
            _feature,
            [new AiMessage("system", systemPrompt), new AiMessage("user", userContent)],
            model,
            timeout: timeout,
            metadata: metadata);
        var client = new AiClient(new SingleAiProviderRouter(textProvider), new AiTokenBudgetPolicy(budgets), logger: null);
        var result = await client.GenerateAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            return LlmResult.Failure(result.Failure is { Category: AiFailureCategory.QuotaExceeded }
                ? $"AI token budget exceeded for today ({SettingKeys.AiMaxTokensPerDay})."
                : result.Failure?.Message ?? "The AI provider request failed.");
        }

        if (string.IsNullOrWhiteSpace(result.Text))
        {
            return LlmResult.Failure("LLM returned an empty response.");
        }

        if (userId.HasValue)
        {
            await budgets.ConsumeAsync(userId.Value, estimated + AiTokenBudget.EstimateTokens(result.Text), cancellationToken);
        }

        await cache.SetAsync(
            cacheKey,
            result.Text,
            new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10), Tags = [RenderCacheService.PolishTag] },
            cancellationToken);
        Log.PolishCacheStore(logger, RenderCacheService.SafePrefix(cacheKey.Value), result.Text.Length);
        return LlmResult.Success(result.Text);
    }

    private OllamaProvider CreateOllamaProvider(string? endpoint)
    {
        var baseAddress = string.IsNullOrWhiteSpace(endpoint)
            ? _defaultOllamaBaseAddress
            : EnsureAllowedEndpoint(endpoint, allowLoopbackDefault: true);
        var http = httpClientFactory.CreateClient(AiAssistClientNames.HttpClient);
        return new OllamaProvider(http, new OllamaOptions { BaseAddress = baseAddress });
    }

    private OpenAiCompatibleProvider CreateOpenAiCompatibleProvider(string? endpoint, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("AI endpoint is not configured.");
        }

        var baseAddress = EnsureAllowedEndpoint(endpoint, allowLoopbackDefault: false);
        var http = httpClientFactory.CreateClient(AiAssistClientNames.HttpClient);
        return new OpenAiCompatibleProvider(http, new OpenAiCompatibleOptions { BaseAddress = baseAddress, ApiKey = apiKey });
    }

    /// <summary>
    /// Fails closed on insecure remote endpoints. An explicitly configured
    /// <c>http:</c> URL is accepted only for loopback with
    /// <c>Ai:AllowInsecureHttp</c>; the Ollama loopback default needs no flag
    /// (local-first, no data egress).
    /// </summary>
    private Uri EnsureAllowedEndpoint(string endpoint, bool allowLoopbackDefault)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("AI endpoint must use https:// unless it targets loopback and Ai:AllowInsecureHttp is true.");
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return uri;
        }

        if (uri.Scheme != Uri.UriSchemeHttp || !uri.IsLoopback)
        {
            throw new InvalidOperationException("AI endpoint must use https:// unless it targets loopback and Ai:AllowInsecureHttp is true.");
        }

        if (allowLoopbackDefault)
        {
            return uri;
        }

        var allowed = bool.TryParse(configuration["Ai:AllowInsecureHttp"], out var parsed) && parsed;
        if (!allowed)
        {
            throw new InvalidOperationException("AI endpoint must use https:// unless it targets loopback and Ai:AllowInsecureHttp is true.");
        }

        return uri;
    }

    private Guid? ResolveCurrentUserId()
    {
        // The gateway is reached via AiAssistService inside a request; the
        // endpoint-level budget gate stays authoritative when no ambient
        // user exists (tests, background flows) and the policy then allows.
        var principal = httpContext.HttpContext?.User;
        var raw = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal?.FindFirst("sub")?.Value;
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Debug, "AI polish cache hit key={KeyPrefix} chars={Chars}.")]
        public static partial void PolishCacheHit(ILogger logger, string keyPrefix, int chars);

        [LoggerMessage(2, LogLevel.Debug, "AI polish cache store key={KeyPrefix} chars={Chars}.")]
        public static partial void PolishCacheStore(ILogger logger, string keyPrefix, int chars);
    }
}

/// <summary>
/// Redaction-safe render/LPR cache over the platform hybrid store
/// (single-host; Redis is explicitly not adopted). Keys are SHA-256 hex of
/// normalized inputs under tenant/app prefixes — raw PII never appears in a
/// key, tag, or log. Renders/polish use a 10-minute TTL, LPR a 24-hour TTL.
/// </summary>
public sealed partial class RenderCacheService(
    ICacheStore cache,
    CacheKeyBuilder keys,
    ISystemConfigReader config,
    ILogger<RenderCacheService> logger)
{
    public const string RenderTag = "renders";

    public const string PolishTag = "ai-polish";

    public const string LprTag = "lpr";

    public static string Sha256Hex(string normalized)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    /// <summary>Short key prefix for logs; never the full key or inputs.</summary>
    public static string SafePrefix(string keyValue)
    {
        return keyValue.Length <= 16 ? keyValue : keyValue[..16];
    }

    public static string NormalizeValues(
        Guid userId,
        Guid templateId,
        string definitionJson,
        string body,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyCollection<string> selectedClauseIds)
    {
        var sb = new StringBuilder();
        sb.Append(userId.ToString("N")).Append('|');
        sb.Append(templateId.ToString("N")).Append('|');
        sb.Append(definitionJson).Append('|').Append(body).Append('|');
        foreach (var pair in values.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            sb.Append(pair.Key).Append('=').Append(pair.Value).Append(';');
        }

        sb.Append('|');
        foreach (var id in selectedClauseIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            sb.Append(id).Append(',');
        }

        return sb.ToString();
    }

    public async Task<(bool Hit, string? Text)> GetOrCreateRenderAsync(
        string normalizedInputs,
        Func<CancellationToken, Task<string?>> factory,
        CancellationToken ct)
    {
        var ttlMinutes = await config.GetAsync<int>(SettingKeys.CacheRenderTtlMinutes, ct);
        var ttl = TimeSpan.FromMinutes(ttlMinutes > 0 ? ttlMinutes : 10);
        var key = keys.ForApplication($"render:{Sha256Hex(normalizedInputs)}");
        var read = await cache.GetAsync<string>(key, ct);
        if (read.Status == CacheReadStatus.Hit && read.Value is not null)
        {
            Log.RenderHit(logger, SafePrefix(key.Value), read.Value.Length);
            return (true, read.Value);
        }

        var value = await factory(ct);
        if (value is null)
        {
            return (false, null);
        }

        await cache.SetAsync(
            key,
            value,
            new CacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl, Tags = [RenderTag] },
            ct);
        Log.RenderStore(logger, SafePrefix(key.Value), value.Length);
        return (false, value);
    }

    public async Task<decimal> GetOrCreateLprRateAsync(
        Func<CancellationToken, Task<decimal>> factory,
        CancellationToken ct)
    {
        var ttlHours = await config.GetAsync<int>(SettingKeys.CacheLprTtlHours, ct);
        var ttl = TimeSpan.FromHours(ttlHours > 0 ? ttlHours : 24);
        var key = keys.ForApplication("lpr:one-year-rate");
        var read = await cache.GetAsync<decimal>(key, ct);
        if (read.Status == CacheReadStatus.Hit)
        {
            Log.LprHit(logger);
            return read.Value;
        }

        var value = await factory(ct);
        await cache.SetAsync(
            key,
            value,
            new CacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl, Tags = [LprTag] },
            ct);
        return value;
    }

    public Task<CacheOperationResult> InvalidateAsync(string tag, CancellationToken ct)
    {
        return cache.RemoveByTagAsync(tag, ct);
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Debug, "Render cache hit key={KeyPrefix} chars={Chars}.")]
        public static partial void RenderHit(ILogger logger, string keyPrefix, int chars);

        [LoggerMessage(2, LogLevel.Debug, "Render cache store key={KeyPrefix} chars={Chars}.")]
        public static partial void RenderStore(ILogger logger, string keyPrefix, int chars);

        [LoggerMessage(3, LogLevel.Debug, "LPR rate cache hit.")]
        public static partial void LprHit(ILogger logger);
    }
}

public static class PlatformAiCachingWiring
{
    public static IServiceCollection AddPlatformAiCaching(this IServiceCollection services)
    {
        services.AddPlatformCaching("opendockify");
        services.AddPlatformCachingHybrid();
        services.AddSingleton<ICacheStore, HybridCacheStore>();
        services.AddHttpContextAccessor();
        services.AddScoped<AiTokenBudget>();
        services.AddScoped<AiTokenBudgetPolicy>();
        services.AddScoped<RenderCacheService>();
        services.AddScoped<ILlmClient, PlatformGatewayLlmClient>();
        return services;
    }

    public static IEndpointRouteBuilder MapAdminAiCacheEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/cache").RequireAuthorization("RequireAdmin");

        group.MapPost("/invalidate", async (
            InvalidateCacheRequest request,
            RenderCacheService renders,
            CancellationToken ct) =>
        {
            var scope = (request.Scope ?? "all").Trim().ToLowerInvariant();
            var tags = scope switch
            {
                "lpr" => new[] { RenderCacheService.LprTag },
                "renders" => new[] { RenderCacheService.RenderTag },
                "ai-polish" => new[] { RenderCacheService.PolishTag },
                "all" => new[] { RenderCacheService.LprTag, RenderCacheService.RenderTag, RenderCacheService.PolishTag },
                _ => null,
            };
            if (tags is null)
            {
                return Results.Json(
                    new { error = "Unknown scope. Use lpr, renders, ai-polish, or all." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            foreach (var tag in tags)
            {
                await renders.InvalidateAsync(tag, ct);
            }

            return Results.Ok(new { invalidated = tags });
        });

        return endpoints;
    }
}

public sealed record InvalidateCacheRequest(string? Scope);
