using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenDockify.Integrations.Models;

namespace OpenDockify.Integrations.Services;

public sealed record ServiceTokenIssuance(ServiceToken Token, string ClearToken);

/// <summary>Authenticated service-token identity for one request.</summary>
public sealed class ServiceTokenAuthentication
{
    public ServiceTokenAuthentication(ServiceToken token)
    {
        Token = token;
        Scopes = AutomationScopes.Parse(token.Scopes);
    }

    public ServiceToken Token { get; }

    public Guid OwnerId => Token.OwnerId;

    public Guid TokenId => Token.Id;

    public IReadOnlyList<string> Scopes { get; }

    public bool HasScope(string scope)
    {
        return Scopes.Contains(scope);
    }
}

/// <summary>Redacted token view: never includes the verifier or any clear value.</summary>
public sealed record ServiceTokenView(
    Guid Id,
    string Name,
    string Prefix,
    string Scopes,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    DateTime? LastUsedAtUtc,
    int UseCount);

/// <summary>
/// Issues, verifies, and revokes opaque 256-bit service tokens. The clear
/// token (<c>odk_&lt;prefix&gt;_&lt;secret&gt;</c>) is shown once; storage keeps
/// only a keyed SHA-256 (HMAC) verifier so a database leak cannot mint tokens.
/// </summary>
public sealed class ServiceTokenService(DbContext db, IConfiguration configuration)
{
    public const string ClearTokenPrefix = "odk_";
    private const int _secretBytes = 32;
    private const int _prefixChars = 12;

    public async Task<ServiceTokenIssuance> CreateAsync(
        Guid ownerId,
        string name,
        IEnumerable<string> scopes,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var scopeList = scopes.ToList();
        if (scopeList.Count == 0 || scopeList.Any(s => !AutomationScopes.IsValid(s)))
        {
            throw new ArgumentException("At least one valid scope is required.", nameof(scopes));
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            throw new ArgumentException("Token name must be 1-100 characters.", nameof(name));
        }

        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(3650))
        {
            throw new ArgumentException("Token lifetime must be positive and at most 3650 days.", nameof(lifetime));
        }

        var prefix = GenerateUrlSafe(_prefixChars);
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(_secretBytes))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var clearToken = $"{ClearTokenPrefix}{prefix}_{secret}";

        var token = new ServiceToken
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = name.Trim(),
            Prefix = prefix,
            TokenHash = HashToken(clearToken),
            Scopes = string.Join(',', scopeList.Distinct(StringComparer.Ordinal)),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.Add(lifetime),
        };

        db.Set<ServiceToken>().Add(token);
        await db.SaveChangesAsync(cancellationToken);
        return new ServiceTokenIssuance(token, clearToken);
    }

    /// <summary>
    /// Resolves a clear token to its identity. Returns null for unknown,
    /// tampered, expired, or revoked tokens. Successful use updates the
    /// redacted usage counters.
    /// </summary>
    public async Task<ServiceTokenAuthentication?> AuthenticateAsync(
        string clearToken,
        CancellationToken cancellationToken = default)
    {
        if (clearToken.Length < ClearTokenPrefix.Length + _prefixChars + 2
            || !clearToken.StartsWith(ClearTokenPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = clearToken[ClearTokenPrefix.Length..];
        var separator = rest.IndexOf('_', StringComparison.Ordinal);
        if (separator != _prefixChars)
        {
            return null;
        }

        var prefix = rest[..separator];
        var hash = HashToken(clearToken);

        // Constant-time comparison over the stored verifier of the single
        // candidate row selected by the public prefix.
        var token = await db.Set<ServiceToken>()
            .SingleOrDefaultAsync(x => x.Prefix == prefix, cancellationToken);
        if (token is null
            || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(hash),
                Convert.FromHexString(token.TokenHash)))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        if (token.ExpiresAtUtc <= now || token.RevokedAtUtc is not null)
        {
            return null;
        }

        token.LastUsedAtUtc = now;
        token.UseCount++;
        await db.SaveChangesAsync(cancellationToken);
        return new ServiceTokenAuthentication(token);
    }

    /// <summary>Revokes immediately; scoped to the owning user.</summary>
    public async Task<bool> RevokeAsync(Guid ownerId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var token = await db.Set<ServiceToken>()
            .SingleOrDefaultAsync(x => x.Id == tokenId && x.OwnerId == ownerId, cancellationToken);
        if (token is null || token.RevokedAtUtc is not null)
        {
            return false;
        }

        token.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Owner-scoped, redacted listing for observability.</summary>
    public async Task<IReadOnlyList<ServiceTokenView>> ListAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        var views = await db.Set<ServiceToken>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new ServiceTokenView(
                x.Id,
                x.Name,
                x.Prefix,
                x.Scopes,
                x.CreatedAtUtc,
                x.ExpiresAtUtc,
                x.RevokedAtUtc,
                x.LastUsedAtUtc,
                x.UseCount))
            .ToListAsync(cancellationToken);
        return views;
    }

    private string HashToken(string clearToken)
    {
        using var hmac = new HMACSHA256(GetPepper());
        return Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(clearToken)))
            .ToLowerInvariant();
    }

    private byte[] GetPepper()
    {
        // Prefer an explicit deployment pepper; otherwise derive one from the
        // mandatory JWT secret so no new required credential is introduced.
        var pepper = configuration["Integrations:TokenPepper"];
        if (string.IsNullOrWhiteSpace(pepper))
        {
            pepper = $"opendockify-token-pepper:{configuration["Jwt:Secret"]}";
        }

        return System.Text.Encoding.UTF8.GetBytes(pepper);
    }

    private static string GenerateUrlSafe(int length)
    {
        const string alphabet = "abcdefghijkmnpqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        return new string(bytes.Select(b => alphabet[b % alphabet.Length]).ToArray());
    }
}
