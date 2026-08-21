## Context

`DocumentService.GenerateAsync` currently performs validation, warnings, rendering,
PDF creation, and persistence as one operation. Flutter's dynamic form owns the
current values only in widget controllers, so leaving the screen loses work. The
new document library reinforces that generated records are immutable; preview must
therefore happen before that persistence boundary.

## Goals / Non-Goals

**Goals:**

- Reuse one authoritative server render/validation path for preview and finalization.
- Guarantee preview creates neither a document row nor a PDF.
- Recover in-progress form data privately for the same signed-in user and form.
- Require an explicit finalization action and clear recovered data only on success.

**Non-Goals:**

- Cross-device drafts, server draft entities, collaborative editing, or preview PDF.
- Any relaxation of owner/template access or immutable generated records.
- Rendering-library changes; QuestPDF remains MIT and iText7 is not introduced.

## Decisions

- **Preview-first orchestration in `DocumentService`.** Extract template access,
  definition/value validation, finance warnings, rendering, and risk-notice append
  into `PreviewAsync`. `GenerateAsync` consumes its successful result before PDF
  creation/persistence. Duplicating the render path was rejected because previews
  could drift from final output.
- **Explicit route plus compatibility alias.** Add `POST /api/documents/preview`
  and `/finalize`; keep `/generate` mapped to the same finalization handler for old
  clients. Preview returns rendered text, warnings, and template name only.
- **User-scoped secure local draft.** A small JSON snapshot is stored through
  `flutter_secure_storage`, keyed by authenticated user plus template or re-edit
  document id. Shared preferences were rejected because contract values can be
  sensitive. Storage contains values/selected clauses only, never JWTs in the same
  payload.
- **Debounced autosave with final flush.** Dynamic form changes emit raw snapshots
  without requiring validity. Flutter debounces writes and flushes the latest
  snapshot on disposal. Server validation still controls preview/finalization.
- **Re-edit compatibility.** Re-edit forms use the same preview API and their
  existing re-edit finalization endpoint, retaining `ParentId` and title inheritance.

## Risks / Trade-offs

- [Secure storage support varies by platform] → Reuse the already-required secure
  storage plugin and surface storage failures without blocking form editing.
- [Local drafts do not follow the user] → State this explicitly; server-side draft
  synchronization remains a later capability.
- [Frequent form changes cause writes] → Debounce writes and serialize only the
  bounded template snapshot.
- [Preview could accidentally persist] → Keep PDF/database work after the preview
  success boundary and verify document count/files at HTTP level.

## Migration Plan

1. Deploy additive preview/finalize routes; old clients continue using `/generate`.
2. Deploy secure local draft storage and the updated Flutter form.
3. No database migration or data backfill is required. Rollback leaves only
   harmless client secure-storage keys, which are overwritten or cleared later.

## Open Questions

- Server-synchronized drafts and preview PDFs remain separate future changes.
