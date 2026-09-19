using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenDockify.Generation.Models;
using Platform.Jobs;

namespace OpenDockify.Operations.Services;

/// <summary>
/// Recurring job that backfills the content-digest field on document
/// rows whose <c>ContentSha256</c> is null. Bounded to 100 documents
/// per pass so a long-running pass never starves other work. The
/// recurring cadence is hourly; the platform is responsible for
/// enqueueing the next pass when the current one finishes.
/// </summary>
[RecurringJob("0 * * * *", Name = "openDockify.operations.digestBackfill", TimeZone = "UTC")]
public sealed partial class DocumentDigestBackfillJobHandler : IRecurringJobHandler
{
    private const int _maximumPerPass = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentDigestBackfillJobHandler> _logger;

    public DocumentDigestBackfillJobHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentDigestBackfillJobHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<DocumentDigestBackfillService>();
        try
        {
            var report = await service.RunAsync(_maximumPerPass, cancellationToken);
            Log.Scanned(_logger, report.Scanned, report.Updated, report.MissingDocumentIds.Count, report.HasMore);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // recurring job must not fail the scheduler; the platform retries per its own policy.
        catch (Exception ex)
        {
            Log.Failed(_logger, ex);
            throw new InvalidOperationException("Digest backfill pass failed.", ex);
        }
#pragma warning restore CA1031
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information,
            "Digest backfill scanned {Scanned}, updated {Updated}, missing {Missing}, hasMore={HasMore}.")]
        public static partial void Scanned(ILogger logger, int scanned, int updated, int missing, bool hasMore);

        [LoggerMessage(2, LogLevel.Error, "Digest backfill pass failed.")]
        public static partial void Failed(ILogger logger, Exception exception);
    }
}
