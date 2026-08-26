using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Operations.Services;

namespace OpenDockify.Operations;

public static class OperationsModuleExtensions
{
    public static IServiceCollection AddOperationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Configuration.BackupOptions>(configuration.GetSection(Configuration.BackupOptions.SectionName));
        services.AddSingleton<MaintenanceMode>();
        services.AddSingleton<RestoreReceiptService>();
        services.AddScoped<BackupBundleService>();
        services.AddScoped<DatabaseSnapshotService>();
        services.AddScoped<ArchiveIntegrityService>();
        services.AddScoped<DocumentDigestBackfillService>();
        services.AddScoped<BackupRetentionService>();
        services.AddScoped<BackupCoordinator>();
        services.AddHostedService<ScheduledBackupService>();
        return services;
    }
}
