namespace OpenDockify.Integrations.Models;

public enum IdempotencyOutcome
{
    Completed = 0,
    Failed = 1,
}

/// <summary>
/// Stored outcome of one automation mutation, bound to (token, key, route,
/// request digest). The serialized response is replayed verbatim on safe
/// retries; a digest mismatch is rejected with 409.
/// </summary>
public sealed class IdempotencyRecord
{
    /// <summary>Stable operation id exposed via the status endpoint.</summary>
    public Guid Id { get; set; }

    public Guid TokenId { get; set; }

    public Guid OwnerId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    /// <summary>SHA-256 hex of the exact request body.</summary>
    public string RequestDigest { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public string ResponseJson { get; set; } = string.Empty;

    public IdempotencyOutcome Outcome { get; set; }

    public Guid? DocumentId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }
}
