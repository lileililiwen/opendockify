# backup-restore Specification

## Purpose
TBD - created by archiving change backup-restore-integrity. Update Purpose after archive.
## Requirements
### Requirement: Application-consistent backup bundle

The system SHALL create a point-in-time bundle containing the database, generated PDFs, required application data, and a versioned manifest with per-file size and SHA-256 checksum; it MUST exclude logs, caches, and configured external API secrets.

#### Scenario: Successful backup

- **WHEN** an administrator creates a backup with a valid encryption passphrase
- **THEN** the system streams an encrypted bundle from one consistent application snapshot and records a secret-free operation result

#### Scenario: Snapshot cannot be made consistent

- **WHEN** database or file snapshot coordination fails
- **THEN** the operation fails, publishes no successful bundle, and leaves live data unchanged

### Requirement: Backup validation

The system SHALL validate bundle format, supported application/schema version, authenticated encryption, manifest completeness, checksums, and database readability without modifying live state.

#### Scenario: Valid bundle dry run

- **WHEN** an administrator performs restore validation with the correct passphrase
- **THEN** the system reports compatibility, object counts, and required downtime without changing live data

#### Scenario: Corrupt bundle rejected

- **WHEN** authentication, checksum, manifest, or database validation fails
- **THEN** the bundle is rejected with a safe diagnostic and no partial restore

### Requirement: Controlled restore

The system SHALL restore only in maintenance mode after a successful validation and explicit confirmation tied to the validated bundle digest; it MUST preserve the pre-restore state as a recoverable rollback snapshot until verification succeeds.

#### Scenario: Confirmed restore

- **WHEN** an administrator confirms the exact validated digest while no normal requests are admitted
- **THEN** data is restored atomically, migrations are checked, and post-restore integrity verification runs before service resumes

#### Scenario: Restore verification fails

- **WHEN** post-restore verification fails
- **THEN** the prior state is reinstated and normal service remains unavailable until that rollback is verified

### Requirement: Archive integrity check

The system SHALL provide a read-only health check for database relationships, required PDF presence, stored content digests, migration state, and manifest consistency.

#### Scenario: Missing document file

- **WHEN** a persisted document references a missing or altered PDF
- **THEN** the checker reports the affected document id without exposing its content and does not silently repair it

### Requirement: Scheduled retention

The system SHALL optionally schedule encrypted backups to a deployer-mounted local path and SHALL apply count/age retention only to files created and positively identified by OpenDockify.

#### Scenario: Retention cleanup

- **WHEN** a scheduled backup succeeds
- **THEN** eligible expired OpenDockify bundles are removed while unknown files and the newest valid backup are retained

### Requirement: Provider recovery documentation and tests

The system SHALL document and integration-test SQLite recovery and the supported external-provider dump/restore contract, including key custody and recovery drill steps.

#### Scenario: Recovery drill

- **WHEN** the documented drill restores a production-like fixture into a clean deployment
- **THEN** authentication, template counts, document metadata, and PDF digests match the source fixture

