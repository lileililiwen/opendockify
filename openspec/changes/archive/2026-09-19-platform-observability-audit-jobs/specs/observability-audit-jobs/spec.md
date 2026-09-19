## ADDED Requirements

### Requirement: Audit trail and export

The system SHALL record login, admin, and document-lifecycle audit events with redacted payloads and SHALL expose paged export with 90-day retention.

#### Scenario: Login audited

- **WHEN** a login succeeds or fails
- **THEN** an audit event with actor, outcome, IP hint, and correlation id is stored with no password or token

#### Scenario: Audit export paged

- **WHEN** an administrator calls `GET /api/admin/audit?from&to&page&pageSize`
- **THEN** matching events return newest-first with total count, and events older than retention are absent

### Requirement: Migrator boundary and readiness

The system SHALL expose migration pending/apply via `IMigrationRunner` and SHALL report readiness including DB currency.

#### Scenario: Pending visible without migrate

- **WHEN** an operator runs the CLI `list-pending`
- **THEN** pending migration ids are listed and the database is unmodified

#### Scenario: Readiness reflects migrations

- **WHEN** migrations are pending and a client GETs `/readyz`
- **THEN** readiness reports unhealthy with reason `migrations-pending`, while `/healthz` stays `ok`

### Requirement: Recurring jobs via Hangfire

The system SHALL run scheduled backup, interview cleanup, webhook retry, and digest backfill as registered recurring jobs (InMemory default).

#### Scenario: Backup schedule registered

- **WHEN** `Backup:ScheduleHours>0` and the app starts
- **THEN** a recurring backup job with that interval is registered and visible in job telemetry, and setting `0` registers none
