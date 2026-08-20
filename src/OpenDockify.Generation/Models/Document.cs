using OpenDockify.Esign.Models;

namespace OpenDockify.Generation.Models;

public enum DocumentStatus
{
    Generated = 0,
}

/// <summary>
/// A generated document. Immutable by design: re-editing creates a new record
/// whose <see cref="ParentId"/> references the original. The snapshot stores
/// the filled values + selected clause ids for re-edit and historical
/// verification; the rendered text and PDF path are recorded separately.
/// <see cref="SigningStatus"/> is reserved: it always stays
/// <see cref="Esign.Models.SigningStatus.NotInitiated"/> in the MVP — no flow
/// transitions it.
/// </summary>
public sealed class Document
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid TemplateId { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Generated;

    /// <summary>Reserved e-signature state; never changed by MVP flows.</summary>
    public SigningStatus SigningStatus { get; set; } = SigningStatus.NotInitiated;

    /// <summary>JSON: filled values map + selected clause ids.</summary>
    public string SnapshotJson { get; set; } = string.Empty;

    public string RenderedText { get; set; } = string.Empty;

    public string PdfPath { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
