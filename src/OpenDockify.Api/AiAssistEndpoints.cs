using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.AiAssist.Services;
using OpenDockify.Auth;

namespace OpenDockify.Api;

/// <summary>
/// AI polish endpoints. Every <c>/api/ai/*</c> call is gated on
/// <c>Ai.Enabled</c> (disabled → 403, no LLM call) and an optional per-user
/// daily rate limit. Responses always carry a sensitivity warning.
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
            CancellationToken ct) =>
        {
            var result = await ai.PolishClauseAsync(CurrentUserId(http), request.TemplateId, request.Draft ?? string.Empty, ct);
            return ToResult(result);
        });

        group.MapPost("/polish-document", async (
            HttpContext http,
            PolishDocumentRequest request,
            AiAssistService ai,
            CancellationToken ct) =>
        {
            var result = await ai.PolishDocumentAsync(
                CurrentUserId(http),
                request.TemplateId,
                request.RenderedText ?? string.Empty,
                request.Values ?? new Dictionary<string, string>(),
                request.SelectedClauseIds ?? [],
                ct);
            return ToResult(result);
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
