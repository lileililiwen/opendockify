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

## Next change

`platform-storage-abstraction` is the only active change to implement in the next cycle. Do not interleave other changes.
