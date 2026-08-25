## Why

Private templates can only be created and maintained inside one instance, making backup, review, reuse, and controlled rollout impractical. A portable, versioned package is needed before template ecosystems or administrative promotion workflows can be safe.

## What Changes

- Define a canonical, schema-versioned template package format with deterministic export.
- Add validate-before-import, conflict handling, provenance, and immutable revision history.
- Allow rollback by creating a new revision rather than mutating historical definitions.
- Add administrator and Flutter import/export/version controls.

## Capabilities

### New Capabilities

- `template-portability`: Safe template package export/import, provenance, revision history, and rollback.

### Modified Capabilities

None.

## Non-goals

- No marketplace, remote registry, automatic network fetch, executable extensions, or cross-instance user transfer.
- No DOCX/PDF form ingestion in this change.

## Impact

- Template persistence and service contracts, EF migration, administrator API, streamed package handling, and Flutter template administration.

