namespace OpenDockify.Auth.Models;

public enum UserRole
{
    Regular = 0,
    Administrator = 1,
}

/// <summary>
/// Minimal identity model per the MVP scope: no RBAC matrix, no departments.
/// Password is stored as a salted PBKDF2 hash via
/// <c>Microsoft.AspNetCore.Identity.PasswordHasher&lt;T&gt;</c> — never plaintext.
/// </summary>
public sealed class User
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Regular;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
