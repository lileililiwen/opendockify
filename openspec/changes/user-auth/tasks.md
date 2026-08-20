## 1. Auth Module

- [ ] 1.1 Create `src/OpenDockify.Auth`; add `Models/User.cs` (Id, Username
  unique, PasswordHash, DisplayName, Role enum, CreatedAt) and
  `Configuration/UserConfiguration.cs`
- [ ] 1.2 Register `User` DbSet + configuration in `OpenDockify.Data`
  (`ApplyConfigurationsFromAssembly`)
- [ ] 1.3 Implement `Services/AccountService.cs` — register (validate username
  uniqueness, min password length; hash with Identity `PasswordHasher<T>`),
  login (verify hash), me; `CurrentUserContext` reads `sub`/`role` claims
- [ ] 1.4 `AuthModuleExtensions.cs` registers services; `OpenDockify.Api`
  wires it

## 2. JWT + Endpoints

- [ ] 2.1 Add `Microsoft.AspNetCore.Authentication.JwtBearer` package
- [ ] 2.2 Configure JWT bearer auth in `Program.cs`: issuer/audience/secret/
  expiry from config; startup validation: secret present and ≥ 32 bytes
- [ ] 2.3 Add `RequireAdmin` authorization policy
- [ ] 2.4 Add `/api/auth/register`, `/api/auth/login`, `/api/auth/me`
  endpoints; login rate-limit note (minimal, per-IP limiter or documented
  middleware)

## 3. Seeding

- [ ] 3.1 Extend startup seeder: when DB empty, create the admin user from
  `Seed:AdminUsername`/`Seed:AdminPassword` (defaults `admin`/`admin123`,
  env-overridable); hash the password, never store plaintext
- [ ] 3.2 Restart test: seeder does not create a duplicate admin

## 4. Isolation

- [ ] 4.1 Add `OwnedResourceResult` helper (ok/not-found) used by module
  services to scope by current user id

## 5. Build & Verify

- [ ] 5.1 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
- [ ] 5.2 `dotnet ef migrations add AddUsers` and apply
- [ ] 5.3 HTTP smoke tests (curl, Bearer token):
  - register new user → 200 + JWT; duplicate → 409; short password → 400
  - login valid → JWT; wrong password/unknown user → 401; expired/invalid
    token → 401
  - admin endpoint called as Regular → 403; as Administrator → 200
  - `/api/auth/me` with JWT → identity; without → 401
  - isolation: user B requesting user A's resource id → 404
