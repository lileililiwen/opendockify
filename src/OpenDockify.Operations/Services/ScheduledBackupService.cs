using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDockify.Operations.Configuration;

namespace OpenDockify.Operations.Services;

public sealed class ScheduledBackupService(
    IServiceScopeFactory scopes,
    IOptions<BackupOptions> options,
    Microsoft.Extensions.Logging.ILogger<ScheduledBackupService> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> _scheduledBackupFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(1, "ScheduledBackupFailed"),
            "Scheduled backup failed without publishing a successful bundle.");
    private readonly BackupOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.ScheduleHours <= 0 || string.IsNullOrWhiteSpace(_options.SchedulePassphraseFile))
            return;
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(_options.ScheduleHours), stoppingToken);
            try
            {
                var passphrasePath = Path.GetFullPath(_options.SchedulePassphraseFile);
                if (!File.Exists(passphrasePath) || File.GetAttributes(passphrasePath).HasFlag(FileAttributes.ReparsePoint))
                    continue;
                var passphrase = (await File.ReadAllTextAsync(passphrasePath, stoppingToken)).TrimEnd();
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<BackupCoordinator>().CreateAsync(passphrase, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _scheduledBackupFailed(logger, ex);
            }
        }
    }
}
