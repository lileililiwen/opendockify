namespace OpenDockify.Rendering.Services;

/// <summary>
/// Seam for overlaying content (e.g. seals/stamps) on an existing PDF — the
/// cross-page seal flow in a future e-signature feature.
///
/// LICENSE WARNING: a real implementation would use iText7, which is AGPL
/// licensed. AGPL obligations apply from the moment iText7 is added; the
/// dependency is deliberately NOT referenced in the MVP build so the project
/// stays AGPL-free. If implemented, every touch point must carry this warning
/// and the dependency must be removable.
/// </summary>
public interface IPdfOverlay
{
    /// <summary>
    /// Applies <paramref name="overlay"/> to <paramref name="sourcePdfPath"/>
    /// and writes the result to <paramref name="outputPdfPath"/>.
    /// </summary>
    Task ApplyAsync(
        string sourcePdfPath,
        string outputPdfPath,
        object overlay,
        CancellationToken cancellationToken);
}
