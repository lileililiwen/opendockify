using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Storage.Contracts;

namespace OpenDockify.Api;

/// <summary>
/// Readiness check for the configured object-storage provider. Reads
/// only the safe <see cref="StorageProviderStatus"/> snapshot; never
/// reports credentials, paths, or endpoint URLs.
/// </summary>
public sealed class StorageHealthCheck(StorageHealthProbe probe) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var status = probe.Current;
        var result = status.State switch
        {
            StorageProviderState.Healthy => HealthCheckResult.Healthy($"storage:{status.Provider}"),
            StorageProviderState.Unavailable => HealthCheckResult.Unhealthy(
                $"storage:{status.Provider}:{status.Code ?? "unavailable"}"),
            _ => HealthCheckResult.Degraded($"storage:{status.Provider}"),
        };
        return Task.FromResult(result);
    }
}
