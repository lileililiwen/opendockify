## Why

Generated documents are currently identified mainly by timestamps and opaque
template ids, which becomes unusable as a user's library grows. Users need stable
titles, discoverable template information, lifecycle organization, and visible
version relationships without weakening owner isolation or immutable content.

## What Changes

- Add a user-editable document title and archive state while keeping rendered
  document content and snapshots immutable.
- Enrich document summaries with template names and lifecycle metadata.
- Add owner-scoped search, active/archived filtering, and deterministic sorting to
  the paginated document API.
- Add owner-scoped metadata update and version-history endpoints.
- Upgrade the Flutter document library with search, filters, sorting, rename,
  archive/restore actions, and version-history navigation.
- Preserve existing documents with a safe fallback title during migration.

## Capabilities

### New Capabilities

- `document-library`: Owner-scoped document metadata, discovery, lifecycle
  organization, and version-history behavior across API and Flutter client.

### Modified Capabilities

None.

## Non-goals

- No full-text search over rendered legal text, external search engine, tags,
  favorites, sharing, bulk operations, or trash/restore semantics.
- No mutation of rendered text, snapshots, PDFs, ownership, or signing state.
- No change to e-signature scope.

## Impact

- `OpenDockify.Generation` document model, configuration, service queries, and EF
  migration.
- `OpenDockify.Api` document request/response contracts and endpoints.
- Flutter document DTOs, API client, controllers, list/detail screens, and tests.
- Existing document-generation behavior remains compatible; new fields are
  additive and old records receive a display fallback.
