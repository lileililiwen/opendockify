## Why

OpenDockify's runtime behavior — the AI-assist toggle, the LLM endpoint/key,
and the LPR reference values used for interest-rate warnings — must be
configurable by the deployer without code changes. The requirements are
explicit: no hardcoded third-party keys, everything in a system configuration
table, with environment variables able to override database settings for easy
Docker deployment.

## What Changes

- `Setting` entity (key/value JSON) in a `SystemConfig` module; typed read/write
  API (`GetAsync<T>`, `SetAsync`) with caching and a documented allowlist of
  known keys.
- Config keys: `Ai.Enabled` (bool), `Ai.Endpoint`, `Ai.ApiKey`, `Ai.Model`,
  `Lpr.OneYearRate` (decimal %), `Lpr.ReferenceDate` — the AI keys powering
  `ai-assist`; the LPR keys powering interest-rate validation in
  `finance-conversion`/`template-engine`.
- Environment-variable override precedence: env > DB settings > defaults. A
  helper `IConfigurationStore` merges the three layers for a given key.
- Secret handling: `Ai.ApiKey` is never returned in full by any API, is never
  logged, and is stored in the DB (documented risk); deployers are warned to
  use local Ollama for privacy-sensitive data.
- Admin API surface: GET/PUT system settings (admin-only), with masked
  secret values on read.

## Capabilities

### New Capabilities

- `system-config`: DB-backed global settings with typed access, env override
  precedence, admin read/update API, secret masking for API keys.

### Modified Capabilities

None.

## Non-goals

- No per-user configuration (settings are global).
- No config history/audit of changes (can be added later).
- No support for arbitrary/unknown keys (allowlist only — prevents injection of
  junk rows).

## Impact

- New module `src/OpenDockify.SystemConfig`: `Models/Setting.cs`,
  `Configuration/SettingConfiguration.cs`, `Services/SystemConfigService.cs`,
  `Services/IConfigurationStore.cs` (merge env+DB), `SystemConfigModuleExtensions.cs`.
- `OpenDockify.Data`: `Setting` DbSet + migration.
- Admin endpoints: `GET /api/admin/settings`, `PUT /api/admin/settings/{key}`
  (admin-only).
- Seed: default LPR value (configurable seed value from config/env) and
  `Ai.Enabled=false` default.
