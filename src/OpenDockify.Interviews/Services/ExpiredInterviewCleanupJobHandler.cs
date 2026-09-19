using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenDockify.Interviews.Models;
using Platform.Jobs;

namespace OpenDockify.Interviews.Services;

/// <summary>
/// Hourly recurring job that removes expired interview sessions.
/// Replaces the previous <c>BackgroundService</c>-based
/// <c>ExpiredInterviewCleanupService</c>; the platform is now
/// responsible for triggering the pass. The handler is idempotent
/// and bounded: a single pass only deletes rows whose expiry is in
/// the past, so concurrent executions are safe.
/// </summary>
[RecurringJob("0 * * * *", Name = "openDockify.interviews.expiredCleanup", TimeZone = "UTC")]
public sealed partial class ExpiredInterviewCleanupJobHandler : IRecurringJobHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredInterviewCleanupJobHandler> _logger;

    public ExpiredInterviewCleanupJobHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredInterviewCleanupJobHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        try
        {
            var removed = await db.Set<InterviewSession>()
                .Where(session => session.ExpiresAt <= DateTimeOffset.UtcNow)
                .ExecuteDeleteAsync(cancellationToken);
            Log.Removed(_logger, removed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // recurring job must not fail the scheduler; the platform retries per its own policy.
        catch (Exception ex)
        {
            Log.Failed(_logger, ex);
            throw new InvalidOperationException("Expired interview cleanup pass failed.", ex);
        }
#pragma warning restore CA1031
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information, "Expired interview cleanup removed {Count} session(s).")]
        public static partial void Removed(ILogger logger, int count);

        [LoggerMessage(2, LogLevel.Error, "Expired interview cleanup pass failed.")]
        public static partial void Failed(ILogger logger, Exception exception);
    }
}
