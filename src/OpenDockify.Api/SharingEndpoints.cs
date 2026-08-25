using Microsoft.AspNetCore.RateLimiting;
using OpenDockify.Auth;
using OpenDockify.Sharing.Services;

namespace OpenDockify.Api;

public static class SharingEndpoints
{
    public static IEndpointRouteBuilder MapSharingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var owner = endpoints.MapGroup("/api/documents/{documentId:guid}/shares").RequireAuthorization();
        owner.MapGet("", async (HttpContext http, Guid documentId, DocumentSharingService service, CancellationToken ct) =>
        {
            var items = await service.ListSharesAsync(UserId(http), documentId, ct);
            return items is null ? Results.NotFound(new { error = "Document not found." }) : Results.Ok(items);
        });
        owner.MapPost("/grants", async (HttpContext http, Guid documentId, CreateGrantRequest request, DocumentSharingService service, CancellationToken ct) =>
        {
            var result = await service.CreateGrantAsync(UserId(http), documentId, request.Username, request.AccessLevel, ct);
            if (result.NotFound)
                return Results.NotFound(new { error = "Document not found." });
            return result.Grant is null ? Results.BadRequest(new { error = result.Error }) : Results.Created($"/api/documents/{documentId}/shares", result.Grant);
        });
        owner.MapDelete("/grants/{grantId:guid}", async (HttpContext http, Guid documentId, Guid grantId, DocumentSharingService service, CancellationToken ct) =>
            ToOwnerResult(await service.RevokeGrantAsync(UserId(http), documentId, grantId, ct)));
        owner.MapPost("/links", async (HttpContext http, Guid documentId, CreateLinkRequest request, DocumentSharingService service, CancellationToken ct) =>
        {
            var result = await service.CreateLinkAsync(UserId(http), documentId, request.LifetimeHours, request.AllowDownload, ct);
            if (result.NotFound)
                return Results.NotFound(new { error = "Document not found." });
            return result.Link is null ? Results.BadRequest(new { error = result.Error }) : Results.Created($"/s/{result.Link.Token}", result.Link);
        });
        owner.MapDelete("/links/{linkId:guid}", async (HttpContext http, Guid documentId, Guid linkId, DocumentSharingService service, CancellationToken ct) =>
            ToOwnerResult(await service.RevokeLinkAsync(UserId(http), documentId, linkId, ct)));
        owner.MapGet("/audit", async (HttpContext http, Guid documentId, int page, int pageSize, DocumentSharingService service, CancellationToken ct) =>
        {
            var result = await service.GetAuditAsync(UserId(http), documentId, page, pageSize, ct);
            return result is null ? Results.NotFound(new { error = "Document not found." }) : Results.Ok(result);
        });

        var external = endpoints.MapGroup("/s").RequireRateLimiting("public-shares");
        external.MapGet("/{token}", async (HttpContext http, string token, DocumentSharingService service, CancellationToken ct) =>
        {
            DefensiveHeaders(http.Response);
            var view = await service.ResolvePublicAsync(token, false, Client(http), ct);
            if (view is null)
                return PublicNotFound();
            var downloadUrl = view.AllowDownload ? $"/s/{token}/download" : null;
            return Results.Ok(new { view.Title, view.TemplateName, view.RenderedText, view.CreatedAt, view.AllowDownload, downloadUrl });
        });
        external.MapGet("/{token}/download", async (HttpContext http, string token, DocumentSharingService service, CancellationToken ct) =>
        {
            DefensiveHeaders(http.Response);
            var view = await service.ResolvePublicAsync(token, true, Client(http), ct);
            return view is null || !File.Exists(view.PdfPath) ? PublicNotFound() : Results.File(view.PdfPath, "application/pdf", "shared-document.pdf", enableRangeProcessing: false);
        });
        return endpoints;
    }

    private static IResult ToOwnerResult(ShareOperationResult result)
    {
        if (result.NotFound)
            return Results.NotFound(new { error = "Share not found." });
        return result.Succeeded ? Results.NoContent() : Results.BadRequest(new { error = result.Error });
    }
    private static IResult PublicNotFound()
    {
        return Results.NotFound(new { error = "Shared document not found." });
    }

    private static Guid UserId(HttpContext http)
    {
        return CurrentUserContextFactory.FromClaims(http.User).UserId;
    }

    private static string Client(HttpContext http)
    {
        var ip = http.Connection.RemoteIpAddress?.GetAddressBytes();
        var prefix = ip is null ? "unknown" : Convert.ToHexString(ip.AsSpan(0, Math.Min(ip.Length, 8))).ToLowerInvariant();
        var agent = http.Request.Headers.UserAgent.ToString();
        return $"{prefix}:{(agent.Length > 48 ? agent[..48] : agent)}";
    }
    private static void DefensiveHeaders(HttpResponse response)
    {
        response.Headers.CacheControl = "private, no-store";
        response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers.XContentTypeOptions = "nosniff";
    }
}

public sealed record CreateGrantRequest(string Username, string AccessLevel);
public sealed record CreateLinkRequest(int LifetimeHours, bool AllowDownload);
