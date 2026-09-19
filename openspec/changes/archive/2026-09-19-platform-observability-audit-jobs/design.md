## Context

Bespoke `MigrateAndSeedAsync`, `ScheduledBackupService`, `ExpiredInterviewCleanupService`, `WebhookDeliveryWorker`, per-module audit gaps. Platform gives `IMigrationRunner`, `IJobDispatcher/Registry` + Hangfire adapter, audit interceptor/contracts.

## Goals / Non-Goals

Goals: migrator boundary, audit trail+export, `/readyz`, Hangfire jobs.
Non-goals: metrics exporter, Sentry, legal-hold (see proposal).

## Decisions

- **Migrator**: `MigrationRunner.ListPending/ApplyAsync` + seed callback; console `apply|list-pending --seed` parity for CLI; no auto-migrate bypass (explicit).
- **Audit**: `PlatformSaveChangesInterceptor` (actor via `CurrentUserContext`, clock via `IClock`) + `AuditEvents` table; login/admin/document events via `Auditing.AspNetCore`; 90-day retention job.
- **Jobs**: `HangfireJobDispatcher/Registry`, InMemory default, Postgres option reusing `Database:Provider`; dashboard OFF by default, requires admin callback when on.
- **Readiness**: `/readyz` checks DB migrations-current + storage + jobs-storage; `/healthz` stays liveness.

## Risks / Trade-offs

- [Risk: Hangfire InMemory lost on restart] -> Mitigation: recurring schedules re-registered at startup; durable work (backups) remains file/outbox-backed; Postgres option documented.
- [Risk: audit volume] -> Mitigation: bounded payloads, 90-day retention, paging on export.
- Licensing: no AGPL touch.

## Migration Plan

1. Add refs; swap seeder to `IMigrationRunner`; add interceptor.
2. Move backup/cleanup/webhook-retry/digest-backfill to `IRecurringJobHandler`s.
3. Add audit endpoints + `/readyz`; smoke + retention test.
