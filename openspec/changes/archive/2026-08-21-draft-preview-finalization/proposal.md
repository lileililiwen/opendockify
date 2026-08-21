## Why

The current fill action immediately persists an immutable document and PDF, so
users cannot safely review the real rendered output and may lose partially entered
work when leaving the form. A deliberate draft, preview, and finalization boundary
reduces accidental records while preserving the immutable-history model.

## What Changes

- Add an authenticated preview endpoint that runs the same template access,
  validation, finance-warning, rendering, and risk-notice path as finalization but
  creates no database record or PDF.
- Add an explicit `/api/documents/finalize` endpoint while retaining `/generate`
  as a compatibility alias.
- Autosave form values and selected clauses locally on the user's device and
  restore them when the same template/re-edit form is reopened.
- Add Preview and Finalize actions to Flutter, display server warnings/rendered
  text before finalization, and clear the local draft only after success.
- Ensure re-edit preview/finalization keeps the existing immutable parent behavior.

## Capabilities

### New Capabilities

- `draft-preview-finalization`: Private local draft recovery, non-persisting server
  preview, and explicit immutable finalization across API and Flutter.

### Modified Capabilities

None.

## Non-goals

- No cross-device/server-side draft synchronization, collaborative editing,
  background PDF generation, or changes to immutable document history.
- No preview PDF; preview returns the exact rendered text and warnings.
- No browser autosave of passwords, tokens, or unrelated personal data.

## Impact

- `OpenDockify.Generation` shared preview/finalize orchestration.
- `OpenDockify.Api` preview/finalize document routes.
- Flutter document DTO/API contracts, preferences storage, dynamic form change
  notifications, fill workflow, and tests. No schema migration is required.
