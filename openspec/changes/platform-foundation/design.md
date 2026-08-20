## Context

Greenfield project in an empty directory. The architecture is deliberately a
**modular monolith** (mirroring proven .NET patterns): one class library per
business domain, a central `OpenDockify.Data` project that references all
modules and owns `AppDbContext` + migrations, and an `OpenDockify.Api` shell as
the composition root. This keeps the dependency graph acyclic (domain modules
inject the base `DbContext`, never `OpenDockify.Data`) and lets future
capabilities land as new modules with one-line registrations.

Database strategy: SQLite is the default so self-hosters need nothing but the
image/volume. The other providers (PostgreSQL, MySQL/MariaDB, SQL Server) are
selected by configuration through a **provider registry** so a deployer switches
databases by editing two settings and restarting — no recompile. EF Core
migrations live in `OpenDockify.Data`; startup applies them then runs a
guard-aware seeder (`if (db is empty) seed`). Domain-specific seed data (roles,
admin, built-in templates, config defaults) belongs to their respective
capability changes — this change only establishes the mechanism.

Docker: multi-stage build. The sdk stage restores/builds/publishes with
`PublishSingleFile=true` (RID `linux-x64`); the aspnet runtime stage installs
`fonts-noto-cjk` before copying the publish output. This addresses the known
pitfall that QuestPDF Chinese rendering fails without CJK fonts.

## Goals / Non-Goals

**Goals:**
- Buildable, runnable skeleton on .NET 8 (LTS) with 0 warnings/0 errors.
- SQLite default + pluggable providers (Postgres/MySQL/SQL Server); auto-migrate
  + idempotent seed.
- Single-file Docker image with CJK fonts and a healthcheck.
- MIT license + README legal disclaimer.

**Non-Goals:**
- Any business capability (auth, templates, generation, config, AI, esign).
- React frontend (separate repository).
- E-signature logic.
- Deployment orchestration beyond Dockerfile + compose.

## Decisions

- **Modular monolith, not microservices**: single deployable, clear module
  boundaries, low ops cost for self-hosters.
- **.NET 8 (LTS) rather than .NET 9**: the original requirements said .NET 9,
  but .NET 9 is STS (support ends 2026-05) while .NET 8 is LTS (supported to
  2026-11), the APIs used (Minimal APIs, EF Core, JWT) are identical across
  8/9, and the build environment only has the .NET 8 SDK. Specs target
  `net8.0`. Upgrade to .NET 10 (next LTS) is the natural future jump.
- **Provider registry, not scattered switches**: `AddDatabaseModule` reads
  `Database:Provider` (default `sqlite`), and a registry maps the provider name
  to the EF `UseXxx` extension + provider-specific options. Connection string
  from `ConnectionStrings:Default`; SQLite default
  `Data Source=/app/data/opendockify.db`.
  - `sqlite` → `UseSqlite` (always-referenced package).
  - `postgres` → `UseNpgsql` + `EnableRetryOnFailure` (Npgsql package).
  - `mysql` → `UseMySql` with `ServerVersion.AutoDetect` (Pomelo package).
  - `sqlserver` → `UseSqlServer` (Microsoft package).
  - Unknown name → startup error listing the supported set.
  All four provider packages are referenced from `OpenDockify.Data`, so the
  switch is runtime-only; all providers use `MigrationsAssembly("OpenDockify.Data")`.
- **Migrations + seed at startup**: self-hosters should not need to run
  `dotnet ef` manually. Seeder is idempotent (no duplicate seed rows).
- **Provider-neutral queries**: domain code uses only portable LINQ (no
  provider-specific SQL/JSON functions at this stage). Where cross-provider
  semantics differ (case sensitivity, decimal handling), the `system-config`
  layer owns any normalization.
- **Single-file publish + CJK fonts in image**: satisfies the Docker pitfall
  noted in the project context; uses `fonts-noto-cjk` for broad CJK coverage.
- **Directory.Build.props sets Nullable=enable, TreatWarningsAsErrors,
  AnalysisLevel=latest-recommended** — mirrors the "serious code" standard.

## Risks / Trade-offs

- [Risk: EF migrations auto-applied on startup can conflict under concurrent
  multi-instance deployments] → Mitigation: document that SQLite default is
  single-instance; note in README. Acceptable for MVP self-hosted usage.
- [Risk: provider-specific behavior differences (case sensitivity, decimal
  precision, JSON handling) surprise deployers] → Mitigation: README documents
  the differences and recommends SQLite/Postgres as the tested paths; provider
  matrix covered in CI smoke tests where feasible.
- [Risk: MySQL ServerVersion auto-detection fails on locked-down hosts] →
  Mitigation: allow explicit `Database:MySqlServerVersion` override; document it.
- [Risk: fonts-noto-cjk increases image size] → Trade-off accepted; correct
  Chinese PDF rendering is a hard requirement.
- [Risk: TreatWarningsAsErrors slows initial scaffolding] → Accepted; keeps the
  build gate meaningful per §3.4 of Agents.md.

## Migration Plan

1. Create solution + projects in the modular layout; set project references.
2. Add `Directory.Build.props`, `.editorconfig`, `.gitignore`.
3. Implement `AppDbContext` (empty at this stage; domain entities come later),
   the provider registry (`UseSqlite`/`UseNpgsql`/`UseMySql`/`UseSqlServer`
   selected by `Database:Provider`), auto-migrate + seed guard.
4. Add `LICENSE` (MIT) and `README.md` with the legal disclaimer AND a
   "Database providers" section documenting the four providers, the settings to
   change, and per-provider caveats.
5. Add `Dockerfile` (multi-stage, single-file, CJK fonts) + `docker-compose.yml`.
6. Add CI workflow that runs `dotnet build OpenDockify.sln` (and a smoke job
   against SQLite; Postgres/MySQL matrix optional).
7. Verify: `dotnet build` = 0/0; `dotnet run` starts and creates/updates the
   SQLite file; switch to Postgres/MySQL/SQL Server via config and confirm
   startup + migration apply; `docker build` succeeds and image contains fonts.

## Open Questions

- Which exact SQLite file path / volume mount should be documented? Decision:
  `/app/data/opendockify.db` via volume, overrideable with env
  `ConnectionStrings:Default`.
- Should the CI matrix include all providers? Decision: build only for MVP,
  plus a SQLite smoke job; a full provider matrix (Postgres/MySQL/SQL Server
  containers) can be added as a follow-up once the database capability is
  implemented.
