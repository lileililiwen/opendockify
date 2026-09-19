namespace OpenDockify.Sharing.Models;

public sealed class ExternalShareLink
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid OwnerId { get; set; }
    public byte[] SecretHash { get; set; } = [];
    public bool AllowDownload { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
    public byte[]? PasswordHash { get; set; }
    public byte[]? PasswordSalt { get; set; }
    public int PasswordFailedAttempts { get; set; }
    public DateTimeOffset? PasswordLockedUntil { get; set; }
}
