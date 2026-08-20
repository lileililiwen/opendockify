## Why

The core user workflow — select template → fill in fields → toggle optional
clauses → validate → render full text → append the risk notice → export PDF →
save a historical record — must be implemented so users can generate, preview,
download, re-edit, list, and delete documents. This is the capability that ties
the template engine, finance conversion, and PDF rendering together, while
preserving an immutable history: re-editing creates a new record and never
mutates the old one.

## What Changes

- `Document` entity: id, owner id, template id, title, status (Draft /
  Generated), parameter snapshot (filled values + selected clause ids as JSON),
  rendered text (with risk notice appended), pdf path/blob, created-at,
  parent-id (null for original, set when a document was produced by re-editing
  another document).
- Generation flow (`OpenDockify.Generation`): load template → validate all
  required fields → validate interest/amount rules (warnings only) → render
  body + selected clauses → append template risk-notice text → call the PDF
  renderer → persist the record with its snapshot.
- PDF rendering (`OpenDockify.Rendering`): QuestPDF render of the document text
  with embedded Chinese fonts; iText7 reserved for future existing-PDF overlay
  (cross-page seals) — licensed AGPL, so any iText usage carries license
  comments and is isolated behind an interface that can be compiled out.
- Re-edit endpoint: creates a NEW document record carrying `parentId` of the
  original; the original is never modified.
- Document endpoints: list (paginated, own only), get, download PDF, preview
  (rendered text), delete (own only), generate, re-edit.
- Deleting a document removes the record and its stored PDF blob.

## Capabilities

### New Capabilities

- `document-generation`: document entity + lifecycle (generate, re-edit,
  list, get, delete), parameter snapshot, risk-notice append, immutable history
  via parent-id.
- `pdf-rendering`: QuestPDF text→PDF with embedded CJK fonts; iText7 overlay
  seam (interface only in MVP, AGPL comments); PDF binary storage + download.

### Modified Capabilities

None.

## Non-goals

- No e-signature stamping (reserved; see `esign-extensions`).
- No batch generation, no JSON import/export of documents (deferred).
- No OCR or scanned-doc handling.
- No editing/deleting of historical records (immutable by design).

## Impact

- New modules `src/OpenDockify.Generation` and `src/OpenDockify.Rendering`;
  both reference `OpenDockify.Templates`, `OpenDockify.Finance`,
  `OpenDockify.SystemConfig`.
- `OpenDockify.Data`: `Document` DbSet + migration.
- `Document` links to `Template` by id (no nav to user per module rules).
- NuGet: `QuestPDF` (MIT). Optional `iText7` package referenced ONLY if the
  overlay seam is implemented; otherwise the seam is an interface with a
  no-op/default implementation.
- Endpoints under `/api/documents` + `/api/documents/{id}/download`.
- Seeder: no documents seeded.
