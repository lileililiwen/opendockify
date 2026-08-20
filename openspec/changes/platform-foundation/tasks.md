## 1. Solution Scaffold

- [x] 1.1 `dotnet new sln -n OpenDockify` and create the projects:
  `src/OpenDockify.Api` (minimal api), `src/OpenDockify.Data`,
  `src/OpenDockify.{Auth,Templates,Generation,Rendering,Finance,AiAssist,Esign,SystemConfig}`
- [x] 1.2 Add all projects to the solution; set references:
  `OpenDockify.Api → all modules`; `OpenDockify.Data → all modules`;
  domain modules reference only what they need, never `OpenDockify.Data`
- [x] 1.3 Add `Directory.Build.props` (Nullable enable,
  `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`,
  `LangVersion=latest`) and `.editorconfig`
- [x] 1.4 Add `.gitignore` (bin/obj/.vs/opendockify.db)

## 2. EF Core Foundation

- [x] 2.1 Add packages to `OpenDockify.Data`:
  `Microsoft.EntityFrameworkCore.Sqlite` (always),
  `Npgsql.EntityFrameworkCore.PostgreSQL`, `Pomelo.EntityFrameworkCore.MySql`,
  `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`
- [x] 2.2 Implement `AppDbContext` (empty `DbSet` set at this stage) and a
  `DatabaseProvider` enum with values `Sqlite, Postgres, MySql, SqlServer`
- [x] 2.3 Implement `DatabaseProviderRegistry` — maps provider name
  (`sqlite`|`postgres`|`mysql`|`sqlserver`) to the EF `UseXxx` extension and
  provider-specific options:
  - `postgres` → `UseNpgsql` + `EnableRetryOnFailure`
  - `mysql` → `UseMySql` + `ServerVersion.AutoDetect` (with
    `Database:MySqlServerVersion` override)
  - `sqlserver` → `UseSqlServer`
  - all providers use `MigrationsAssembly("OpenDockify.Data")`
  - unknown provider name → startup error listing supported names
- [x] 2.4 Implement startup `MigrateAndSeed` — apply migrations then run the
  registered seeders (each idempotent)
- [x] 2.5 Wire `AddDatabaseModule(IServiceCollection, IConfiguration)` in
  `OpenDockify.Api/Program.cs`; default `sqlite`; reads
  `Database:Provider` and `ConnectionStrings:Default` (both env-overridable)

## 3. License, README, Legal Disclaimer

- [x] 3.1 Add `LICENSE` (MIT) with current year + author
- [x] 3.2 Add `README.md` — overview, quick start, SQLite-out-of-box note, a
  "Database providers" section (the four providers, exact settings to change,
  per-provider caveats: case sensitivity, decimal handling, MySQL server
  version override), Docker usage, AND the full legal disclaimer:
  drafting-tool-only, no legal services, no guarantee of legal validity,
  predatory-lending/usury prohibited, consult a lawyer

## 4. Docker + CI

- [x] 4.1 Add multi-stage `Dockerfile`: sdk build →
  `dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true`
  → aspnet runtime stage with `apt-get install -y fonts-noto-cjk`; expose port;
  volume `/app/data`; healthcheck hitting `/healthz`
- [x] 4.2 Add a `/healthz` endpoint to `OpenDockify.Api`
- [x] 4.3 Add `docker-compose.yml` (service, volume, env passthrough for
  config overrides)
- [x] 4.4 Add `.github/workflows/ci.yml` — `dotnet build OpenDockify.sln`
  (0 warnings/0 errors) on push/PR

## 5. Verify

- [x] 5.1 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
- [x] 5.2 `dotnet run --project src/OpenDockify.Api` → starts, `/healthz` 200,
  SQLite file created (default provider)
- [x] 5.3 Restart against existing SQLite file → seed is a no-op (no errors,
  no duplicate rows)
- [x] 5.4 Provider switch smoke test (PostgreSQL): set
  `Database:Provider=postgres` with a valid `ConnectionStrings:Default` →
  app starts, `__EFMigrationsHistory` created, `/healthz` 200. MySQL/SQL
  Server switch verified via registry unit coverage (no local instances).
- [x] 5.5 Unknown provider (`Database:Provider=oracle`) → startup fails with a
  clear error listing the four supported names
- [ ] 5.6 `docker build` succeeds; `docker run` container has
  `fonts-noto-cjk` installed and `/healthz` responds
