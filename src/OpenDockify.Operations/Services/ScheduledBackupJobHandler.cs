using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDockify.Operations.Configuration;
using Platform.Core.Time;
using Platform.Jobs;

namespace OpenDockify.Operations.Services;

/// <summary>
/// Recurring job that creates a backup bundle every
/// <see cref="BackupOptions.ScheduleHours"/> hours. The handler is
/// only registered when <c>ScheduleHours &gt; 0</c> and a passphrase
/// file path is configured. The passphrase is read on every execution
/// from the configured non-symlink file so operators can rotate it
/// without restarting the host. Failures are logged and rethrown so
/// the scheduler can apply its own retry policy.
/// </summary>
[RecurringJob("0 * * * *", Name = "openDockify.operations.scheduledBackup", TimeZone = "UTC")]
public sealed partial class ScheduledBackupJobHandler : IRecurringJobHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BackupOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<ScheduledBackupJobHandler> _logger;

    public ScheduledBackupJobHandler(
        IServiceScopeFactory scopeFactory,
        IOptions<BackupOptions> options,
        IClock clock,
        ILogger<ScheduledBackupJobHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (options.ScheduleHours <= 0 || string.IsNullOrWhiteSpace(options.SchedulePassphraseFile))
        {
            return;
        }

        var passphrasePath = Path.GetFullPath(options.SchedulePassphraseFile);
        if (!File.Exists(passphrasePath) || File.GetAttributes(passphrasePath).HasFlag(FileAttributes.ReparsePoint))
        {
            Log.Skipped(_logger, passphrasePath);
            return;
        }

        string passphrase;
        try
        {
            passphrase = (await File.ReadAllTextAsync(passphrasePath, cancellationToken)).TrimEnd();
        }
#pragma warning disable CA1031 // scheduler retries; we just rethrow with context.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.PassphraseReadFailed(_logger, passphrasePath, ex);
            throw new InvalidOperationException($"Failed to read passphrase file {passphrasePath}.", ex);
        }
#pragma warning restore CA1031

        await using var scope = _scopeFactory.CreateAsyncScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<BackupCoordinator>();
        try
        {
            var result = await coordinator.CreateAsync(passphrase, cancellationToken);
            Log.Created(_logger, result.Path, result.Digest, _clock.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // scheduler retries; we just rethrow with context.
        catch (Exception ex)
        {
            Log.Failed(_logger, _clock.UtcNow, ex);
            throw new InvalidOperationException($"Scheduled backup failed at {_clock.UtcNow:o}.", ex);
        }
#pragma warning restore CA1031
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Warning,
            "Scheduled backup skipped; passphrase file {Path} is missing or a symlink.")]
        public static partial void Skipped(ILogger logger, string path);

        [LoggerMessage(2, LogLevel.Error,
            "Scheduled backup failed reading passphrase file {Path}.")]
        public static partial void PassphraseReadFailed(ILogger logger, string path, Exception exception);

        [LoggerMessage(3, LogLevel.Information,
            "Scheduled backup created {Path} (digest={Digest}) at {Now:o}.")]
        public static partial void Created(ILogger logger, string path, string digest, DateTimeOffset now);

        [LoggerMessage(4, LogLevel.Error, "Scheduled backup failed at {Now:o}.")]
        public static partial void Failed(ILogger logger, DateTimeOffset now, Exception exception);
    }
}
