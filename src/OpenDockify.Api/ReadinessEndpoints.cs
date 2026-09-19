using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenDockify.Data;
using Platform.Persistence.EfCore.Migrator;

namespace OpenDockify.Api;

/// <summary>
/// Readiness endpoint. Distinct from the liveness <c>/healthz</c>: it
/// reports <c>degraded</c> when migrations are pending so the
/// orchestrator can hold traffic until the host is fully migrated,
/// and <c>unhealthy</c> when the storage or job subsystems are not
/// reachable. The liveness endpoint is unchanged and continues to
/// return 200 unconditionally so Docker's healthcheck does not flap
/// during startup.
/// </summary>
public static class ReadinessEndpoints
{
    public static IEndpointRouteBuilder MapReadinessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/readyz", async (IServiceProvider sp, CancellationToken ct) =>
        {
            var runner = sp.GetService<IMigrationRunner>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            var migrationReport = new Dictionary<string, object>();
            bool migrationsCurrent = true;
            if (runner is not null)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var request = new MigrationRunnerRequest(
                        CreateContext: token => Task.FromResult<DbContext>(db),
                        SeedAfterApply: false);
                    var result = await runner.ListPendingAsync(request, ct);
                    if (!result.Succeeded)
                    {
                        migrationsCurrent = false;
                        migrationReport["reason"] = result.Failure?.Category.ToString() ?? "inspection-failed";
                    }
                    else if (result.PendingMigrations.Count > 0)
                    {
                        migrationsCurrent = false;
                        migrationReport["reason"] = "migrations-pending";
                        migrationReport["pending"] = result.PendingMigrations;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    migrationsCurrent = false;
                    migrationReport["reason"] = "inspection-failed";
                    migrationReport["exceptionType"] = ex.GetType().Name;
                }
            }
            else
            {
                migrationReport["reason"] = "migrator-not-registered";
                migrationsCurrent = false;
            }

            var checks = sp.GetService<HealthCheckService>();
            HealthReport? report = null;
            if (checks is not null)
            {
                try
                {
                    report = await checks.CheckHealthAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    report = null;
                    migrationReport["healthCheckException"] = ex.GetType().Name;
                    migrationReport["healthCheckMessage"] = ex.Message;
                }
            }

            var status = "ok";
            var reason = "ready";
            var httpStatus = StatusCodes.Status200OK;
            if (!migrationsCurrent)
            {
                status = "unhealthy";
                reason = "migrations-pending";
                httpStatus = StatusCodes.Status503ServiceUnavailable;
            }
            if (report is { Status: HealthStatus.Unhealthy })
            {
                status = "unhealthy";
                reason = "dependency-unhealthy";
                httpStatus = StatusCodes.Status503ServiceUnavailable;
            }
            else if (report is { Status: HealthStatus.Degraded } && status == "ok")
            {
                status = "degraded";
                reason = "dependency-degraded";
            }

            return Results.Json(new
            {
                status,
                reason,
                migrations = migrationReport,
                checks = report is null
                    ? Array.Empty<object>()
                    : (IEnumerable<object>)report.Entries.Select(kv => new
                    {
                        name = kv.Key,
                        status = kv.Value.Status.ToString(),
                        description = kv.Value.Description,
                    }),
            }, statusCode: httpStatus);
        });

        return endpoints;
    }
}
