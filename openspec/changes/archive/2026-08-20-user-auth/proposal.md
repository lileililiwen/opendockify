## Why

OpenDockify needs a minimal but correct identity layer before any user-owned
data (templates, documents) can exist. The requirements explicitly say to skip
complex RBAC for the MVP: a simple user model with username/password login and
JWT authentication, two roles (Regular User, Administrator), and strict
multi-user isolation so users only ever see their own templates and generated
documents.

## What Changes

- `User` entity: id, username (unique), password hash (PBKDF2/Argon2 via
  ASP.NET Core identity or a purpose-built hasher), display name, role
  (Regular/Administrator), created-at timestamp.
- JWT authentication: `/api/auth/register`, `/api/auth/login`, `/api/auth/me`;
  tokens signed with a server-side secret from config/env, with expiry and
  `sub`/`role` claims.
- Roles enforced via an authorization policy (`RequireAdmin`); regular users
  cannot call admin endpoints.
- Multi-user isolation helper: every template/document query scopes by the
  authenticated user id; admin global templates are handled by the
  `template-engine` change.
- Seeding: the startup seeder creates the `Administrator` role and a default
  admin account (credentials from env/config, e.g. `admin` / `admin123` overridable).

## Capabilities

### New Capabilities

- `user-auth`: registration, login, JWT issuance/validation, roles (Regular
  User / Administrator), current-user identity endpoint, default admin seed.

### Modified Capabilities

None.

## Non-goals

- No RBAC matrix, departments, or organizational structures (explicitly
  deferred in requirements).
- No OAuth/SSO, no email verification, no password reset flows.
- No refresh-token rotation (single short-lived JWT is acceptable for MVP).
- No e-signature identities (that is the `esign-extensions` change).

## Impact

- New module `src/OpenDockify.Auth`: `Models/User.cs`,
  `Configuration/UserConfiguration.cs`, `Services/AccountService.cs`,
  `AuthModuleExtensions.cs`; JWT setup + auth endpoints in `OpenDockify.Api`.
- `OpenDockify.Data`: `User` DbSet + migration.
- Config: `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Secret`, `Jwt:ExpiryMinutes`,
  `Seed:AdminUsername`, `Seed:AdminPassword` (env-overridable).
- Seeder extended to create the admin account when the DB is empty.
