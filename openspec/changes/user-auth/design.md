## Context

The MVP deliberately avoids full RBAC. A single `User` entity with a role
enum (`Regular`/`Administrator`) is enough. Password hashing uses
`Microsoft.AspNetCore.Identity`'s `PasswordHasher<T>` (PBKDF2, per-user salt,
timing-safe comparison) without adopting the full Identity framework — this
keeps the module light while reusing a battle-tested hasher. JWT is issued
manually with `Microsoft.IdentityModel.JsonWebTokens`, signed HMAC-SHA256 with
`Jwt:Secret` from config/env.

Multi-user isolation is enforced at the query layer: module services always
filter by `userId` derived from the validated token's `sub` claim, never from
client-supplied ids. Owned-resource endpoints return `404` for foreign ids so
existence is not leaked (matches the spec's negative scenario).

Default admin seeding reads `Seed:AdminUsername` / `Seed:AdminPassword` from
config with sensible defaults, creating the account only when the DB is empty
(the seeder guard established by `platform-foundation`).

## Goals / Non-Goals

**Goals:**
- Register, login, JWT, `/api/auth/me`.
- Two roles; admin-only policy.
- Query-layer multi-user isolation helper.
- Default admin seed.

**Non-Goals:**
- RBAC matrix, departments, org structures.
- OAuth/SSO, email verification, password reset.
- Token refresh/revocation lists.

## Decisions

- **Use Identity's `PasswordHasher<T>`**, not the full Identity middleware:
  secure hashing with minimal surface.
- **JWT claims**: `sub` = user id, `role` = role; expiry from config
  (`Jwt:ExpiryMinutes`, default 480). Secret MUST be ≥ 32 bytes; validated at
  startup with a clear error if missing.
- **Admin policy name**: `RequireAdmin`; applied via `RequireAuthorization`
  or a policy check in Minimal API endpoint filters.
- **Isolation helper**: `CurrentUserContext` (resolved from token claims) +
  `OwnedResourceResult` pattern (returns typed not-found on cross-user access).

## Risks / Trade-offs

- [Risk: JWT secret weak or default in production] → Mitigation: startup
  validation + README warning to override `Jwt:Secret`; default secret only for
  dev.
- [Risk: single short-lived JWT, no refresh] → Accepted trade-off for MVP;
  documented as non-goal.
- [Risk: password hasher interop if users later want external identity] →
  Mitigation: hash stored in a dedicated column; migration path exists via a
  future SSO change.

## Migration Plan

1. Add `OpenDockify.Auth` module: `User`, `Role` enum, `UserConfiguration`.
2. Implement `AccountService` (register/login/me) + `CurrentUserContext`.
3. Register JWT bearer auth + `RequireAdmin` policy in `OpenDockify.Api`.
4. Add `/api/auth/register|login|me` endpoint group.
5. Extend seeder: create admin when DB empty.
6. Add EF migration `AddUsers`.
7. Verify HTTP scenarios (§4.3): register, duplicate-409, login-ok,
   wrong-password-401, expired/invalid token-401, admin endpoint gating,
   `/api/auth/me`, isolation smoke check (second user gets 404 on foreign id).

## Open Questions

- Should registration be open or admin-invite-only? Decision: open registration
  for MVP (self-hosted; each deployer controls their network exposure), README
  notes disabling it via config if desired.
