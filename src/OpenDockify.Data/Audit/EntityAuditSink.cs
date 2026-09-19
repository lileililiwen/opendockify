using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenDockify.Data.Models;
using Platform.Auditing.Contracts;
using Platform.Core.Time;
using AuditEventContract = Platform.Auditing.Contracts.AuditEvent;

namespace OpenDockify.Data.Audit;

/// <summary>
/// Application-owned <see cref="IAuditSink"/> that persists every
/// <see cref="AuditEventContract"/> to the <c>AuditEvents</c> table. The
/// sink is a singleton that creates a fresh scope per event so it never
/// captures a scoped <see cref="AppDbContext"/> (the platform recorder
/// itself is a singleton). Writes use a dedicated context to avoid
/// re-entering a request's in-flight <c>SaveChanges</c> pipeline. The
/// sink is fail-open: any failure while writing is logged at warning
/// level and never re-thrown, so an audit-system outage cannot block
/// the originating request. Writes are best-effort and
/// single-attempt; the platform recorder wraps the call in its own
/// bounded policy.
/// </summary>
public sealed class EntityAuditSink : IAuditSink
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EntityAuditSink>? _logger;

    public EntityAuditSink(IServiceScopeFactory scopeFactory, ILogger<EntityAuditSink>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RecordAsync(AuditEventContract auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        var entity = new Models.AuditEvent
        {
            Id = Guid.NewGuid(),
            Action = auditEvent.Action,
            Category = auditEvent.Category,
            Outcome = auditEvent.Outcome.ToString(),
            Severity = auditEvent.Severity.ToString(),
            OccurredAt = auditEvent.OccurredAt.UtcDateTime,
            CorrelationId = auditEvent.CorrelationId,
            SubjectId = auditEvent.SubjectId,
            TenantId = auditEvent.TenantId,
            Source = auditEvent.Source,
            MetadataJson = Models.AuditEvent.SerializeMetadata(auditEvent.Metadata),
        };
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<Models.AuditEvent>().Add(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
#pragma warning disable CA1848
            _logger?.LogWarning(ex, "Audit sink failed to record event {Action}; dropped.", auditEvent.Action);
#pragma warning restore CA1848
        }
    }
}
