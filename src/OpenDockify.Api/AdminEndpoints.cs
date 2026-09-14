using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenDockify.Auth.Services;
using Platform.Admin.Contracts;

namespace OpenDockify.Api;

/// <summary>
/// Admin-only endpoints. Requires the <c>RequireAdmin</c> policy (role
/// Administrator). Domain-specific admin surfaces (settings, global templates)
/// land in their own changes; this is the gating proof + shared pattern.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin").RequireAuthorization("RequireAdmin");

        group.MapGet("/ping", () => Results.Ok(new { status = "admin ok" }));

        group.MapGet("/users", async (
            int? page,
            int? pageSize,
            string? search,
            string? sortBy,
            bool? descending,
            AdminStore store,
            CancellationToken ct) =>
        {
            var query = new AdminQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 25,
                Search: search,
                SortBy: sortBy,
                Descending: descending ?? false);
            var result = await store.GetUsersAsync(query, ct);
            return Results.Ok(result);
        });

        group.MapPost("/users/{id:guid}/disable", async (
            Guid id,
            System.Security.Claims.ClaimsPrincipal principal,
            AdminStore store,
            CancellationToken ct) =>
        {
            var actorId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var result = await store.SetUserEnabledAsync(id.ToString(), false, actorId, null, ct);
            if (!result.Succeeded)
            {
                return Results.Json(
                    new { error = result.Code ?? "disable_failed" },
                    statusCode: result.Code == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
            }
            return Results.Ok(new { id, enabled = false });
        });

        group.MapPost("/users/{id:guid}/enable", async (
            Guid id,
            System.Security.Claims.ClaimsPrincipal principal,
            AdminStore store,
            CancellationToken ct) =>
        {
            var actorId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var result = await store.SetUserEnabledAsync(id.ToString(), true, actorId, null, ct);
            if (!result.Succeeded)
            {
                return Results.Json(
                    new { error = result.Code ?? "enable_failed" },
                    statusCode: result.Code == "not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
            }
            return Results.Ok(new { id, enabled = true });
        });

        group.MapGet("/audit", async (
            int? page,
            int? pageSize,
            string? search,
            string? sortBy,
            bool? descending,
            AdminStore store,
            CancellationToken ct) =>
        {
            var query = new AdminQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 25,
                Search: search,
                SortBy: sortBy,
                Descending: descending ?? false);
            var result = await store.GetAuditAsync(query, ct);
            return Results.Ok(result);
        });

        return endpoints;
    }
}
