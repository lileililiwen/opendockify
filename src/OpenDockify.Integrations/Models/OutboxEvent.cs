namespace OpenDockify.Integrations.Models;

/// <summary>
/// Transactional outbox row: committed in the same database transaction as the
/// mutation it describes, then fanned out to matching webhook subscriptions by
/// the delivery worker.
/// </summary>
public sealed class OutboxEvent
{
    /// <summary>Stable event id; receivers use it for replay protection.</summary>
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string Type { get; set; } = string.Empty;

    /// <summary>Canonical JSON body delivered verbatim (and signed) to receivers.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAtUtc { get; set; }
}
