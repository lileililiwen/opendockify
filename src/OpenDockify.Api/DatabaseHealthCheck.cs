using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenDockify.Data;

namespace OpenDockify.Api;

/// <summary>
/// Readiness check for the EF Core <see cref="AppDbContext"/>.
/// Pings the database to ensure the connection is open. Does not run
/// queries; uses <c>Database.CanConnectAsync</c> so the probe stays
/// cheap and provider-agnostic.
/// </summary>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("database:connect")
                : HealthCheckResult.Unhealthy("database:cannot-connect");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("database:exception", ex);
        }
    }
}
