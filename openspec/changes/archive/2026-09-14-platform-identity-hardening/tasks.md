## 1. Packages and entities

- [x] 1.1 Add pinned refs `Platform.Identity.Contracts`, `Platform.Identity.AspNetCore`, `Platform.Identity.EntityFrameworkCore`, `Platform.Admin.Contracts`; `dotnet restore`
- [x] 1.2 Add `RefreshTokens`, `RecoveryTokens` entities + configs + migration; wire `ApplyConfigurationsFromAssembly`
- [x] 1.3 Implement `IRefreshTokenStore` adapter + recovery/2FA services over platform contracts

## 2. Endpoints and policy

- [x] 2.1 Add `/refresh|/logout|/change-password|/recovery/start|/recovery/complete|/2fa/*` + admin disable/enable; flip `Jwt:ExpiryMinutes` default 15
- [x] 2.2 Add lockout + account-recovery rate policy; enumeration-safe responses

## 3. Verify

- [x] 3.1 `dotnet build OpenDockify.sln` 0/0; `dotnet test` green
- [x] 3.2 HTTP smoke: rotation, reuse-revoke, logout, change-revokes, recovery 202-both, TOTP challenge, lockout 429, disabled 401
- [x] 3.3 Flutter refresh retry + new screens; `openspec validate --change platform-identity-hardening --strict`
