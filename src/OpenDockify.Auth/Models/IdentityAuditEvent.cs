namespace OpenDockify.Auth.Models;

/// <summary>
/// Security-sensitive identity-lifecycle audit record. <see cref="Action"/>
/// is one of the dotted names emitted by the platform services
/// (for example <c>identity.refresh.reused</c>). Metadata is a free-form
/// JSON string with non-sensitive diagnostic context.
/// </summary>
public sealed class IdentityAuditEvent
{
    public Guid Id { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? SubjectId { get; set; }

    public bool Succeeded { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Metadata { get; set; }
}
