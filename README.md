# OpenDockify

Open-source, MIT-licensed, **self-hosted document / contract drafting system**.

OpenDockify generates complete legal-style documents (loan IOUs, lease
agreements, NDAs, and more) from database-stored JSON templates, form input,
selectable optional clauses, and PDF export. A "Loan IOU" module is a key
feature.

**Self-hosted only.** There is no public SaaS, no cloud service, no collection
of private user data, and no embedded third-party paid service keys. You deploy
it yourself — typically via Docker.

## ⚠️ Legal disclaimer

> **OpenDockify is a document drafting tool only.** It does not provide legal
> services and does not guarantee the legal validity of any document it
> generates. Use of this tool for predatory lending (for example, usury or
> "loan sharking" schemes) is **prohibited**. For formal contracts, consult a
> qualified lawyer.

This disclaimer is also appended as a risk notice on generated documents and
shown in the application UI.

## Features

- **Template engine** — templates stored in the database as JSON definitions:
  typed fields (string / number / currency / date), validation rules, optional
  clauses that can be toggled on or off, and `{{variable_name}}` placeholders.
- **8 built-in templates** — Loan IOU, General IOU, Residential Lease, Mutual
  NDA, Outsourcing Service, Part-time/Labor Agreement, Repayment Confirmation,
  and Simple Demand Letter. Built-ins are read-only; copy them to make private
  editable versions.
- **Document generation** — fill the form → validate → render full text with
  the risk notice → export PDF → keep an immutable history (re-editing creates
  a new record).
- **Chinese finance features** — currency fields render as RMB-uppercase
  (中文大写); annual interest rates are validated against the LPR reference with
  a warning (never blocking).
- **AI assist (optional)** — polish custom clause text or the full document,
  with strict constraints (AI cannot fabricate amounts/IDs or add unselected
  clauses). Disabled by default; the deployer supplies their own LLM endpoint
  and key (OpenAI-compatible or Ollama).
- **Simple auth** — username/password + JWT, roles Regular User and
  Administrator, multi-user isolation.
- **E-signature seam** — not implemented in the MVP; the schema reserves
  signing status, signers, and an audit log. For legally reliable signing the
  deployer must integrate an external CA and timestamping service.

## Technology stack

| Layer | Choice |
|---|---|
| Backend | C# / .NET 8 (LTS), Minimal APIs |
| ORM | EF Core |
| Database | **SQLite** (default, zero setup) · PostgreSQL · MySQL/MariaDB · SQL Server |
| PDF | QuestPDF (MIT); iText7 (AGPL) reserved for future existing-PDF overlays |
| Frontend | React SPA (separate repository) |

## Quick start (Docker, SQLite out of the box)

```bash
docker compose up -d
# Open http://localhost:8080  (API on /healthz)
```

No external database is required — OpenDockify uses a SQLite file stored in the
Docker volume.

## Database providers

SQLite is the default and needs nothing. To use another database, set two
settings (via `appsettings.json` or environment variables) and restart:

| Provider | `Database:Provider` | Notes |
|---|---|---|
| SQLite | `sqlite` | default; file DB, no service |
| PostgreSQL | `postgres` | `Npgsql`; retry-on-failure enabled |
| MySQL / MariaDB | `mysql` | server version auto-detected (`Database:MySqlServerVersion` to override) |
| SQL Server | `sqlserver` | |

```bash
# Docker example: switch to PostgreSQL via env vars
Database__Provider=postgres
ConnectionStrings__Default="Host=db;Database=opendockify;Username=opendockify;Password=change-me"
```

Switching providers requires **no recompile** — the same binaries run against
any of the four providers.

### Per-provider caveats

- **Case sensitivity** of string comparisons differs between providers
  (SQLite/PostgreSQL are case-sensitive for `=` by default, MySQL/SQL Server
  vary by collation). Use provider-agnostic comparisons in code.
- **Decimal precision** — SQLite stores decimals as 64-bit floats; use
  `decimal` sparingly in queries and round at boundaries. Postgres/MySQL/SQL
  Server give exact decimal types.
- Migrations are shared; `dotnet ef migrations add` writes provider-agnostic
  migrations, and each provider applies them on startup.

## Security notes

- **JWT secret** — the signing secret `Jwt:Secret` MUST be at least 32 bytes
  long and MUST be overridden in production (the value in
  `appsettings.Development.json` is a local-development placeholder). Startup
  fails with a clear error if it is missing or too short.
- **Login rate limiting** — registration is open and login attempts are
  currently unthrottled. Self-hosters SHOULD rate-limit `/api/auth/login`
  (and register) to slow credential stuffing, either with ASP.NET Core's
  built-in Rate Limiting middleware or at the reverse proxy (e.g. nginx
  `limit_req`). This is the deployer's responsibility for the MVP.
- **Default admin** — the seeder creates `admin` / `admin123` when the
  database is empty. Override both via `Seed:AdminUsername` /
  `Seed:AdminPassword` (or their env forms) before any real deployment.

## Development

```bash
dotnet build OpenDockify.sln          # MUST be 0 warnings / 0 errors
dotnet run --project src/OpenDockify.Api
```

See `Agents.md` for the normative development workflow (spec-first via
OpenSpec, quality gates, verification standard) and `CONTRIBUTING.md` for the
contribution flow.

## License

MIT — see [LICENSE](LICENSE). QuestPDF is MIT-licensed.

**Note on iText7 (AGPL):** the MVP does not depend on iText7. If future
e-signature work adds iText7 for existing-PDF overlay (e.g. cross-page seals),
the AGPL obligations apply; the dependency can be removed if that feature is
unused. Code comments mark every touch point.
