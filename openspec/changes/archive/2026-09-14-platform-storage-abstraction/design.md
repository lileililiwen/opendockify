## Context

PDFs: `{Storage:DocumentsPath}/{userId}/{id}.pdf` via `File.*`; backups: `.odbak` files. Platform `IObjectStorage` + `LocalFileStorage` (atomic) + `S3Storage` (app-supplied client) match single-host default + optional MinIO without cloud lock-in.

## Goals / Non-Goals

Goals: storage interface, local default, S3 option, range + presigned download, health.
Non-goals: scanning, encryption-at-rest, CDN (see proposal).

## Decisions

- **Local default**: `LocalFileStorage` rooted at `/app/data/objects`; atomic write-then-rename; keys validated (`StorageObjectKey`, no traversal).
- **S3 opt-in**: `S3Storage` with deployer endpoint/bucket/keys; presigned GET 15 min for automation PDFs; no SDK in base package (adapter only).
- **Lazy backfill**: old `PdfPath` files keep serving; `digests/backfill` job copies to storage on access; no mass migration.
- **Range**: `Range: bytes=` -> `206` with `Accept-Ranges: bytes`; whole-file fallback preserved.

## Risks / Trade-offs

- [Risk: S3 misconfig breaks downloads] -> Mitigation: startup validation, provider health in `/status`, fail-closed with safe code only (no secrets in errors).
- [Risk: double-read during backfill] -> Mitigation: idempotent copy keyed by `ContentSha256`; concurrent copy guarded by `IIdempotencyStore` (next change).
- Licensing: QuestPDF MIT; iText7 AGPL overlay untouched and still removable.

## Migration Plan

1. Add refs; implement `StorageModule` (`AddPlatformStorage` selector local|s3).
2. Swap `QuestPdfRenderer` output + `BackupCoordinator` bundle I/O to `IObjectStorage`.
3. Add range + presigned handlers; extend `/status`; smoke local + MinIO.
