using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Operations.Services;
using Platform.Jobs;
using Platform.Jobs.DependencyInjection;

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

        services.AddPlatformJobs();
        services.AddScoped<ScheduledBackupJobHandler>();
        services.AddScoped<DocumentDigestBackfillJobHandler>();
        return services;
    }

    /// <summary>
    /// Registers the operations recurring-job descriptors. The backup
    /// handler is only registered when <c>Backup:ScheduleHours &gt; 0</c>
    /// and a passphrase file path is supplied, so a fresh deployment
    /// without a schedule is silent. The digest-backfill handler is
    /// always registered (hourly).
    /// </summary>
    public static IServiceProvider RegisterOperationsRecurringJobs(this IServiceProvider services, IConfiguration configuration)
    {
        var registry = services.GetRequiredService<IRecurringJobRegistry>();
        var scheduleHours = configuration.GetValue<int?>("Backup:ScheduleHours") ?? 0;
        var passphraseFile = configuration["Backup:SchedulePassphraseFile"];

        if (scheduleHours > 0 && !string.IsNullOrWhiteSpace(passphraseFile))
        {
            registry.Register(Platform.Jobs.RecurringJobAttribute.GetDescriptor(typeof(ScheduledBackupJobHandler))
                .WithName("openDockify.operations.scheduledBackup")
                .WithCron(BuildHourlyCron(scheduleHours)));
        }

        registry.Register(Platform.Jobs.RecurringJobAttribute.GetDescriptor(typeof(DocumentDigestBackfillJobHandler)));
        return services;
    }

    private static string BuildHourlyCron(int hours)
    {
        // Every N hours at minute 0; cap at 24 (daily).
        var bounded = Math.Clamp(hours, 1, 24);
        return bounded switch
        {
            1 => "0 * * * *",
            2 => "0 */2 * * *",
            3 => "0 */3 * * *",
            4 => "0 */4 * * *",
            6 => "0 */6 * * *",
            8 => "0 */8 * * *",
            12 => "0 */12 * * *",
            24 => "0 0 * * *",
            _ => $"0 */{bounded} * * *",
        };
    }
}
