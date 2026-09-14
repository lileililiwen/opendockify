using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenDockify.Auth.Models;
using PlatformAuditHook = Platform.Identity.Contracts.IIdentityAuditHook;
using PlatformIdentityAuditEvent = Platform.Identity.Contracts.IdentityAuditEvent;

namespace OpenDockify.Auth.Services;

/// <summary>
/// Persists <see cref="PlatformIdentityAuditEvent"/> records to the
/// <c>IdentityAuditEvents</c> table. The platform's lifecycle services
/// invoke this hook to record refresh rotations, reuse detections,
/// password changes, and recovery starts/completions.
/// </summary>
public sealed class IdentityAuditHook : PlatformAuditHook
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly DbContext _db;

    public IdentityAuditHook(DbContext db)
    {
        _db = db;
    }

    public async ValueTask RecordAsync(PlatformIdentityAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        var entity = new IdentityAuditEvent
        {
            Id = Guid.NewGuid(),
            Action = auditEvent.Action,
            SubjectId = auditEvent.SubjectId,
            Succeeded = auditEvent.Succeeded,
            OccurredAt = auditEvent.OccurredAt,
            Metadata = JsonSerializer.Serialize(auditEvent, _jsonOptions),
        };
        _db.Set<IdentityAuditEvent>().Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
