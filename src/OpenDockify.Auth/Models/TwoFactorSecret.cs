namespace OpenDockify.Auth.Models;

/// <summary>
/// Per-user TOTP enrolment. The <see cref="Secret"/> is a base32-encoded shared
/// secret; <see cref="RecoveryCodesHash"/> stores the SHA-256 hashes of ten
/// one-time recovery codes (comma-separated). The <see cref="UserId"/> index
/// is unique so each user has at most one enrolment record.
/// </summary>
public sealed class TwoFactorSecret
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Secret { get; set; } = string.Empty;

    public string RecoveryCodesHash { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public DateTimeOffset? EnabledAt { get; set; }
}
