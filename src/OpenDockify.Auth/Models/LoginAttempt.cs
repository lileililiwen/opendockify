namespace OpenDockify.Auth.Models;

/// <summary>
/// One row per login attempt. The lockout policy counts recent failures
/// grouped by username and IP within a rolling 15-minute window. Successful
/// attempts reset the counter for the same user+IP pair.
/// </summary>
public sealed class LoginAttempt
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
