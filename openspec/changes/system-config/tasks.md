## 1. SystemConfig Module

- [x] 1.1 Create `src/OpenDockify.SystemConfig`; add `Models/Setting.cs`
  (Id, Key unique, ValueJson), `Configuration/SettingConfiguration.cs`
- [x] 1.2 Add `SettingKeys` — allowlist with key names, value types, defaults,
  env mappings (`AI_ENDPOINT`, `AI_API_KEY`, `AI_MODEL`, `LPR_ONE_YEAR_RATE`,
  `AI_ENABLED`), and per-key validation rules
- [x] 1.3 Implement `SystemConfigService` — typed `GetAsync<T>`,
  `SetAsync(key, value)`, cache with invalidation on write
- [x] 1.4 Implement `IConfigurationStore` — merge precedence:
  environment variable > DB value > default
- [x] 1.5 Add `MaskSecret` + log redaction guard for `Ai.ApiKey` patterns
- [x] 1.6 `SystemConfigModuleExtensions.cs`; wire into `OpenDockify.Api`

## 2. Data + Migration

- [x] 2.1 Register `Setting` DbSet + configuration in `OpenDockify.Data`
- [x] 2.2 Extend seeder: when DB empty, insert `Ai.Enabled=false` and default
  `Lpr.OneYearRate` (from config/env)
- [x] 2.3 `dotnet ef migrations add AddSettings` and apply

## 3. Admin API

- [x] 3.1 Add `GET /api/admin/settings` (RequireAdmin) — all allowlisted keys,
  secrets masked
- [x] 3.2 Add `PUT /api/admin/settings/{key}` (RequireAdmin) — allowlist
  check (unknown → 400), type/range validation, persist, invalidate cache
- [x] 3.3 `PUT` of `Ai.ApiKey` accepts new value but reads always masked

## 4. Build & Verify

- [x] 4.1 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
- [x] 4.2 HTTP smoke tests:
  - Admin GET settings → all keys, `Ai.ApiKey` masked
  - PUT known key → 200, persisted, subsequent GET reflects change
  - PUT unknown key → 400
  - Regular user GET/PUT settings → 403
  - Set env `LPR_ONE_YEAR_RATE` → effective value is env value even if DB
    differs
  - No DB row + no env → default returned
  - Logs scanned for full API key → absent
  - Restart with populated DB → defaults not duplicated
