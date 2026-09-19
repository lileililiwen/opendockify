## 1. Persistence and audit

- [x] 1.1 Add pinned refs `Platform.Observability`, `Platform.Auditing.Contracts|EfCore|AspNetCore`, `Platform.Persistence.EfCore`, `Platform.Persistence.EfCore.Migrator`
- [x] 1.2 Swap seeder to `IMigrationRunner` + seed callback; add save-changes audit interceptor + `AuditEvents` migration

## 2. Jobs and endpoints

- [x] 2.1 Add `Platform.Jobs`, `Platform.Jobs.Hangfire` (InMemory default); move backup/cleanup/webhook-retry/backfill to `IRecurringJobHandler`
- [x] 2.2 Add `/readyz` + `GET /api/admin/audit` export with 90-day retention job

## 3. Verify

- [x] 3.1 `dotnet build` 0/0; `list-pending` no-mutate, `/readyz` pending-unhealthy, audit paged export
- [x] 3.2 Backup schedule register/none, retention purge; `openspec validate --change platform-observability-audit-jobs --strict`
