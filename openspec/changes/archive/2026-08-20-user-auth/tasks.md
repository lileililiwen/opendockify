## 1. Auth Module

- [x] 1.1 Create `src/OpenDockify.Auth`; add `Models/User.cs` (Id, Username
  unique, PasswordHash, DisplayName, Role enum, CreatedAt) and
  `Configuration/UserConfiguration.cs`
- [x] 1.2 Register `User` DbSet + configuration in `OpenDockify.Data`
  (`ApplyConfigurationsFromAssembly`)
- [x] 1.3 Implement `Services/AccountService.cs` — register (validate username
  uniqueness, min password length; hash with Identity `PasswordHasher<T>`),
  login (verify hash), me; `CurrentUserContext` reads `sub`/`role` claims
- [x] 1.4 `AuthModuleExtensions.cs` registers services; `OpenDockify.Api`
  wires it

## 2. JWT + Endpoints

- [x] 2.1 Add `Microsoft.AspNetCore.Authentication.JwtBearer` package
- [x] 2.2 Configure JWT bearer auth in `Program.cs`: issuer/audience/secret/
  expiry from config; startup validation: secret present and ≥ 32 bytes
- [x] 2.3 Add `RequireAdmin` authorization policy
- [x] 2.4 Add `/api/auth/register`, `/api/auth/login`, `/api/auth/me`
  endpoints; login rate-limit note (minimal, per-IP limiter or documented
  middleware)

## 3. Seeding

- [x] 3.1 Extend startup seeder: when DB empty, create the admin user from
  `Seed:AdminUsername`/`Seed:AdminPassword` (defaults `admin`/`admin123`,
  env-overridable); hash the password, never store plaintext
- [x] 3.2 Restart test: seeder does not create a duplicate admin

## 4. Isolation

- [x] 4.1 Add `OwnedResourceResult` helper (ok/not-found) used by module
  services to scope by current user id

## 5. Build & Verify

- [x] 5.1 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
- [x] 5.2 `dotnet ef migrations add AddUsers` and apply
- [x] 5.3 HTTP smoke tests (curl, Bearer token):
  - register new user → 200 + JWT; duplicate → 409; short password → 400
  - login valid → JWT; wrong password/unknown user → 401; expired/invalid
    token → 401
  - admin endpoint called as Regular → 403; as Administrator → 200
  - `/api/auth/me` with JWT → identity (id, username, role); without → 401
  - isolation: user B requesting user A's resource id → 404 — helper
    `OwnedResourceResult` delivered here; HTTP check lands with
    `template-engine`, which adds the first owned resources
