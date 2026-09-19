using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.AiAssist.Services;
using OpenDockify.Auth;

namespace OpenDockify.Api;

/// <summary>
/// AI polish endpoints. Every <c>/api/ai/*</c> call is gated on
/// <c>Ai.Enabled</c> (disabled → 403, no LLM call), an optional per-user
/// daily rate limit, and the per-user per-day token budget
/// (<c>Ai:MaxTokensPerDay</c> → 429 with reset time, no provider call).
/// Responses always carry a sensitivity warning plus the remaining token
/// quota for the Flutter budget surface.
/// </summary>
public static class AiAssistEndpoints
{
    public static IEndpointRouteBuilder MapAiAssistEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ai").RequireAuthorization();

        group.MapPost("/polish-clause", async (
            HttpContext http,
            PolishClauseRequest request,
            AiAssistService ai,
            AiTokenBudget budgets,
            NotifyRateGate gate,
            CancellationToken ct) =>
        {
            var rate = await gate.CheckRateAsync("ai-polish", http, ct);
            if (!rate.Allowed)
                return NotifyRateGate.RateLimitedResult(rate);
            var (quotaOk, quotaReject) = await gate.CheckAiQuotaAsync(http, ct);
            if (!quotaOk)
                return quotaReject!;
            var userId = CurrentUserId(http);
            var estimated = AiTokenBudget.EstimateTokens(request.Draft) + AiTokenBudget.SystemPromptReserveTokens;
            var (allowed, resetAt, _) = await budgets.CheckAsync(userId, estimated, ct);
            if (!allowed)
                return TokenBudgetExceeded(http, resetAt);
            var result = await ai.PolishClauseAsync(userId, request.TemplateId, request.Draft ?? string.Empty, ct);
            if (result.Status == AiAssistStatus.Ok)
                await gate.ConsumeAiAsync(http, ct);
            if (result is { Status: AiAssistStatus.Ok, Text: not null })
                await budgets.ConsumeAsync(userId, estimated + AiTokenBudget.EstimateTokens(result.Text), ct);
            return await ToResultAsync(result, budgets, userId, ct);
        });

        group.MapPost("/polish-document", async (
            HttpContext http,
            PolishDocumentRequest request,
            AiAssistService ai,
            AiTokenBudget budgets,
            NotifyRateGate gate,
            CancellationToken ct) =>
        {
            var rate = await gate.CheckRateAsync("ai-polish", http, ct);
            if (!rate.Allowed)
                return NotifyRateGate.RateLimitedResult(rate);
            var (quotaOk, quotaReject) = await gate.CheckAiQuotaAsync(http, ct);
            if (!quotaOk)
                return quotaReject!;
            var userId = CurrentUserId(http);
            var valuesChars = request.Values is null
                ? 0
                : request.Values.Sum(p => (p.Key?.Length ?? 0) + (p.Value?.Length ?? 0));
            var estimated = AiTokenBudget.EstimateTokens(request.RenderedText)
                + AiTokenBudget.EstimateCharCount(valuesChars)
                + AiTokenBudget.SystemPromptReserveTokens;
            var (allowed, resetAt, _) = await budgets.CheckAsync(userId, estimated, ct);
            if (!allowed)
                return TokenBudgetExceeded(http, resetAt);
            var result = await ai.PolishDocumentAsync(
                userId,
                request.TemplateId,
                request.RenderedText ?? string.Empty,
                request.Values ?? new Dictionary<string, string>(),
                request.SelectedClauseIds ?? [],
                ct);
            if (result.Status == AiAssistStatus.Ok)
                await gate.ConsumeAiAsync(http, ct);
            if (result is { Status: AiAssistStatus.Ok, Text: not null })
                await budgets.ConsumeAsync(userId, estimated + AiTokenBudget.EstimateTokens(result.Text), ct);
            return await ToResultAsync(result, budgets, userId, ct);
        });

        return endpoints;
    }

    public static IEndpointRouteBuilder MapAdminAiUsageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/ai-usage")
            .RequireAuthorization("RequireAdmin");

        group.MapGet("", async (AiUsageLogService logs, int limit = 50, CancellationToken ct = default) =>
        {
            var items = await logs.ListRecentAsync(limit, ct);
            return Results.Ok(items.Select(log => new
            {
                log.Id,
                log.UserId,
                log.Action,
                log.RequestSnippet,
                log.ResponseSnippet,
                log.Success,
                log.Timestamp,
            }));
        });

        return endpoints;
    }

    private static Guid CurrentUserId(HttpContext http)
    {
        return CurrentUserContextFactory.FromClaims(http.User).UserId;
    }

    private static IResult TokenBudgetExceeded(HttpContext http, DateTimeOffset resetAt)
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
                detail = "AI token budget (Ai:MaxTokensPerDay) exceeded. Try again after the daily reset.",
                resetAt,
            },
            statusCode: 429,
            contentType: "application/problem+json");
    }

    private static async Task<IResult> ToResultAsync(
        AiAssistResult result,
        AiTokenBudget budgets,
        Guid userId,
        CancellationToken ct)
    {
        if (result.Status != AiAssistStatus.Ok || result.Text is null)
        {
            return ToResult(result);
        }

        var remaining = await budgets.RemainingAsync(userId, ct);
        return Results.Ok(new { text = result.Text, warning = result.Warning, remainingTokens = remaining });
    }

    private static IResult ToResult(AiAssistResult result)
    {
        return result.Status switch
        {
            AiAssistStatus.Disabled => Results.Json(
                new { error = "AI disabled. Enable it via system settings (Ai.Enabled)." },
                statusCode: StatusCodes.Status403Forbidden),
            AiAssistStatus.RateLimited => Results.Json(
                new { error = "Daily AI usage limit reached." },
                statusCode: StatusCodes.Status429TooManyRequests),
            _ => result.Text is null
                ? Results.Json(new { error = result.Warning }, statusCode: StatusCodes.Status400BadRequest)
                : Results.Ok(new { text = result.Text, warning = result.Warning }),
        };
    }
}

public sealed record PolishClauseRequest(Guid TemplateId, string? Draft);

public sealed record PolishDocumentRequest(
    Guid TemplateId,
    string? RenderedText,
    Dictionary<string, string>? Values,
    List<string>? SelectedClauseIds);
