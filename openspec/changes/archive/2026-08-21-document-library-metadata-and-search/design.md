## Context

`DocumentService` already owns generation, immutable re-editing through `ParentId`,
owner-scoped reads, pagination, and deletion. `DocumentEndpoints` exposes those
operations, while the Flutter `DocumentsController` and document list/detail
screens consume them. This change reuses those paths and adds mutable library
metadata around immutable legal content.

## Goals / Non-Goals

**Goals:**

- Give each document an owner-editable title and archive state.
- Make large libraries searchable, filterable, sortable, and understandable from
  their summaries.
- Expose every owner-visible revision related through `ParentId`.
- Preserve strict owner filtering in every query and mutation.

**Non-Goals:**

- Full-text indexing, tags, sharing, bulk operations, or soft-delete trash.
- Mutation of snapshots, rendered text, PDFs, template identity, signing state, or
  ownership.
- Rendering changes; QuestPDF remains MIT-licensed and iText7 is not introduced.

## Decisions

- **Metadata on `Document`.** Add `Title` (maximum 200 characters) and
  `IsArchived`. A separate metadata table was rejected because these fields have a
  one-to-one lifecycle and are required in nearly every library query.
- **Server-generated initial titles.** New documents use the template name; re-edit
  versions inherit the original title. Migrated rows use an empty stored value and
  receive the current template name as an API fallback, avoiding fabricated legal
  metadata in the immutable snapshot.
- **Dedicated library DTO.** Paginated queries project document plus template name
  into a summary instead of exposing persistence entities. This avoids N+1 template
  requests and keeps domain entities free of cross-module navigation properties.
- **Bounded query vocabulary.** Search is trimmed and capped at 100 characters,
  matches title or template name case-insensitively, archive mode is
  `active|archived|all`, and sorting is `newest|oldest|title`. Unknown values return
  `400` rather than silently changing semantics.
- **Metadata endpoint.** `PUT /api/documents/{id}/metadata` updates title and archive
  state together after owner lookup. Blank/oversized titles are rejected, and a
  foreign id returns `404`.
- **Connected version history.** The history endpoint loads only the caller's
  documents, walks parent edges in both directions from the requested id, and
  returns the connected revisions oldest-first. This supports legitimate branches
  without leaking another owner's records.
- **Flutter controller owns library query state.** Search is submitted/debounced by
  the screen, while filter and sort choices reset pagination. Metadata actions
  invalidate both list and detail providers.

## Risks / Trade-offs

- [Case behavior differs across database providers] → Normalize both operands with
  translated lowercase expressions and verify SQLite behavior; keep search limited
  to metadata rather than provider-specific full-text features.
- [Version traversal loads all owner documents] → Accept for the current self-hosted
  scale; use indexed root/version identifiers in a future migration if libraries
  become very large.
- [Archive is mutable while content is immutable] → Restrict mutation to title and
  archive fields and cover the boundary with tests and HTTP checks.
- [Migration cannot infer historical titles reliably] → Use template name at the API
  projection layer and populate a real title on the next metadata edit.

## Migration Plan

1. Add nullable-safe/defaulted title and archive columns plus owner/archive/title
   indexes.
2. Deploy additive API fields and endpoints; older clients ignore new JSON fields.
3. Deploy the Flutter library controls.
4. Rollback uses the prior application version; the additive columns can remain.

## Open Questions

- Tagging, favorites, trash, and scalable root-version identifiers remain separate
  future changes.
