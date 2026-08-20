## Why

OpenDockify is a greenfield project in an empty directory. Nothing exists yet:
no solution, no repository layout, no CI, no Docker story, no license or legal
disclaimer. Before any capability can be specified or built, the platform
foundation must be established: a .NET 8 (LTS) modular-monolith solution that runs on
SQLite out of the box (no external DB server for self-hosters), can switch to
PostgreSQL, MySQL/MariaDB, or SQL Server by configuration alone, and ships as a
single-file Docker image.

## What Changes

- Scaffold a new .NET 8 (LTS) solution `OpenDockify.sln` in
  `/home/paul/code/opendockify` with the modular-monolith layout:
  `OpenDockify.Api` (Minimal API shell + DI composition root) and one class
  library per business domain (`Auth`, `Templates`, `Generation`, `Rendering`,
  `Finance`, `AiAssist`, `Esign`, `SystemConfig`, `Data`).
- MIT license file, README with the mandatory legal disclaimer (drafting tool
  only; no legal services; no guarantee of legal validity; predatory
  lending/usury prohibited; consult a lawyer for formal contracts).
- EF Core with **SQLite as the default provider** (file-based, zero config) and
  a **pluggable provider registry** that selects SQLite, PostgreSQL, MySQL/
  MariaDB, or SQL Server via `Database:Provider` + `ConnectionStrings:Default`
  — no code change or recompile required to switch databases.
- Dockerfile + docker-compose that publish a single-file image AND pre-install
  Chinese fonts (noto-cjk) — QuestPDF requires embedded/available CJK fonts and
  Docker images typically lack them.
- `.gitignore`, `Directory.Build.props` (analysis level, nullable, warnings-as-errors),
  `.editorconfig`, and a CI workflow (`dotnet build` MUST pass 0 warnings/errors).
- App start behavior: apply EF migrations automatically and run a seed routine
  that is a no-op if the DB already has data (seeding of roles/admin/templates
  comes with the capability changes that need them).

## Capabilities

### New Capabilities

- `platform-foundation`: solution scaffold, modular-monolith layout, EF Core
  pluggable provider registry (SQLite default + PostgreSQL/MySQL/MariaDB/SQL
  Server), automatic migration+seed on start, Docker image with CJK fonts,
  single-file publish, MIT license, legal disclaimer.

### Modified Capabilities

None.

## Non-goals

- No business capabilities yet (auth, templates, generation, etc. are separate
  changes).
- No e-signature implementation (reserved for `esign-extensions`).
- No SaaS/cloud offering; self-hosted only.
- No frontend scaffolding (React lives in a separate decoupled repository).

## Impact

- New repository at `/home/paul/code/opendockify` (currently empty).
- New .NET 8 (LTS) solution; dependencies: ASP.NET Core Minimal APIs, EF Core
  `Microsoft.EntityFrameworkCore.Sqlite` (always), plus optional provider
  packages `Npgsql.EntityFrameworkCore.PostgreSQL`,
  `Pomelo.EntityFrameworkCore.MySql`, `Microsoft.EntityFrameworkCore.SqlServer`
  referenced in `OpenDockify.Data` so all four providers are available without
  recompiling.
- Docker: multi-stage build (sdk → aspnet runtime), font package installation
  (`fonts-noto-cjk`), single-file `dotnet publish -p:PublishSingleFile=true`.
- README carries the full legal disclaimer; LICENSE = MIT.
