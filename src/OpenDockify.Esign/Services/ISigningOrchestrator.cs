namespace OpenDockify.Esign.Services;

/// <summary>
/// RESERVED — interface skeleton only. NOT implemented in the MVP.
///
/// This project orchestrates the signing workflow only. It does NOT issue
/// certificates and does NOT provide legally reliable timestamps. For legally
/// reliable signatures the deployer MUST integrate an external CA and
/// timestamping service (certificate issuance, RFC 3161 timestamps, seal
/// custody); this project never acts as a CA.
///
/// A future implementation may use iText7 (AGPL) to overlay signature seals on
/// existing PDFs; every such touch point must carry the AGPL license note and
/// the dependency can be dropped when that feature is unused.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1040:Avoid empty interfaces",
    Justification = "Reserved skeleton: the MVP registers no signing implementation; the empty interface exists so the future signing capability has a stable, documented seam.")]
public interface ISigningOrchestrator
{
}
