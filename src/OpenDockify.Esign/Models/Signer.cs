namespace OpenDockify.Esign.Models;

/// <summary>
/// RESERVED — a party to a signing workflow. No MVP endpoint or UI creates or
/// mutates signers; the future e-signature capability owns these rows.
/// </summary>
public sealed class Signer
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public int SigningOrder { get; set; }

    public SigningStatus Status { get; set; } = SigningStatus.NotInitiated;

    public DateTime? SignedAt { get; set; }
}
