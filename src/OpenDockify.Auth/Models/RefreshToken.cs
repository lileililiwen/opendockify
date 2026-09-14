namespace OpenDockify.Auth.Models;

/// <summary>
/// Server-side refresh-token record. The opaque <c>Handle</c> is never stored
/// in cleartext — only its SHA-256 hash. <see cref="FamilyId"/> groups
/// descendants of the same login so reuse detection can revoke the whole tree.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string FamilyId { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }

    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokeReason { get; set; }
}
