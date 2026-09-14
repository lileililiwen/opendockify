## Context

`OpenDockify.Auth` owns `User` + PBKDF2 hasher + 480-min JWT. `dotnet-platform-libs` owns tested lifecycle: `IRefreshTokenService/Store`, `IPasswordRecoveryService`, `ITwoFactorService`, EF store, Minimal-API mappers. Adoption is composition, not replacement: domain `User` stays app-owned; platform supplies token lifecycle.

## Goals / Non-Goals

Goals: 15-min access JWT, rotating refresh, revocation, change/recovery, TOTP opt-in, lockout, admin disable/enable.
Non-goals: SSO/OIDC, WebAuthn, orgs, mandatory 2FA (see proposal).

## Decisions

- **Keep `User` app-owned**, implement platform `IRefreshTokenStore` over new `RefreshTokens`/`RecoveryTokens` tables via `Platform.Identity.EntityFrameworkCore` mappings; no platform base-class inheritance.
- **Opaque refresh tokens** (256-bit, SHA-256 at rest), rotation with reuse detection (reuse -> revoke family + audit event).
- **TOTP opt-in** via `ITwoFactorService`; QR provisioning + 10 recovery codes (hashed); login flow: password -> 2FA challenge when enabled.
- **Lockout**: 5 failures/15 min per user+IP, `Platform.RateLimiting` account-recovery policy (3/hr) on recovery endpoints.
- **Admin disable** sets `IsDisabled`; login/refresh fail closed with `401`; existing access JWTs expire within 15 min (no hot-revoke list in this change).

## Risks / Trade-offs

- [Risk: BREAKING shorter JWT breaks old clients] -> Mitigation: Flutter refresh retry lands same change; document migration; old tokens drain within 8h.
- [Risk: refresh theft] -> Mitigation: rotation + reuse detection + HttpOnly secure cookie option + IP binding hint in audit.
- [Risk: recovery email abused] -> Mitigation: single-use 30-min tokens, enumeration-safe responses, rate limits; SMTP optional (console log when disabled).
- [Risk: platform version drift] -> Mitigation: centrally pinned versions, `eng/package-manifest`-style pin check in CI.
- Licensing: no AGPL/iText touch; QuestPDF MIT note unchanged.

## Migration Plan

1. Add platform refs (pinned); add `RefreshTokens`, `RecoveryTokens`, `TwoFactorSecrets` entities + migration.
2. Implement store adapters + `AccountService` extensions; wire `AddPlatformIdentityLifecycle`.
3. Add `/refresh|/logout|/change-password|/recovery/*|/2fa/*`, admin disable/enable; flip default expiry 15.
4. Flutter refresh + new screens; HTTP smoke all scenarios; `dotnet build` 0/0.
