# platform-foundation Specification

## Purpose
TBD - created by archiving change platform-foundation. Update Purpose after archive.
## Requirements
### Requirement: Solution scaffold

The system SHALL be a .NET 8 (LTS) solution named `OpenDockify.sln` organized as a
modular monolith: `src/OpenDockify.Api` for the Minimal API shell and DI
composition root, plus one class library per business domain under `src/`.

#### Scenario: Domain class libraries exist

- **WHEN** a developer inspects the repository
- **THEN** the solution contains `OpenDockify.Api` and the domain libraries
  `OpenDockify.Auth`, `OpenDockify.Templates`, `OpenDockify.Generation`,
  `OpenDockify.Rendering`, `OpenDockify.Finance`, `OpenDockify.AiAssist`,
  `OpenDockify.Esign`, `OpenDockify.SystemConfig`, and `OpenDockify.Data`

#### Scenario: Dependency direction is enforced

- **WHEN** the solution is built
- **THEN** domain libraries never reference `OpenDockify.Data`, and the build
  fails if a domain library introduces such a reference

### Requirement: SQLite-first persistence

The system SHALL use SQLite as the default database provider so that a
self-hoster can run with zero external database services, and SHALL support
PostgreSQL as an optional provider chosen by configuration.

#### Scenario: Default startup uses SQLite

- **WHEN** the app starts with no database provider configured
- **THEN** a SQLite file database is used and no external DB service is required

#### Scenario: PostgreSQL can be selected

- **WHEN** the app starts with `Database:Provider=Postgres` configured
- **THEN** the app connects to PostgreSQL using the configured connection string

### Requirement: Pluggable database providers

The system SHALL support switching the EF Core database provider by
configuration, covering SQLite, PostgreSQL, MySQL/MariaDB, and SQL Server, with
each provider mapped from a stable provider name (`sqlite`, `postgres`, `mysql`,
`sqlserver`) in the `Database:Provider` setting (environment-overridable), and
the connection string from `ConnectionStrings:Default`. The provider SHALL be
selected once at startup via a registry/factory — no provider-specific code in
domain modules, and no recompile required to switch databases.

#### Scenario: MySQL selected by name

- **WHEN** the app starts with `Database:Provider=mysql` and a valid MySQL
  connection string
- **THEN** the app connects to MySQL/MariaDB and migrations/seeds apply there

#### Scenario: SQL Server selected by name

- **WHEN** the app starts with `Database:Provider=sqlserver` and a valid SQL
  Server connection string
- **THEN** the app connects to SQL Server and migrations/seeds apply there

#### Scenario: Unknown provider fails fast

- **WHEN** `Database:Provider` is set to a value not in the supported set
- **THEN** the app fails to start with a clear error listing the supported
  provider names

#### Scenario: No code change to switch provider

- **WHEN** a deployer changes `Database:Provider` and the connection string
- **THEN** the same binaries run against the new provider without recompiling

#### Scenario: Connection string per provider

- **WHEN** a provider other than SQLite is selected
- **THEN** the connection string from `ConnectionStrings:Default` is used, and
  provider-specific options (e.g. MySQL server version auto-detection, Npgsql
  retry-on-failure) are applied by the provider registry

### Requirement: Automatic migration and seed on startup

The system SHALL apply EF Core migrations automatically at startup and SHALL
run a seeding routine that is a no-op when the database already contains data.

#### Scenario: Fresh database

- **WHEN** the app starts against an empty database
- **THEN** migrations are applied and the seed routine runs

#### Scenario: Existing database

- **WHEN** the app starts against a database that already has data
- **THEN** the seed routine performs no destructive or duplicate writes

### Requirement: Docker image with CJK fonts

The system SHALL ship a Dockerfile that builds a single-file self-contained
publish and installs Chinese (CJK) fonts in the runtime image, because
QuestPDF requires such fonts to render Chinese text and stock images lack them.

#### Scenario: Image contains CJK fonts

- **WHEN** a container is built from the Dockerfile and inspected
- **THEN** a CJK font package (e.g. `fonts-noto-cjk`) is present in the runtime
  image

#### Scenario: Single-file publish

- **WHEN** the Docker image is built
- **THEN** the published app is produced with `PublishSingleFile=true` for the
  configured RID

### Requirement: Build hygiene

The system SHALL build with zero warnings and zero errors, with nullable
reference types enabled and an analysis level configured via
`Directory.Build.props`.

#### Scenario: Clean build

- **WHEN** `dotnet build OpenDockify.sln` is run
- **THEN** the build completes with 0 warnings and 0 errors

### Requirement: MIT license and legal disclaimer

The repository SHALL be licensed under MIT and SHALL contain a README that
states the project is a document drafting tool only, provides no legal services
and no guarantee of legal validity, prohibits use for predatory lending or
usury, and recommends consulting a lawyer for formal contracts.

#### Scenario: README disclaimer present

- **WHEN** a user reads the README
- **THEN** it contains the drafting-tool-only disclaimer, the predatory-lending
  prohibition, and the lawyer recommendation

