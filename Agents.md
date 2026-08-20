# Agents.md

> This document is the contract for AI agents (and humans) working on
> the OpenDockify codebase. It is **normative**: every principle here
> MUST be followed unless explicitly overridden by a written decision
> in an OpenSpec change.

---

## 1. What is OpenDockify?

OpenDockify is an open-source, MIT-licensed, **self-hosted document /
contract drafting system** built with C# / .NET 8 (LTS) Minimal APIs and EF Core
(SQLite by default; PostgreSQL, MySQL/MariaDB, and SQL Server switchable via
`Database:Provider` config — see `platform-foundation`). It generates complete
legal-style documents (loan IOUs, lease agreements, NDAs, etc.) from
database-stored JSON templates, form input, selectable optional clauses, and
PDF export. A "Loan IOU" module is a key feature.

**Positioning — strict non-negotiable constraints:**
- **Self-hosted only.** No public SaaS, no cloud services, no collection of
  private user data, no embedded third-party paid API keys. The deployer runs
  their own instance via Docker.
- **Legal disclaimer is mandatory.** This is a *document drafting tool only*;
  it does not provide legal services or guarantee legal validity. Use for
  predatory lending (usury / "loan sharking") is prohibited. README + in-app
  risk notice must say so.
- **E-signature is OUT of MVP scope.** Only reserved entities (signing status,
  signers, audit log) and interface skeletons exist. For legally reliable
  signing the deployer must integrate external CA + timestamping services; this
  project only orchestrates the workflow, never issues certificates.

**Status:** spec-first bootstrapping. OpenSpec changes are being authored;
no implementation yet.

---

## 2. Architecture — Modular Monolith

OpenDockify is a .NET 8 (LTS) solution organized as a **modular monolith**: one class
library per business domain. A new domain/capability lives in a new package and
MUST NOT require edits across unrelated code.

```
src/
├── OpenDockify.Auth/                # User, roles, JWT, AccountService
├── OpenDockify.Templates/           # Template entity, JSON definition model, engine, built-ins
├── OpenDockify.Generation/          # Fill -> validate -> render -> persist workflow
├── OpenDockify.Rendering/           # QuestPDF renderer, iText7 overlay, font loading
├── OpenDockify.Finance/             # RMB uppercase conversion, LPR/interest validation
├── OpenDockify.AiAssist/            # LLM polish (toggleable), prompt constraints, usage log
├── OpenDockify.Esign/               # RESERVED: signer/status/audit skeletons only
├── OpenDockify.SystemConfig/        # global config table, env overrides
├── OpenDockify.Data/                # central AppDbContext, migrations, seeding
└── OpenDockify.Api/                 # Minimal API shell + DI composition root
```

### 2.1 Adding a new domain (the fixed pattern)

1. Create `src/OpenDockify.<Domain>` class library; add to the solution;
   reference the modules it needs (never `OpenDockify.Data`).
2. `Models/` — entities. `Configuration/` — one `IEntityTypeConfiguration<T>`
   per entity. `Services/` — services injecting the **base `DbContext`** and
   using `Set<T>()`. `<Domain>ModuleExtensions.cs` — an
   `AddXxxModule(IServiceCollection)` extension.
3. In `OpenDockify.Data/AppDbContext.cs`: add `DbSet`s and ONE line
   `builder.ApplyConfigurationsFromAssembly(typeof(<Entity>).Assembly);`
   (zero other edits).
4. In `OpenDockify.Api/Program.cs`: one line
   `builder.Services.AddXxxModule();`.
5. Create an EF migration:
   `dotnet ef migrations add <Name> --project src/OpenDockify.Data --startup-project src/OpenDockify.Api`

**Dependency rules (enforced by the build):**
- Modules MUST NOT reference `OpenDockify.Data` (the Data project references
  all modules; this keeps the graph acyclic). Services depend on the base
  `Microsoft.EntityFrameworkCore.DbContext`.
- Cross-module navigation collections are avoided; queries go through module
  services.
- `User` carries no navigation collections to templates/documents.
- Multi-user isolation is a hard rule: every query filters by the current
  user's id (or by global/system scope for admin templates).

---

## 3. Spec-First Development — the Fixed Workflow

**Every change to OpenDockify goes through OpenSpec BEFORE any code is
written.** This is a fixed workflow — there is no other path.

```
propose  →  validate  →  implement (apply)  →  archive  →  spec is source of truth
```

### 3.1 The OpenSpec workflow

| Phase | What happens | Output |
|---|---|---|
| `propose` | `openspec new change <name>` creates the change folder; then author the **four canonical docs** (below) | `openspec/changes/<name>/` |
| `validate` | `openspec status --change <name> --json` shows artifact status; every artifact must be `done` before implementation | pass / fail |
| `apply` | Implement `tasks.md` in order with serious, production-quality code; mark every checkbox complete | code |
| `archive` | `openspec archive <name> -y` | delta folded into `openspec/specs/<cap>/spec.md`; change moved to `openspec/changes/archive/<YYYY-MM-DD>-<name>/` |

### 3.2 The four canonical docs

Every change MUST contain exactly these artifacts:

```
openspec/changes/<name>/
├── proposal.md                 # WHY: motivation, what changes, capabilities, impact
├── specs/<cap>/spec.md         # WHAT: "## ADDED Requirements" with SHALL/MUST +
│                               #   "#### Scenario:" blocks (exactly 4 hashtags)
├── design.md                   # HOW: context, decisions with rationale, risks, migration
└── tasks.md                    # checkbox implementation list, grouped "## N." headings
```

- Use SHALL/MUST for normative requirements; every requirement needs at least
  one scenario with **WHEN / THEN**.
- If an existing capability's behavior changes, include a
  `specs/<existing-cap>/spec.md` with `## MODIFIED Requirements` (copy the FULL
  requirement block from `openspec/specs/<cap>/spec.md` and edit it).
- The **source of truth** for any capability is `openspec/specs/<cap>/spec.md`.
  Code that drifts from it is a bug.

### 3.3 Sequential processing rule

When two or more changes are pending, implement them one at a time, in the
order listed in [§7 Roadmap](#7-current-state--roadmap). A change is finished
only when it is implemented AND archived AND merged to `main` via a reviewed
pull request. Only then may the next change be started. Never interleave or
partially complete multiple changes.

### 3.4 "Serious code" standard

Implementation is expected to be **production-quality, not stubs**:

- Real, working code with correct behavior per the spec scenarios — no `TODO`,
  no placeholder returns, no `NotImplementedException`. The ONLY exception is
  the `OpenDockify.Esign` module, which is deliberately scoped to reserved
  entities + interface skeletons (its spec says so).
- Follow existing conventions: the module pattern in §2, Minimal API endpoint
  groups, ownership checks on every mutating endpoint, server-side validation
  (never trust the client), dual frontend+backend XSS/validation.
- Security care: JWT authorization on every restricted endpoint, ownership
  filters (`currentUserId != owner` → 404/403), antiforgery not needed for
  token-based APIs but rate-limit login, no secrets in logs/URLs, JSON template
  parse validation (no malicious JSON injection), input length limits.
- Licensing reminders in code comments wherever iText7 is used (AGPL) vs
  QuestPDF (MIT).
- Every feature MUST be exercised end-to-end before declaring done (see §4.3)
  — not just compiled.

---

## 4. Build, Run & Verify

### 4.1 Commands

```bash
dotnet build OpenDockify.sln                # MUST finish with 0 warnings / 0 errors
dotnet run --project src/OpenDockify.Api    # app on configured port (SQLite: no setup)
dotnet ef migrations add <Name> --project src/OpenDockify.Data --startup-project src/OpenDockify.Api
dotnet ef database update --project src/OpenDockify.Data --startup-project src/OpenDockify.Api
```

> Environment note: if `dotnet` is not on PATH, prefix commands with
> `export PATH="$HOME/.dotnet:$PATH"` and
> `export PATH="$HOME/.dotnet/tools:$PATH"`.

### 4.2 Database

- **SQLite (default):** file-based, zero external service. Connection string in
  `src/OpenDockify.Api/appsettings.json`. Migrations apply automatically on
  startup; a seeder creates roles, admin user, system config defaults, and the
  8 built-in templates when the DB is empty.
- **PostgreSQL / MySQL-MariaDB / SQL Server (optional):** selected via
  config/env (`Database:Provider=postgres|mysql|sqlserver`,
  `ConnectionStrings:Default`). Provider switching requires no recompile — the
  `OpenDockify.Data` provider registry maps the name to the EF `UseXxx`
  extension. MySQL server-version override:
  `Database:MySqlServerVersion`.

### 4.3 Verification standard

- Build clean, app starts, migration applies, seed runs (SQLite out of the
  box — no external DB).
- Exercise each spec scenario via HTTP against the API using curl. Get a JWT by
  POSTing to `/api/auth/login`, then use `Authorization: Bearer <token>`.
- Verify role gating (Regular User vs Administrator), multi-user isolation
  (user A cannot see user B's templates/documents), the happy path AND the
  negative scenarios in the spec (missing fields, invalid amounts, out-of-range
  interest rates, non-owners, clause toggles).

---

## 5. Agent Workflow Checklist

When asked to implement a feature or spec:

1. **Read** the change folder: `proposal.md`, `specs/<cap>/spec.md`,
   `design.md`, `tasks.md` — these are authoritative. Also load the relevant
   source-of-truth specs in `openspec/specs/` (the change is a *delta* on top
   of them).
2. **Check the roadmap order** (§7). Implement pending changes one at a time in
   listed order; never jump ahead.
3. **Explore & reuse** *(mandatory)* — before planning or coding, search the
   tree for existing entities, services, endpoint groups, and patterns that
   already satisfy the requirement. In `design.md`, name what you will reuse.
   Do not re-implement what already exists.
4. **Plan** by walking `tasks.md` top-to-bottom.
5. **Implement** in the module pattern (§2): entities → configs → services →
   DI registration → migration → API endpoints.
6. **Smoke-test** every scenario at the HTTP layer (§4.3) before declaring
   done.
7. **Build** — `dotnet build OpenDockify.sln` MUST be 0 warnings / 0 errors.
8. **Update** `tasks.md` — every box checked.
9. **Archive** — `openspec archive <name> -y` (folds deltas into
   `openspec/specs/`, moves the change to `openspec/changes/archive/`).
10. **Commit & land via PR** — commit with a conventional message (short title,
    blank line, detailed body explaining *why* and *what*), on a **feature
    branch** named after the change, then open a pull request that passes the
    required CI checks and one approving review before merging into `main`.

> **Global-view rule:** if you cannot point at the existing module or utility
> your change depends on, stop and explore before writing code. Confidently
> inventing an API that already exists elsewhere is the most expensive failure
> mode.

---

## 6. Anti-Patterns (do not do these)

- **Don't** write code before the spec change exists and is `validate`-ready.
  Spec-first is a fixed workflow.
- **Don't** implement two changes at once, or skip the roadmap order.
- **Don't** edit files outside your domain module unless the composition root
  needs a one-line change (`AppDbContext` scanning line, `Program.cs` module
  line).
- **Don't** create a circular reference: modules never reference
  `OpenDockify.Data`; services inject the base `DbContext`.
- **Don't** leave stubs, `TODO`s, or unhandled error paths in shipped code —
  except in the deliberately-skeletal `OpenDockify.Esign` module.
- **Don't** trust client input — enforce ownership/roles server-side on every
  endpoint, including forged requests.
- **Don't** skip the negative scenarios (missing fields, invalid amounts,
  non-owners, rate limits) when verifying.
- **Don't** hardcode LLM API keys, or embed third-party paid service keys.
- **Don't** silently remove the legal disclaimer or the predatory-lending
  prohibition.
- **Don't** archive a change whose spec scenarios are not verified by an HTTP
  smoke test.

---

## 7. Current State & Roadmap

### 7.1 Shipped & archived

None yet — the project is at spec-first bootstrapping stage.

### 7.2 Pending changes (implement in this order)

| Order | Change | Capabilities | One-line summary |
|---|---|---|---|
| 1 | `platform-foundation` | platform-foundation | Solution scaffold, modular monolith, pluggable EF Core providers (SQLite/Postgres/MySQL/SQL Server), Docker, single-file publish, MIT, README disclaimer |
| 2 | `editorconfig-and-analyzers` | editorconfig-and-analyzers | Root `.editorconfig` + shared `Directory.Build.props`, SonarAnalyzer, deterministic `dotnet format` |
| 3 | `architecture-enforcement` | architecture-enforcement | ArchUnitNET tests enforcing the modular-monolith boundary rules |
| 4 | `coverage-gates` | coverage-gates | xUnit test project, Coverlet OpenCover collector, incremental 80% new-code gate script |
| 5 | `nuget-audit` | nuget-audit | Restore-time + CI NuGet vulnerability scanning, explicit accept policy |
| 6 | `git-hooks` | git-hooks | Husky.Net local pre-commit (format) + pre-push (build) hooks, auto-install |
| 7 | `ci-pipeline` | ci-pipeline | CI on push/PR: restore, audit, format, build 0/0, tests, incremental coverage |
| 8 | `ai-code-conventions` | ai-code-conventions | AI-involvement PR markers, AI review checklist, non-blocking large-diff warning |
| 9 | `branch-protection` | branch-protection | Protected `main` policy + CONTRIBUTING + PR template |
| 10 | `user-auth` | user-auth | Username/password + JWT, roles Regular User/Administrator, multi-user isolation |
| 11 | `system-config` | system-config | Global config in DB (AI toggle, LLM endpoint/key, LPR values), env overrides |
| 12 | `template-engine` | template-engine | JSON template definition (fields, validation, clauses), `{{var}}` rendering, 8 built-ins, copy-not-edit |
| 13 | `finance-conversion` | finance-conversion | RMB uppercase amount conversion (edge cases), LPR/interest-rate validation |
| 14 | `document-generation` | document-generation, pdf-rendering | Fill → validate → render full text + risk notice → PDF → persist record → download/preview/re-edit/list/delete |
| 15 | `ai-assist` | ai-assist | Optional LLM polish (config-toggleable), strict prompt constraints, usage logging |
| 16 | `esign-extensions` | esign-extensions | RESERVED signing status field, signer entities, audit log; interface skeletons only |

### 7.3 Deferred roadmap

- E-signature implementation (external CA + timestamping integration).
- Template JSON import/export; batch document generation; SSO; advanced
  approval workflows.
- Additional template packs; RBAC / organizational structures.

---

## 8. References

- `openspec/specs/*/spec.md` — source-of-truth capability specs
- `openspec/changes/<name>/` — active changes (proposal/design/specs/tasks)
- `openspec/changes/archive/` — frozen history of shipped changes
- `README.md` — overview, setup, license, legal disclaimer
- `src/OpenDockify.Data/AppDbContext.cs` — module registration
- `src/OpenDockify.Api/Program.cs` — composition root
- `.codebuddy/skills/openspec-*` — OpenSpec workflow skills
