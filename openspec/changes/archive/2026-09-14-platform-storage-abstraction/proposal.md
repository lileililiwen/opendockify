## Why

PDFs and backup bundles live as raw files on local disk (`PdfPath`, `/app/data`), with no presigned access, no range downloads, and no S3-compatible option. `Platform.Storage` (+`Local`/`S3`) already provides tested key validation, atomic writes, and presigned operations.

## What Changes

- Adopt `Platform.Storage` (`IObjectStorage`), `Platform.Storage.Local` (default, atomic writes under `/app/data/objects`), `Platform.Storage.S3` (optional, MinIO-compatible, deployer-supplied endpoint/bucket).
- Migrate PDF persistence (`Generation`/`Rendering`) and backup bundles (`Operations`) to `IObjectStorage`; keys `pdfs/{userId}/{docId}.pdf`, `backups/{name}.odbak`.
- Add range-capable download (`Accept-Ranges`, `206`) + presigned automation PDF fetch; keep existing download routes.
- Add storage health (`GET /api/admin/operations/status` extends with provider status); local remains zero-setup default.

## Capabilities

### New Capabilities
- `storage-abstraction`: object-storage boundary for PDFs/backups, local default, S3-compatible option, range/presigned download.

### Modified Capabilities
None — external download URLs preserved; storage swap is implementation detail.

## Non-goals

- No virus scanning, no encryption-at-rest beyond filesystem/volume (deployer concern).
- No migration of existing files in this change beyond lazy backfill job (old paths keep serving).
- No CDN, no multi-region replication.
- No change to QuestPDF rendering itself (iText7 AGPL note unchanged; QuestPDF MIT).

## Impact

- New refs: `Platform.Storage`, `Platform.Storage.Local`, `Platform.Storage.S3` (pinned).
- `OpenDockify.Generation`/`Rendering`/`Operations`: replace `File.Exists/Delete/ReadAllBytes` with `IObjectStorage`; config `Storage:Provider=local|s3`, `Storage:S3:*`.
- Docker: volume unchanged; S3 path needs only env vars, no compose service added.
- Flutter: none (download URLs unchanged, adds range resume opportunistically).
