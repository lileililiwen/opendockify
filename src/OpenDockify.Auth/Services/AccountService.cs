using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenDockify.Auth.Models;

namespace OpenDockify.Auth.Services;

/// <summary>
/// Registration, login, and current-user queries. Passwords are hashed with
/// <c>PasswordHasher&lt;User&gt;</c> (PBKDF2, per-user salt, timing-safe verify)
/// — never stored in plaintext.
/// </summary>
public sealed class AccountService
{
    private readonly DbContext _db;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AccountService(DbContext db)
    {
        _db = db;
    }

    public async Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        return await _db.Set<User>()
            .SingleOrDefaultAsync(u => u.Username == username, cancellationToken);
    }

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _db.Set<User>()
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    /// <summary>
    /// Creates a user. Returns a result describing success or a validation
    /// failure (duplicate username, weak password).
    /// </summary>
    public async Task<RegisterResult> RegisterAsync(
        string username,
        string password,
        string displayName,
        UserRole role,
        CancellationToken cancellationToken)
    {
        username = username.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName.Trim();

        if (username.Length < 3 || username.Length > 64)
        {
            return RegisterResult.ValidationError("Username must be between 3 and 64 characters.");
        }

        if (password.Length < 8)
        {
            return RegisterResult.ValidationError("Password must be at least 8 characters.");
        }

        if (await FindByUsernameAsync(username, cancellationToken) is not null)
        {
            return RegisterResult.ValidationError("Username is already taken.", conflict: true);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = displayName,
            Role = role,
            PasswordHash = _passwordHasher.HashPassword(null!, password),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return RegisterResult.Success(user);
    }

    public async Task<User?> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await FindByUsernameAsync(username.Trim(), cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (user.IsDisabled)
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(null!, user.PasswordHash, password);
        return result == PasswordVerificationResult.Success ? user : null;
    }

    /// <summary>
    /// Sets a new password for the user. The caller is responsible for
    /// revoking all refresh-token families for the user after this returns.
    /// </summary>
    public async Task<RegisterResult> SetPasswordAsync(
        User user,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8)
        {
            return RegisterResult.ValidationError("Password must be at least 8 characters.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _db.SaveChangesAsync(cancellationToken);
        return RegisterResult.Success(user);
    }

    public async Task<bool> VerifyCurrentPasswordAsync(
        User user,
        string currentPassword,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        return result == PasswordVerificationResult.Success;
    }

    /// <summary>
    /// SHA-256 hash of a string — used to fingerprint sensitive values (e.g.
    /// before logging) without storing plaintext. Not a password hash.
    /// </summary>
    public static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed record RegisterResult(User? User, string? Error, bool Conflict = false)
{
    public bool Succeeded => User is not null;

    public static RegisterResult Success(User user)
    {
        return new(user, null);
    }

    public static RegisterResult ValidationError(string error, bool conflict = false)
    {
        return new(null, error, conflict);
    }
}
