## Context

Authentication already reuses `AccountService`, `JwtTokenService`, the
`RequireAdmin` policy, and `AdminSeeder`. The API currently constructs its JWT
key from configuration but production configuration is not validated as one
coherent bootstrap contract. The Docker image also declares a curl-based health
check without installing curl. This change strengthens those existing paths and
does not introduce a second identity system.

## Goals / Non-Goals

**Goals:**

- Fail fast with actionable errors before migrations when production secrets or
  seed credentials are missing or unsafe.
- Make registration policy and authentication throttles server-enforced.
- Make the documented Docker deployment boot and report health with explicit
  operator-provided secrets.
- Exercise security decisions through deterministic tests before implementation.

**Non-Goals:**

- Invitation, password recovery, SSO, refresh tokens, or additional roles.
- TLS termination or automated secret management.
- Changes to document rendering; QuestPDF remains MIT-licensed and iText7 is not
  introduced.

## Decisions

- **Central production validation in `OpenDockify.Auth`.** A pure validator will
  accept `IConfiguration` plus the environment name and return all configuration
  errors. `Program.cs` invokes it before building the application. This reuses the
  existing JWT minimum and keeps security rules testable without starting Kestrel.
  Relying on lazy singleton construction was rejected because failure timing then
  depends on which endpoint resolves `JwtTokenService` first.
- **Deployment configuration controls registration.** `Auth:AllowRegistration`
  defaults to false in base/production configuration and true only in development.
  A database toggle was rejected because an unauthenticated registration request
  should not depend on mutable application state during bootstrap.
- **ASP.NET Core fixed-window rate limiting.** Login is partitioned by remote IP
  per minute and registration per remote IP per hour, with zero queued requests.
  This is lightweight, has no external service requirement, and returns `429`.
  Distributed counters are deferred; multi-instance deployers must additionally
  rate-limit at their reverse proxy.
- **Explicit Compose secrets.** Compose variable interpolation will refuse to
  start without a JWT secret and administrator password. `.env.example` documents
  the contract but contains no usable secret. Known production defaults are
  removed; development-only defaults remain in `appsettings.Development.json`.
- **Install curl in the runtime image.** Replacing the health check with shell or
  application-specific probes was considered, but installing the small declared
  dependency is clearest and preserves the existing Docker contract.

## Risks / Trade-offs

- [Existing production deployments rely on default credentials] → Startup becomes
  intentionally breaking for unsafe deployments; operators must set documented
  environment values before upgrading.
- [In-memory throttles are per process] → Document the limitation and recommend a
  reverse-proxy limiter for multi-replica or Internet-facing installations.
- [IP partitioning behind a proxy sees the proxy address] → Keep conservative
  defaults and document trusted forwarded-header configuration as future work;
  do not trust arbitrary forwarded headers in this change.
- [Registration disabled can surprise existing users] → Development remains open,
  Compose exposes an explicit opt-in variable, and the API returns a clear `403`.

## Migration Plan

1. Add and test production bootstrap validation.
2. Wire registration policy and rate-limit middleware.
3. Remove production seed defaults and update Compose, image, and documentation.
4. Before deployment, copy `.env.example` to `.env` and replace every required
   value. Rollback is possible by restoring the prior image; no schema changes are
   made.

## Open Questions

- Distributed authentication throttling and administrator-created invitations are
  deferred to separate changes if multi-instance deployments become common.

