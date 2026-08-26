## Context

Docker persists SQLite and generated files, but copying a live volume does not guarantee one point-in-time state and no restore drill exists. Paperless-ngx treats archive health checking as a first-class feature; docassemble deployment guidance explicitly warns that lost key/data volumes are unrecoverable. OpenDockify must make recovery verifiable while remaining local-only.

Research: https://github.com/paperless-ngx/paperless-ngx/blob/dev/docs/index.md and https://github.com/jhpyle/docassemble-compose

## Goals / Non-Goals

**Goals:** consistent encrypted backups, dry-run validation, rollback-safe restore, integrity reporting, conservative retention, and provider recovery tests.

**Non-Goals:** hosted storage, replication/HA, cross-provider conversion, or filesystem snapshot replacement.

## Decisions

- Add `OpenDockify.Operations` plus a CLI entrypoint. Large bundle bytes stream and never enter the database.
- Use a versioned TAR-like bundle encrypted with an authenticated streaming construction; derive keys from passphrases using a memory-hard KDF and store parameters/salt, never the key.
- Coordinate SQLite online backup API with an immutable file-set snapshot. For external providers, invoke a configured, version-checked native dump adapter; fail closed when unavailable.
- Restore is an offline/maintenance operation. Validation emits a receipt bound to bundle digest; confirmation requires that receipt. Swap staged data atomically where supported and retain a rollback snapshot until checks pass.
- The integrity checker is read-only and bounded. Store a PDF digest at finalization so later checks can identify corruption.
- Scheduled retention operates only in a configured mounted directory and deletes only files with a valid OpenDockify header/manifest; never follow symlinks.

## Risks / Trade-offs

- [False confidence across providers] -> provider-specific integration fixtures and documented tool/version contracts.
- [Restore destroys live data] -> maintenance mode, digest-bound confirmation, staged restore, rollback snapshot, and post-restore checks.
- [Passphrase loss makes backup unrecoverable] -> explicit key-custody warning and mandatory recovery drill; no backdoor key.
- [Retention deletes unrelated data] -> validated ownership marker, fixed directory, no symlink traversal, newest-valid preservation.

## Migration Plan

Add document digests and operation metadata, backfill digests from existing PDFs with a report, then ship backup/validate before enabling restore or schedules.

## Open Questions

- Hardware-backed key wrapping can be added later without changing the bundle manifest contract.

