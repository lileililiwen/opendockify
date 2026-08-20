namespace OpenDockify.Esign.Models;

/// <summary>
/// Reserved signing lifecycle for a generated document. In the MVP every
/// document is <see cref="NotInitiated"/>; the future e-signature capability
/// transitions through the remaining states. This project only orchestrates
/// the signing workflow — it never issues certificates or provides legally
/// reliable timestamps (deployer MUST integrate an external CA + timestamping
/// service).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "The 'Signed' value name is mandated by the esign-extensions spec; renaming would drift from the source of truth.")]
public enum SigningStatus
{
    NotInitiated = 0,

    PendingSignature = 1,

    Signed = 2,

    Rejected = 3,

    Expired = 4,
}
