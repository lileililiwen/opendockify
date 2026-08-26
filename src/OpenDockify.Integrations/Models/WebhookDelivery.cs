namespace OpenDockify.Integrations.Models;

public enum WebhookDeliveryState
{
    Pending = 0,
    Delivering = 1,
    Delivered = 2,
    Exhausted = 3,
    Blocked = 4,
}

/// <summary>
/// One (subscription, event) delivery with bounded exponential retry and a
/// database lease so a crashed worker's claims are recovered after expiry.
/// Request bodies, secrets, and signatures are never persisted here.
/// </summary>
public sealed class WebhookDelivery
{
    public Guid Id { get; set; }

    public Guid SubscriptionId { get; set; }

    /// <summary>Denormalized owner id for owner-scoped listing.</summary>
    public Guid OwnerId { get; set; }

    public Guid EventId { get; set; }

    public WebhookDeliveryState State { get; set; } = WebhookDeliveryState.Pending;

    public int AttemptCount { get; set; }

    public DateTime? NextAttemptAtUtc { get; set; }

    public Guid? LeaseOwner { get; set; }

    public DateTime? LeaseExpiresAtUtc { get; set; }

    public int? LastStatusCode { get; set; }

    public string? LastError { get; set; }

    /// <summary>Set when the destination was refused by outbound-safety checks.</summary>
    public string? BlockedReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? DeliveredAtUtc { get; set; }
}
