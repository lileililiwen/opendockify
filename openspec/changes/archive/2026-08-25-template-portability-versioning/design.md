## Context

`TemplateService` already enforces built-in immutability and validates private definitions. DocuSeal exposes API-created templates, while OpenContracts makes import/export the foundation for safe forking. OpenDockify needs a smaller local package contract before templates can move between instances.

Research: https://github.com/docusealco/docuseal and https://github.com/Open-Source-Legal/OpenContracts/blob/main/docs/walkthrough/key-concepts.md

## Goals / Non-Goals

**Goals:** deterministic round trips, validate-before-write, explicit identity conflicts, provenance, and immutable revision binding.

**Non-Goals:** marketplace distribution, network fetching, executable packages, or DOCX/PDF ingestion.

## Decisions

- Use one canonical JSON file rather than ZIP until binary template assets exist. Canonicalize property ordering and UTF-8 encoding before hashing.
- Add `TemplateStableId`, `TemplateRevision`, and current-revision selection. Generated documents retain the precise revision id as well as template id.
- Treat import as validate then commit. A short-lived validation receipt binds package digest and conflict result; commit rejects a changed package or expired receipt.
- Permit only `create-copy` and authorized `new-revision`; never overwrite history. Imported built-in provenance does not create a locally mutable built-in.
- Cap bytes, JSON depth, collection sizes, string lengths, and decompression if a future package version adds an archive.

## Risks / Trade-offs

- [Revision migration affects current documents] -> backfill one revision per template and bind existing documents to it without changing snapshots.
- [Canonical JSON differs across runtimes] -> define canonicalization in tests with golden vectors.
- [Malicious imports consume resources] -> stream into a strict byte limit before bounded parsing and validation.

## Migration Plan

Backfill stable ids and revision 1 transactionally, then make new documents revision-aware. Add export before import and keep existing CRUD contracts compatible.

## Open Questions

- Binary assets and signed publisher packages require a later package-format version.

