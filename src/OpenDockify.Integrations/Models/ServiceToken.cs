namespace OpenDockify.Integrations.Models;

/// <summary>
/// Owner-scoped machine credential for the automation API. Only a keyed
/// SHA-256 verifier (<see cref="TokenHash"/>) is stored; the clear token is
/// returned exactly once at creation and supports immediate revocation.
/// </summary>
public sealed class ServiceToken
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Public, non-secret prefix embedded in the clear token for fast lookup.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Keyed SHA-256 hex verifier over the full clear token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Comma-separated scope names (see <c>AutomationScopes</c>).</summary>
    public string Scopes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime? LastUsedAtUtc { get; set; }

    public int UseCount { get; set; }
}
