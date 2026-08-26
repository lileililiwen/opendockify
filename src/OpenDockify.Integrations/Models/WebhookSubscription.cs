namespace OpenDockify.Integrations.Models;

/// <summary>
/// Owner webhook endpoint. The per-subscription HMAC secret is stored so
/// deliveries can be signed, but it is never returned by any API after the
/// creation response.
/// </summary>
public sealed class WebhookSubscription
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>Validated HTTPS destination; re-validated on every delivery attempt.</summary>
    public string Url { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    /// <summary>Comma-separated event type names (see <c>AutomationEvents</c>).</summary>
    public string EventTypes { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
