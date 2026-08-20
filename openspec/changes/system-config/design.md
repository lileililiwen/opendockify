## Context

All deployer-tunable knobs live in one place: a `Setting` table (key, JSON
value) served by `SystemConfigService`. Modules depend on `SystemConfigService`
(typed `GetAsync<T>/SetAsync`), never on the `Setting` entity directly, so the
storage/override mechanics stay behind one interface.

Override precedence is the trickiest part. `IConfigurationStore` merges three
layers: (1) environment variable (documented name mapping, e.g.
`AI_ENDPOINT`/`AI_API_KEY`/`LPR_ONE_YEAR_RATE`), (2) DB value, (3) default.
Environment wins so Docker deploys can inject secrets/values without touching
the DB. The env layer is read through `IConfiguration` (which already supports
`Environment` variables) so there's no double-maintenance of env parsing.

Secrecy: `Ai.ApiKey` is masked on read (last 4 chars shown, rest `••••`),
excluded from logs via a redaction helper, and only writable (never readable in
full) through the admin API. README/design must note the residual risk of
storing the key in the DB and recommend Ollama for privacy-sensitive use.

An allowlist of keys (`SettingKeys` static class) is the single source of truth
for key names + value types + env mappings + defaults; unknown keys are
rejected at the API boundary. This is also where the LPR defaults live.

## Goals / Non-Goals

**Goals:**
- Typed global settings with env-override.
- Allowlist enforcement.
- Secret masking + no-log guarantee.
- Admin read/update API; default seeding.

**Non-Goals:**
- Per-user settings.
- Config change audit/history.
- Arbitrary keys (allowlist only).
- Hot-reload of settings beyond cache invalidation on write.

## Decisions

- **`Setting` stores JSON text**; typed access via `System.Text.Json`.
- **`IConfigurationStore` composes env + DB + defaults**; env names mapped in
  `SettingKeys.EnvMapping`.
- **`SettingKeys` allowlist** drives validation, env mapping, defaults, and the
  admin API's key list.
- **Masking + redaction**: `MaskSecret(value)` returns
  `"••••" + last4`; a `LoggingRedaction` guard filters the key pattern in
  structured logs.
- **Cache**: in-memory dictionary invalidated on `SetAsync` (single-instance
  MVP; documented limitation).

## Risks / Trade-offs

- [Risk: API key stored in DB plaintext] → Mitigation: masked reads, no logs,
  README warning + Ollama recommendation; accepted for MVP.
- [Risk: cache staleness in multi-instance deploys] → Mitigation: SQLite is
  single-instance by default; note for Postgres multi-instance that cache is
  per-instance and invalidates on write within that instance.
- [Risk: env key naming collisions] → Mitigation: documented `EnvMapping`;
  namespaced prefixes (`AI_`, `LPR_`).

## Migration Plan

1. Add `OpenDockify.SystemConfig` module: `Setting`, `SettingConfiguration`,
   `SettingKeys`, `SystemConfigService`, `IConfigurationStore`.
2. Wire DI in `OpenDockify.Api`; add `Setting` DbSet to `OpenDockify.Data`.
3. Add admin endpoints `GET /api/admin/settings`, `PUT /api/admin/settings/{key}`
   (RequireAdmin, allowlist validation, secret masking).
4. Extend seeder: `Ai.Enabled=false`, default `Lpr.OneYearRate`.
5. Add EF migration `AddSettings`.
6. Verify: env-override precedence, allowlist rejection, masking, admin-only
   gating, seeding idempotency, no-secret-in-logs check.

## Open Questions

- Should settings support numeric range validation (e.g. LPR 0–100)? Decision:
  yes, per-key validation rules live in `SettingKeys` (decimal ranges, bool
  parse, URL format for endpoint).
