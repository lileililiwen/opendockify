namespace OpenDockify.Rendering.Services;

/// <summary>
/// Renders plain text (with paragraph markers) to a PDF file at
/// <paramref name="outputPath"/>. Throws an exception with a clear message when
/// rendering fails (e.g. the runtime lacks a CJK font).
/// </summary>
public interface IPdfRenderer
{
    Task RenderAsync(string text, string outputPath, CancellationToken cancellationToken);
}
