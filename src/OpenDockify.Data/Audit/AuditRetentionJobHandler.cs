using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Core.Time;
using Platform.Jobs;
using RecurringJobAttribute = Platform.Jobs.RecurringJobAttribute;

namespace OpenDockify.Data.Audit;

/// <summary>
/// Daily recurring handler that purges <c>AuditEvents</c> rows older
/// than the configured retention window (90 days by default). Runs at
/// 03:00 UTC so the work is spread away from the documented backup
/// window. Failures are logged; the platform's recurring-job executor
/// is responsible for re-raising the failure to the scheduler's retry
/// policy.
/// </summary>
[RecurringJob("0 3 * * *", Name = "openDockify.audit.retention", TimeZone = "UTC")]
public sealed partial class AuditRetentionJobHandler : IRecurringJobHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly ILogger<AuditRetentionJobHandler>? _logger;

    public AuditRetentionJobHandler(
        IServiceScopeFactory scopeFactory,
        IClock clock,
        ILogger<AuditRetentionJobHandler>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AuditRetentionService>();
        try
        {
            var removed = await service.PurgeAsync(cancellationToken);
            if (_logger is { } logger)
            {
                Log.Purged(logger, removed, _clock.UtcNow, service.RetentionDays);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (_logger is { } logger)
            {
                Log.Failed(logger, _clock.UtcNow, ex);
            }
            throw new InvalidOperationException("Audit retention recurring job failed.", ex);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information,
            "Audit retention recurring job removed {Removed} events (now={Now:o}, retention={Days}d).")]
        public static partial void Purged(ILogger logger, int removed, DateTimeOffset now, int days);

        [LoggerMessage(2, LogLevel.Error,
            "Audit retention recurring job failed at {Now:o}.")]
        public static partial void Failed(ILogger logger, DateTimeOffset now, Exception exception);
    }
}
