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
- **Document generation** — secure local drafts survive interruptions; preview
  validates and renders without saving a document, while explicit finalization
  exports the PDF and keeps an immutable history (re-editing creates a new
  record). The document library supports titles, template-aware search,
  active/archived filters, deterministic sorting, and connected version history.
- **Guided interviews** — templates may define bounded conditional question
  flows. Answers are validated and saved server-side in private expiring
  sessions, can be resumed after interruption, and use the same
  preview/finalization validation as the standard form.
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
| Frontend | Flutter (Android, Web, and desktop targets) |

## Quick start (Docker, SQLite out of the box)

```bash
cp .env.example .env
# Edit .env and replace the JWT and administrator password placeholders.
docker compose up -d
# Open http://localhost:8080  (API on /healthz)
```

No external database is required — OpenDockify uses a SQLite file stored in the
Docker volume. Compose intentionally refuses to start until
`OPENDOCKIFY_JWT_SECRET` and `OPENDOCKIFY_ADMIN_PASSWORD` are set to safe values.
Public registration defaults to disabled in Docker; explicitly set
`OPENDOCKIFY_ALLOW_REGISTRATION=true` only when self-registration is intended.

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

## AI assist (optional)

The AI polish endpoints (`/api/ai/polish-clause`, `/api/ai/polish-document`)
are **disabled by default** (`Ai.Enabled=false`). They can polish a user-drafted
clause or the rendered document prose while mechanically preserving every
mandatory field value (placeholders are re-substituted server-side; fabricated
amounts/IDs are stripped by the guard).

### ⚠️ Privacy warning

AI polish is **best-effort and never legally binding**. Before enabling it:

- **Do not transmit ID numbers, names, or other personal data to public LLM
  endpoints.** The system redacts stored logs, but the text you submit is sent
  to whatever endpoint you configure.
- **Prefer a local Ollama server for private deployments** so no data leaves
  your host. Any OpenAI-compatible chat-completions endpoint works.

### Configuration (system settings, admin API)

| Setting | Meaning |
|---|---|
| `Ai.Enabled` | `true`/`false`; master gate for all AI endpoints |
| `Ai.Endpoint` | base URL, e.g. `https://api.openai.com/v1` or `http://localhost:11434/v1` (Ollama) |
| `Ai.ApiKey` | Bearer key (secret, never logged). Empty works for local Ollama |
| `Ai.Model` | model name, e.g. `gpt-4o-mini` or `llama3` |
| `Ai.TimeoutSeconds` | request timeout (default 30) |
| `Ai.RateLimitPerDay` | optional per-user daily cap (`0` = off) |

Each can also be set via its environment variable (`AI_ENABLED`,
`AI_ENDPOINT`, `AI_API_KEY`, `AI_MODEL`, `AI_TIMEOUT_SECONDS`,
`AI_RATE_LIMIT_PER_DAY`). To disable AI again, set `Ai.Enabled=false`; every
AI endpoint then returns `403` and never makes an external call.

## E-signature (reserved, not implemented)

E-signature is **out of MVP scope**. The database schema reserves the seams
(`Documents.SigningStatus`, `Signers`, `SigningAuditLogs`) and an
`ISigningOrchestrator` interface skeleton exists, but **no signing behavior is
implemented**: there are no signing endpoints, no certificates are issued, and
no legally reliable timestamps are produced.

For legally reliable signatures the **deployer must integrate an external CA
and timestamping service** (certificate issuance, RFC 3161 timestamps). This
project only orchestrates the workflow and never acts as a certificate
authority.

## Security notes

- **JWT secret** — the signing secret `Jwt:Secret` MUST be at least 32 bytes
  long and MUST be overridden in production (the value in
  `appsettings.Development.json` is a local-development placeholder). Startup
  fails with a clear error if it is missing or too short.
- **Authentication rate limiting** — the API applies fixed-window, per-client
  limits to login and registration. Configure them with
  `Auth__LoginAttemptsPerMinute` and `Auth__RegistrationAttemptsPerHour`.
  Multi-replica or Internet-facing installations SHOULD also enforce a shared
  limit at a trusted reverse proxy.
- **Registration** — public registration is controlled by
  `Auth__AllowRegistration`; it is disabled by default outside local
  development and in the supplied Compose deployment.
- **Initial admin** — production startup requires `Seed:AdminUsername` and a
  non-default `Seed:AdminPassword` of at least 12 characters. The local-only
  `admin` / `admin123` credentials exist only in Development configuration.

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
