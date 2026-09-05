# Backup, integrity, and disaster recovery

OpenDockify backup bundles (`.odbak`) contain one application-consistent database snapshot, generated PDFs, and a versioned manifest. Every entry has a size and SHA-256 digest. The bundle is chunk-encrypted with AES-256-GCM; its key is derived with Argon2id (64 MiB, three iterations). Logs, caches, JWT secrets, AI keys, and configuration secrets are not included.

## Key custody

There is no recovery backdoor. Store the passphrase in a password manager separate from the backup volume, restrict its secret file to the service account, and test it in a clean recovery drill. Losing the passphrase makes the bundle unrecoverable. Do not put a passphrase in command history, API logs, Compose files, or the database.

The admin API accepts a passphrase only in the request body over the deployer's protected administration channel. The CLI accepts a path to a regular, non-symlink passphrase file:

```bash
dotnet run --project src/OpenDockify.Operations.Cli -- backup /run/secrets/backup-passphrase
dotnet run --project src/OpenDockify.Operations.Cli -- validate /app/data/backups/example.odbak /run/secrets/backup-passphrase
dotnet run --project src/OpenDockify.Operations.Cli -- integrity
```

## Configuration and schedules

`Backup:Directory` must be a dedicated mounted directory (default `/app/data/backups`). `Backup:ScheduleHours=0` disables schedules. To enable them, also set `Backup:SchedulePassphraseFile` to a mounted secret file. `RetainCount` and `RetainDays` default to 7 and 30. Cleanup never follows symlinks, never deletes unknown files, and always preserves the newest valid OpenDockify bundle.

Admin endpoints under `/api/admin/operations` create and validate backups, run the read-only integrity check, backfill at most 500 missing document digests per call, show secret-free status, and perform controlled restore. Validation changes no live state and returns a single-use, 30-minute receipt bound to the bundle SHA-256. Restore requires the exact receipt and digest.

Normal `dotnet restore` does not install Husky or download tools. Husky is an
explicit developer opt-in (`HUSKY=1 dotnet restore`), so automated restore and
backup/restore verification paths are not blocked by hook setup.

## SQLite recovery drill

1. Create a backup, copy it and its separately stored passphrase to a clean host, and record source user/template/document counts.
2. Start a clean deployment with an empty `/app/data` volume and the same OpenDockify version.
3. Copy the bundle into its configured backup directory. Validate it and check the reported provider and object counts.
4. Stop normal traffic. Submit restore with the validation receipt and exact digest. The application enters maintenance mode, stages the restore, keeps a rollback snapshot, runs SQLite `integrity_check`, and resumes only after verification. If verification fails it reinstates the prior database and documents.
5. Log in with a restored account. Compare authentication, template count, document metadata, and each PDF SHA-256 against the source. Run `/api/admin/operations/integrity`; it must report healthy.
6. Keep the rollback/snapshot until this drill and an external copy verification succeed. Repeat after material database or deployment changes.

Expected downtime is at least the validation estimate and grows with database/PDF size. Cancel before restore begins; once the atomic swap starts, allow it to finish or roll back.

## External database providers

Cross-provider conversion is rejected. PostgreSQL, MySQL/MariaDB, and SQL Server require a deployer-supplied, version-pinned native dump wrapper configured as `Backup:NativeDumpExecutable`. OpenDockify first runs `<wrapper> --version`, then invokes `<wrapper> <connection-string> <output-file>`. The wrapper must create one consistent native dump and exit nonzero on version mismatch or any error. Keep credentials out of wrapper output.

External-provider restore is intentionally offline and fail-closed in the web process. Configure a version-matched native restore tool, stop OpenDockify, validate the bundle, take an infrastructure snapshot, restore the native dump into an empty database using the provider's transactional/atomic facilities, restore PDFs, run migrations and integrity checks, then restart. Test the exact contract with a production-like fixture:

- PostgreSQL: pinned `pg_dump`/`pg_restore`, custom format, consistent snapshot.
- MySQL/MariaDB: pinned `mysqldump`/`mysql`, single transaction for transactional tables.
- SQL Server: native full backup/restore with checksum and a pinned server major version.

The application refuses to claim a successful external-provider backup when the configured wrapper is missing or fails. Infrastructure volume snapshots remain an additional defense; they do not replace application validation and recovery drills.

## Integrity diagnostics

The checker is read-only. It verifies required PDF presence, stored SHA-256 digests, and migration state. Findings expose document identifiers and safe reason codes, never document contents. Missing digests on pre-migration documents are reported until an administrator runs the bounded digest backfill endpoint.
