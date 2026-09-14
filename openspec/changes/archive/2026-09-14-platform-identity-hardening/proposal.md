## Why

OpenDockify issues 8-hour JWTs with no refresh rotation, no revocation, no lockout, and no password recovery or 2FA. `~/code/dotnet-platform-libs` already ships tested lifecycle contracts for exactly this, so re-implementing them locally wastes effort and keeps P0 security gaps open.

## What Changes

- Adopt `Platform.Identity.Contracts` (`IRefreshTokenService`, `IRefreshTokenStore`, `IPasswordRecoveryService`, `ITwoFactorService`) + `Platform.Identity.AspNetCore` endpoints + `Platform.Identity.EntityFrameworkCore` store.
- Shorten access JWT to 15 minutes; add opaque refresh rotation (`POST /api/auth/refresh`, `POST /api/auth/logout`), server-side revocation on logout/password change.
- Add password change (`POST /api/auth/change-password`), self-service recovery (`POST /api/auth/recovery/*` with single-use tokens), optional TOTP 2FA (`POST /api/auth/2fa/*`), configurable lockout (5 failures / 15 min).
- Add admin user lifecycle (`GET /api/admin/users`, `POST /api/admin/users/{id}/disable|enable`) via `Platform.Admin.Contracts` read model; keep exactly two roles.
- **BREAKING**: default `Jwt:ExpiryMinutes` 480 -> 15; clients MUST use refresh flow. Old long-lived tokens remain valid until expiry.

## Capabilities

### New Capabilities
- `identity-hardening`: refresh rotation, revocation, password change/recovery, TOTP 2FA, lockout, admin disable/enable.

### Modified Capabilities
None — additive endpoints; existing `user-auth` login/register/me semantics preserved.

## Non-goals

- No OAuth/OIDC/SSO/SAML/SCIM (deferred; self-hosted constraint, no external IdP dependency).
- No WebAuthn/passkeys in this change.
- No billing, orgs/teams, or RBAC matrix.
- No mandatory 2FA; deployer opts in per user.

## Impact

- New NuGet refs: `Platform.Identity.Contracts`, `Platform.Identity.AspNetCore`, `Platform.Identity.EntityFrameworkCore`, `Platform.Admin.Contracts` (centrally pinned, no floating versions).
- `src/OpenDockify.Auth`: new refresh/recovery/2FA services over platform contracts; `OpenDockify.Data`: refresh-token + recovery-token entities via platform EF mappings; `OpenDockify.Api`: new endpoints, 15-min JWT default.
- Config: `Identity:AccessExpiryMinutes=15`, `Identity:RefreshDays=14`, `Identity:Lockout*`, `Identity:Require2FA=false` (env-overridable).
- Flutter: login flow adds refresh storage + 401->refresh retry + change-password/recovery/2FA screens.
