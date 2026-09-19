# Handoff — platform-libs adoption queue

Active planning queue for adopting `~/code/dotnet-platform-libs` in OpenDockify.
Implement one change at a time in the dependency order below, archive it, update this handoff with evidence, and stop before selecting the next change.

Self-hosted constraint holds: no SaaS, no cloud services, no paid keys. Excluded platform adapters: Stripe/LemonSqueezy, SendGrid, RabbitMQ, Redis, Anthropic, SSO/SCIM.

## Planned queue (dependency-ordered)

1. `platform-identity-hardening` — 15-min JWT + refresh rotation/reuse-revoke, change/recovery, opt-in TOTP, lockout, admin disable. New spec `identity-hardening`. BREAKING expiry 480->15.
2. `platform-web-edge` — correlation/ProblemDetails+`code`, deny-default CORS, headers/HSTS, versioning, resilience, ForwardedHeaders, secret separation, HTTPS-only AI. New spec `web-edge-hardening`.
3. `platform-storage-abstraction` — `IObjectStorage`, Local default atomic, S3/MinIO option, range `206` + presigned automation fetch. New spec `storage-abstraction`.
4. `platform-observability-audit-jobs` — audit trail+export 90d, `IMigrationRunner`, Hangfire InMemory default, `/readyz`. New spec `observability-audit-jobs`.
5. `platform-notify-rate-quota` — SMTP self-hosted only, per-user rate/quota 429, platform idempotency/outbox/webhooks, link passwords. New spec `notify-rate-quota`.
6. `platform-ai-caching` — Ollama default, PII scrub on, token budget, 500-char redacted logs, HybridCache renders. New spec `ai-caching`.

## Verification evidence

- `openspec validate --changes --strict` — 6 passed, 0 failed (2026-09-14, proposals + specs + designs + tasks only, no implementation yet).
- `platform-identity-hardening` DONE 2026-09-14, commit `8a9b4ef` on `main`, archived as `2026-09-14-platform-identity-hardening`, spec `identity-hardening` (+4 requirements).
  - `dotnet build OpenDockify.sln`: 0 errors (2 pre-existing MSB3277 EF-version warnings in Finance.Tests, unrelated).
  - `dotnet test OpenDockify.sln`: 174/174 green (149 unit incl. 8 `IdentityLifecycleTests`, 5 architecture, 20 finance).
  - `flutter analyze --no-pub`: no issues; `flutter test`: 92/92 green (14 session incl. refresh/2FA).
  - HTTP smoke (`:5555`, fresh sqlite): rotation 200 → reuse 401 + family dead 401 → logout 204 → refresh 401 → change-password 200 → refresh 401 → recovery 202 known + 202 unknown → TOTP login `2fa-required` → verify 200 → admin disable → login 401 → 5× wrong + correct → 429.
  - Bugs found by smoke and fixed: `/refresh` parsed the new opaque handle as Guid (500); `OrderByDescending` on DateTimeOffset in `ResolveSubjectAsync` (SQLite, 500); `/2fa/verify` required auth so login-2FA could never complete (now anonymous, subject from challenge); `/register` issued no refresh handle (now full session); login now issues the 2FA challenge (was null).
- `platform-web-edge` DONE 2026-09-14, commit `b4accbf` on `main`, archived as `2026-09-14-platform-web-edge`, spec `web-edge-hardening` (+2 requirements).
  - `dotnet build OpenDockify.sln`: 0 errors (2 pre-existing MSB3277 EF-version warnings, unrelated).
  - `dotnet test OpenDockify.sln`: 191/191 green (174 prior + 17 new `WebEdgeBootstrapTests`).
  - `flutter analyze`: no issues; `flutter test`: 96/96 green (92 prior + 4 new correlation/code contract tests).
  - HTTP smoke dev (`:5555`): correlation generated when absent and echoed when supplied, security headers (`X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, `CSP frame-ancestors 'none'`) on every response, CORS preflight from unlisted origin omits `Access-Control-Allow-Origin` and the allowlist origin (`http://localhost:8080`) returns the full CORS headers, `/health` and `/openapi/v1.json` mapped, `X-Forwarded-For` honored from loopback.
  - HTTP smoke prod (`:5556`): same defaults; `Sharing:HashKey == Jwt:Secret` refuses to start with the web-edge validator message; remote `http://` `Ai:Endpoint` refuses to start; loopback `http://` is allowed only when `Ai:AllowInsecureHttp=true`; HSTS enforced; `Web:Cors:AllowedOrigins` explicit allowlist (`https://app.example,https://admin.example`) — preflight from each allowed origin returns `Access-Control-Allow-Origin`, preflight from any other origin omits it.
  - Bugs found and fixed: `MapPlatformEndpoints` required `AddHealthChecks()` (added); the security-headers middleware was not in the pipeline until we replaced `UsePlatformCorrelation` with the bundled `UsePlatformWeb` (correlation + security headers + request limits + timeout).
- `platform-storage-abstraction` DONE 2026-09-14, commit `b675cf4` on `main`, archived as `2026-09-14-platform-storage-abstraction`, spec `storage-abstraction` (+2 requirements).
  - `dotnet build OpenDockify.sln`: 0 errors (2 pre-existing MSB3277 EF-version warnings, unrelated).
  - `dotnet test OpenDockify.sln`: 200/200 green (191 prior + 9 new `StorageAbstractionTests`).
  - `flutter analyze --no-pub`: no issues (server-only change, no Flutter work; `flutter test` not rerun because no app sources changed).
  - HTTP smoke (`:5555`, fresh sqlite, `Storage:Provider=local`): finalize writes PDF under `pdfs/{userId:N}/{docId:N}.pdf`, `GET /api/documents/{id}/download` returns full body with `Accept-Ranges: bytes` and the content digest matches; `Range: bytes=0-9` returns `206` with `Content-Range: bytes 0-9/N`; `Range: bytes=999-2000` returns `416` with `Content-Range: bytes */N`; `GET /api/v1/automation/documents/{id}/pdf` returns a 15-min presigned URL with no credentials in the body or query string; `GET /api/admin/operations/status` reports `{ provider: "local", state: "Healthy" }` with no secret; backup create uploads `backups/{name}.odbak`, validate/restore round-trip byte-identical, integrity check passes.
  - Bugs found and fixed: the unsatisfiable-range (`416`) branch of `RangeAwarePdfResultAsync` returned `Results.StatusCode(416)` without setting `http.Response.StatusCode` directly, so the response still carried the default `200`; flipped to set the status code on the response before returning `Results.Empty` (mirrors the `206` path).

- `platform-observability-audit-jobs` DONE 2026-09-19, commit `7798b3d` on `main`, archived as `2026-09-19-platform-observability-audit-jobs`, spec `observability-audit-jobs` (+3 requirements).
  - `dotnet build OpenDockify.sln`: 0 errors, 0 warnings.
  - `dotnet test OpenDockify.sln`: 204/204 green (179 unit incl. 4 new `ObservabilityAuditJobsTests`, 5 architecture, 20 finance).
  - `openspec validate platform-observability-audit-jobs --strict`: valid; `openspec validate --changes --strict`: 3 passed, 0 failed.
  - `migrate list-pending` (CLI): 16 pending incl `20260914144750_AddAuditEvents`, no db file created (no-mutate).
  - HTTP smoke (`:5555`, fresh sqlite, dev): `/healthz ok`; `/readyz ok reason=ready` with `database`+`storage` checks; login 200 → `http.request Success` audited with correlation id and no password/token in metadata; bad login 401 → `http.request Denied` + `authorization.denied` security event; `GET /api/admin/audit` paged (`page/pageSize/total/items` newest-first); `/export?format=jsonl` 200, `/export?format=csv` 200.
  - `/readyz` pending-unhealthy: deleted `AddAuditEvents` history row at runtime → `status unhealthy reason migrations-pending pending=[...]` with HTTP 503 while `/healthz` stayed `ok`.
  - Retention/schedule covered by unit tests (90d default purge, 7d override, `ScheduleHours=0` registers digest-backfill only, `ScheduleHours=6` registers `0 */6 * * *`, recorder masks password/token before persist).
  - Bugs found by smoke/review and fixed: `IAuditSink` kept platform `InMemoryAuditSink` because WIP used `TryAdd` after `AddPlatformAuditing` (events never persisted; now `RemoveAll`+`AddSingleton`); `EntityAuditSink` captured scoped `AppDbContext` as singleton (now scope-per-event); pooled-factory contexts returned null `IServiceScopeFactory` so only `AdminSeeder` ran and the `MigrationRunResult` failure was silently ignored (seeder now uses host scope factory; `MigrateAndSeedAsync` logs and throws on failure); `AuditEvents.OccurredAt` was `DateTimeOffset` so SQLite `ORDER BY` threw 500 (now UTC `DateTime`, migration amended pre-commit); JSONL export used `Utf8JsonWriter.WriteRawValue("\n")` (500; now per-row flush + LF byte); `ScheduledBackupJobHandler` lacked `[RecurringJob]` so enabling the schedule would throw at startup (attribute added); `/readyz` returned `migrations-pending`/`degraded` strings (now `unhealthy`+`reason` per spec); audit middleware ran after auth so 401s were missed (now before auth); Hangfire storage hardcoded InMemory (now `Jobs:Hangfire:Storage` → `BackgroundJobs:Hangfire:Storage` with InMemory fallback + Postgres connection string).

- `platform-notify-rate-quota` DONE 2026-09-19, commit `6154241` on `main`, archived as `2026-09-19-platform-notify-rate-quota`, spec `notify-rate-quota` (+4 requirements).
  - `dotnet build OpenDockify.sln`: 0 errors, 0 warnings.
  - `dotnet test OpenDockify.sln`: 214/214 green (189 unit incl. 9 new `NotifyRateQuotaTests` + 1 new `WebEdgeBootstrapTests.Development_requires_sharing_hash_key`, 5 architecture, 20 finance).
  - `openspec validate platform-notify-rate-quota --strict`: valid; `openspec validate --changes --strict`: 1 passed, 0 failed (remaining `platform-ai-caching`).
  - `flutter analyze --no-pub`: no issues; `flutter test`: 96/96 green (notification prefs tile + link-password field, no new widget tests).
  - HTTP smoke (`:5555`, fresh sqlite `/tmp/od-smoke/smoke.db`, dev): 17 migrations applied; `/healthz ok`; `/readyz ok reason=ready` (`database`+`storage` Healthy); login 200; `GET /api/admin/mail/status` `{enabled:false, provider:DevLogMailService, status:dev-fallback:Healthy}`; generate 200; link-with-password create 201 → no-password 401 → wrong 401 → correct 200 → `POST /s/{token}` unlock 200 → 5 consecutive wrong → correct 401 (locked); plain link 200; 3× generate 200 (rate/quota gates active, no 429 at low volume).
  - Per-user 429 isolation, AI/document quota 429 RFC9457, idempotent replay single-doc, webhook HMAC retry, HashKey-required covered by unit tests (`NotifyRateQuotaTests` 9/9).
  - Bugs found by smoke/review and fixed: new `AddShareLinkPasswords` migration lacked `[DbContext]` so EF discovered only 16 migrations and link create threw `no column named PasswordFailedAttempts` (attribute added, 17 applied); `PlatformWebhookDispatchJobHandler` registered in the recurring registry but not in DI so Hangfire threw `not registered` every minute (now `AddScoped`); `MailAddress.Create` has no display-name overload and `Results.Json` has no headers overload (plaintext intents now use `new MailAddress(addr, name)`, 429s set `Retry-After` on the response); analyzer gate (`TreatWarningsAsErrors` + husky `dotnet format`) required `partial` LoggerMessage hosts, `_`-prefixed consts, block-bodied `Key`, and sorted usings.

## Next change

`platform-ai-caching` is the only active change to implement in the next cycle. Do not interleave other changes.
