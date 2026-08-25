using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenDockify.Sharing.Models;
using OpenDockify.SystemConfig.Services;

namespace OpenDockify.Sharing.Services;

public sealed class SharingCleanupService(IServiceScopeFactory scopes) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            var settings = scope.ServiceProvider.GetRequiredService<ISystemConfigReader>();
            var days = await settings.GetAsync<int>(SettingKeys.SharingAuditRetentionDays, stoppingToken);
            if (days <= 0)
                days = 90;
            var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
            await db.Set<ShareAuditEvent>().Where(x => x.CreatedAtUtcTicks < cutoff.UtcTicks).ExecuteDeleteAsync(stoppingToken);
            await db.Set<ExternalShareLink>().Where(x => x.ExpiresAt < cutoff).ExecuteDeleteAsync(stoppingToken);
        }
    }
}
