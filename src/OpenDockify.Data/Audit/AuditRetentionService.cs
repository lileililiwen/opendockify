using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDockify.Data.Models;

namespace OpenDockify.Data.Audit;

/// <summary>
/// Bounded retention purge for the <c>AuditEvents</c> table. The
/// default retention is 90 days (overridable through
/// <c>Audit:RetentionDays</c>). Events at or older than the cutoff
/// are deleted in a single statement; the resulting row count is
/// logged. Failures are surfaced to the caller; the platform
/// <see cref="Platform.Auditing.Contracts.IAuditRecorder"/> never
/// invokes this job inline, so a failure cannot block an HTTP request.
/// </summary>
public sealed partial class AuditRetentionService
{
    private readonly DbContext _db;
    private readonly AuditRetentionOptions _options;
    private readonly ILogger<AuditRetentionService>? _logger;
    private readonly Func<DateTimeOffset> _utcNow;

    public AuditRetentionService(
        DbContext db,
        IOptions<AuditRetentionOptions> options,
        ILogger<AuditRetentionService>? logger = null)
        : this(db, options, logger, () => DateTimeOffset.UtcNow)
    {
    }

    public AuditRetentionService(
        DbContext db,
        IOptions<AuditRetentionOptions> options,
        ILogger<AuditRetentionService>? logger,
        Func<DateTimeOffset> utcNow)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
        _utcNow = utcNow;
    }

    public int RetentionDays => Math.Max(1, _options.RetentionDays);

    public async Task<int> PurgeAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = _utcNow().UtcDateTime.AddDays(-RetentionDays);
        var removed = await _db.Set<AuditEvent>()
            .Where(a => a.OccurredAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
        if (_logger is { } logger)
        {
            Log.Purged(logger, removed, cutoff, RetentionDays);
        }
        return removed;
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information,
            "Audit retention purge removed {Removed} events older than {Cutoff:o} (retention={Days}d).")]
        public static partial void Purged(ILogger logger, int removed, DateTime cutoff, int days);
    }
}
