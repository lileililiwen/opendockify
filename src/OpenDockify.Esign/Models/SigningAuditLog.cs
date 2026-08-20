namespace OpenDockify.Esign.Models;

/// <summary>
/// RESERVED — audit trail for signing events. No MVP code path writes to it;
/// the future e-signature capability owns these rows.
/// </summary>
public sealed class SigningAuditLog
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public string Actor { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string Detail { get; set; } = string.Empty;
}
