namespace OpenDockify.Rendering.Services;

/// <summary>
/// No-op overlay (MVP): copies the source PDF unchanged. A future e-signature
/// change may implement <see cref="IPdfOverlay"/> with iText7 (AGPL) for
/// cross-page seals; until then this keeps the build free of that dependency.
/// Note: legally-reliable stamping also requires an external CA and
/// timestamping service — that is the deployer's responsibility.
/// </summary>
public sealed class DefaultPdfOverlay : IPdfOverlay
{
    public async Task ApplyAsync(
        string sourcePdfPath,
        string outputPdfPath,
        object overlay,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // No-op: no overlay content in the MVP.
        await Task.Run(() => File.Copy(sourcePdfPath, outputPdfPath, overwrite: true), cancellationToken);
    }
}
