namespace OpenDockify.Sharing.Models;

public enum DocumentGrantLevel { View = 0, Review = 1 }

public sealed class DocumentGrant
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid GranteeId { get; set; }
    public DocumentGrantLevel AccessLevel { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
}
