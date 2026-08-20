using Microsoft.Extensions.Configuration;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OpenDockify.Rendering.Services;

/// <summary>
/// QuestPDF-based text→PDF renderer with CJK font support. The font file is
/// resolved from config (<c>Storage:CjkFontPath</c>, family
/// <c>Storage:CjkFontFamily</c>); the Docker image pre-installs
/// <c>fonts-noto-cjk</c>. If no CJK font is registered, rendering fails with a
/// clear operator-facing error.
/// </summary>
public sealed class QuestPdfRenderer : IPdfRenderer
{
    private readonly string _fontFamily;
    private readonly bool _fontRegistered;

    public QuestPdfRenderer(IConfiguration configuration)
    {
        // OpenDockify is MIT-licensed (open-source), so the free QuestPDF
        // Community license applies. If the project ever becomes a closed
        // commercial product, this must be upgraded to a paid license.
        QuestPDF.Settings.License = LicenseType.Community;

        _fontFamily = configuration["Storage:CjkFontFamily"] ?? "Noto Sans CJK SC";
        var fontPath = configuration["Storage:CjkFontPath"]
            ?? "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc";

        if (File.Exists(fontPath))
        {
            FontManager.RegisterFont(File.OpenRead(fontPath));
            _fontRegistered = true;
        }
        else
        {
            _fontRegistered = false;
        }
    }

    public Task RenderAsync(string text, string outputPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_fontRegistered)
        {
            throw new InvalidOperationException(
                "No CJK font is available. Install a CJK font (e.g. fonts-noto-cjk) and set Storage:CjkFontPath / Storage:CjkFontFamily. The Docker image ships fonts-noto-cjk.");
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var paragraphs = text
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontFamily(_fontFamily).FontSize(12));
                page.Content().Column(column =>
                {
                    foreach (var paragraph in paragraphs)
                    {
                        column.Item().PaddingBottom(8).Text(paragraph);
                    }
                });
            });
        }).GeneratePdf(outputPath);

        return Task.CompletedTask;
    }
}
