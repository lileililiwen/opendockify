## Why

Observability is `/healthz` + local logs only; audit covers sharing/AI but not login/admin/lifecycle; scheduled backup/cleanup/webhook retry are bespoke hosted services; EF patterns (audit, soft-delete, paging) are hand-rolled per module.

## What Changes

- Adopt `Platform.Observability` (redacted structured logging, activity/metric names), `Platform.Auditing.Contracts` + `EfCore` + `AspNetCore` (login/admin/document-lifecycle audit trail + `GET /api/admin/audit?from&to` export), `Platform.Persistence.EfCore` (save-changes audit interceptor, per-entity soft-delete/paging/specification helpers), `Platform.Persistence.EfCore.Migrator` (pending/apply + seed callback replaces bespoke `MigrateAndSeedAsync`), `Platform.Jobs` + `Platform.Jobs.Hangfire` (InMemory default; Postgres option) for scheduled backups, expired-interview cleanup, webhook retry, digest backfill.
- Add `/readyz` (DB + storage + jobs-storage checks); keep `/healthz` for Docker.
- Admin audit export (JSONL/CSV); retention 90 days default, env-overridable.

## Capabilities

### New Capabilities
- `observability-audit-jobs`: standard observability, full audit trail + export, migrator boundary, Hangfire-backed recurring jobs.

### Modified Capabilities
None — existing log lines preserved; new audit events additive; backup schedule semantics unchanged (`ScheduleHours=0` still disables).

## Non-goals

- No Prometheus/Grafana/OpenTelemetry exporter in this change (structured logs only; exporter is follow-up).
- No Sentry integration (follow-up).
- No legal-hold/DLP.
- No Postgres-by-default switch (SQLite remains default).

## Impact

- New refs: `Platform.Observability`, `Platform.Auditing.*`, `Platform.Persistence.EfCore`, `Platform.Persistence.EfCore.Migrator`, `Platform.Jobs`, `Platform.Jobs.Hangfire`.
- `OpenDockify.Data`: interceptor + migrator runner; `Operations`/`Interviews`/`Integrations`: move to `IRecurringJobHandler`; `Api`: `/readyz` + audit endpoints.
- Config: `Jobs:Hangfire:Storage=InMemory|PostgreSql`, `Audit:RetentionDays=90`.
- Docker: no new service (InMemory default); Postgres Hangfire optional via existing provider switch.
- Licensing: no AGPL touch; QuestPDF MIT note unchanged.
