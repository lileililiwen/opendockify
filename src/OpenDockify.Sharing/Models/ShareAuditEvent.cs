namespace OpenDockify.Sharing.Models;

public sealed class ShareAuditEvent
{
    public Guid Id { get; set; }
    public Guid? DocumentId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActorCategory { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public string CoarseClient { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long CreatedAtUtcTicks { get; set; } = DateTimeOffset.UtcNow.UtcTicks;
}
