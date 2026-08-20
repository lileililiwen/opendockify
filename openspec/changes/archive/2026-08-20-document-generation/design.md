## Context

Two modules work together:

**`OpenDockify.Generation`** orchestrates the workflow. It takes the template
(id), the filled values, and the selected clause ids; loads the template; runs
validation (required fields, per-field rules, finance validators — warnings
allowed); calls `TemplateRenderer` from `OpenDockify.Templates` to produce the
full text; appends the risk-notice text; calls `IPdfRenderer`; and persists the
`Document` record with its parameter snapshot. Validation errors abort with 400;
finance warnings are collected and returned in the response body (never block).

**`OpenDockify.Rendering`** owns `IPdfRenderer` (QuestPDF) and `IPdfOverlay`
(seam only). QuestPDF renders the text document with a CJK font family
registered at startup (font file resolved from config/env; Docker image ships
`fonts-noto-cjk`). The overlay seam (`IPdfOverlay.ApplyAsync(existingPdf,
overlay)`) is NOT implemented in MVP: default implementation is a no-op, and
code comments MUST carry the AGPL warning for iText7 (should it be used later
for cross-page seals) plus the note that legally-reliable stamping requires
external CA + timestamping. This keeps the build free of the AGPL dependency
until it's genuinely needed.

PDF storage: files live under a configurable `Storage:DocumentsPath`
(`/app/data/documents/{userId}/{documentId}.pdf`), the path is recorded on the
`Document` record, and delete removes the file. Multi-user isolation applies to
list/get/download/delete — owner only, foreign ids → 404.

Immutability: documents are created once. Re-edit = new record with `ParentId`.
No update/delete of history beyond owner delete (which removes the record +
file).

## Goals / Non-Goals

**Goals:**
- End-to-end generate → validate → render → risk notice → PDF → persist.
- Paginated own-list, get, download, delete, re-edit (new record).
- QuestPDF with CJK fonts; iText7 overlay seam documented but inert.

**Non-Goals:**
- E-signature stamping (reserved for `esign-extensions`).
- Batch generation, document import/export.
- Editing/rewriting historical records.
- OCR/scan handling.

## Decisions

- **Rendering depends on `TemplateRenderer` output (plain text with paragraph
  markers)**, keeping the PDF layer simple; rich formatting is future work.
- **QuestPDF font**: register `NotoSansCJKsc` (or resolved system CJK font)
  globally once at startup; document the path in README.
- **`IPdfOverlay` seam** with no-op default; iText7 package NOT referenced in
  MVP (compile-out-able per requirements).
- **Snapshot JSON** stores raw filled values + selected clause ids — the source
  for re-edit and historical verification; rendered text is stored separately.
- **Files stored on disk** (volume), not DB blobs — keeps DB small and PDFs
  streamable; blob storage is a future option.

## Risks / Trade-offs

- [Risk: QuestPDF license is Community for open-source use] → Mitigation: MIT
  project qualifies; add `QuestPDF.Settings.License = LicenseType.Community`
  with a comment, or document upgrade need if commercial use later.
- [Risk: AGPL contamination if iText7 overlay is added] → Mitigation: seam is
  an interface; iText never in the core build; explicit license comments at
  every touch point; README note on removing the dependency.
- [Risk: PDF path traversal via document ids] → Mitigation: ids are server
  GUIDs; path built from validated id + owner id, no user strings.
- [Risk: large PDFs exhausting memory] → Mitigation: QuestPDF streams to disk;
  download uses file stream with `EnableRangeProcessing` disabled.

## Migration Plan

1. Add `OpenDockify.Rendering`: `IPdfRenderer`, `QuestPdfRenderer`, `IPdfOverlay`
   + no-op, font registration.
2. Add `OpenDockify.Generation`: `Document` entity + configuration,
   `DocumentService`, generation orchestration.
3. Register `Document` DbSet in `OpenDockify.Data`; migration `AddDocuments`.
4. Endpoints: `/api/documents` (list), `POST /api/documents/generate`,
   `GET /api/documents/{id}`, `POST /api/documents/{id}/reedit`,
   `GET /api/documents/{id}/download`, `DELETE /api/documents/{id}`.
5. Verify HTTP scenarios incl. negatives (missing fields, foreign ids,
   delete removes file, re-edit immutability).

## Open Questions

- Should generation return the PDF inline or only the record id? Decision:
  record id + a `downloadUrl`; the React frontend fetches via download endpoint.
- Where should the watermark/anti-fraud background go? Deferred to future work
  (angleline-style 防伪底纹 not in MVP).
