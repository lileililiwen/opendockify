using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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

        return endpoints;
    }
}
