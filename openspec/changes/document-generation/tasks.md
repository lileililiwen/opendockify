## 1. Rendering Module

- [ ] 1.1 Create `src/OpenDockify.Rendering`; add `IPdfRenderer` and
  `QuestPdfRenderer` (QuestPDF; register CJK font from config/env; text →
  PDF; paragraph markers → layout)
- [ ] 1.2 Add `IPdfOverlay` seam + no-op `DefaultPdfOverlay` with AGPL/iText7
  license comments + CA/timestamping note (NOT referenced by default build)
- [ ] 1.3 Add `QuestPDF.Settings.License = LicenseType.Community` with comment
  (MIT project qualifies)
- [ ] 1.4 `RenderingModuleExtensions.cs`; wire into `OpenDockify.Api`

## 2. Generation Module

- [ ] 2.1 Create `src/OpenDockify.Generation`; add `Models/Document.cs`
  (Id, OwnerId, TemplateId, Status, SnapshotJson, RenderedText, PdfPath,
  ParentId?, CreatedAt) and `Configuration/DocumentConfiguration.cs`
- [ ] 2.2 Implement `DocumentService` — generate (validate → render → risk
  notice → PDF → persist), re-edit (new record + ParentId), list (paged,
  own-only), get, delete (record + PDF file)
- [ ] 2.3 Wire finance validation (Finance module) into generation: required
  fields, non-negative amounts, interest warnings collected but non-blocking
- [ ] 2.4 `GenerationModuleExtensions.cs`; wire into `OpenDockify.Api`

## 3. Data + Migration

- [ ] 3.1 Register `Document` DbSet + configuration in `OpenDockify.Data`
- [ ] 3.2 Add `Storage:DocumentsPath` config (default `/app/data/documents`)
- [ ] 3.3 `dotnet ef migrations add AddDocuments` and apply

## 4. API Endpoints

- [ ] 4.1 `GET /api/documents?page=&pageSize=` — paginated own list, newest
  first
- [ ] 4.2 `POST /api/documents/generate` — body: templateId, values map,
  selected clause ids → validate → generate → returns record + warnings +
  downloadUrl
- [ ] 4.3 `GET /api/documents/{id}` — own or 404
- [ ] 4.4 `POST /api/documents/{id}/reedit` — creates new record with
  ParentId; original unchanged
- [ ] 4.5 `GET /api/documents/{id}/download` — PDF stream (`application/pdf`)
- [ ] 4.6 `DELETE /api/documents/{id}` — removes record + PDF file

## 5. Build & Verify

- [ ] 5.1 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
- [ ] 5.2 HTTP smoke tests:
  - Generate happy path → 200, record persisted, PDF file exists, download
    returns valid PDF bytes, text contains values + selected clauses + risk
    notice
  - Missing required field → 400, no record
  - Negative amount → 400
  - Over-cap interest → 200 with warning in response
  - Clause toggles: only selected clauses in rendered text
  - Re-edit → new record with parentId; original bytes unchanged
  - List pagination + foreign doc → 404 on get/download/delete
  - Delete → record + file gone
- [ ] 5.3 QuestPDF: verify Chinese text renders correctly (Docker image has
  fonts; local env must have CJK font installed)
