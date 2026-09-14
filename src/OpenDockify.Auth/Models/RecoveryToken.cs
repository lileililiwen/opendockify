namespace OpenDockify.Auth.Models;

/// <summary>
/// Self-service password-recovery challenge. <see cref="ChallengeId"/> is the
/// value returned to the caller; <see cref="CodeHash"/> is the SHA-256 of the
/// single-use code (the cleartext code is only delivered out-of-band via
/// email). Records are enumeration-safe: an entry is always created, even
/// when no matching user exists, so timing and response shape do not leak
/// account existence.
/// </summary>
public sealed class RecoveryToken
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string ChallengeId { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}
