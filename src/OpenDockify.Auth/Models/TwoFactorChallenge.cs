namespace OpenDockify.Auth.Models;

/// <summary>
/// In-flight two-factor challenge. The challenge id is the value returned by
/// <c>POST /api/auth/2fa/start</c>; the user re-presents it with their TOTP
/// code at <c>POST /api/auth/2fa/verify</c>. Records are short-lived and
/// single-use.
/// </summary>
public sealed class TwoFactorChallenge
{
    public Guid Id { get; set; }

    public string ChallengeId { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}
