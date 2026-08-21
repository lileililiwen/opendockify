## ADDED Requirements

### Requirement: Production security configuration validation

The system MUST validate production JWT and administrator bootstrap credentials
before applying migrations, seeding data, or accepting HTTP traffic, and MUST fail
startup with actionable errors when configuration is missing or unsafe.

#### Scenario: Secure production configuration accepted

- **WHEN** production starts with a JWT secret of at least 32 UTF-8 bytes, a
  non-empty administrator username, and an administrator password of at least 12
  characters that is not the documented development password
- **THEN** security bootstrap validation succeeds and normal startup continues

#### Scenario: Missing production secret rejected

- **WHEN** production starts without `Jwt:Secret`
- **THEN** startup fails before database migration with an error naming
  `Jwt:Secret` and its minimum length

#### Scenario: Insecure administrator password rejected

- **WHEN** production starts with a missing, short, or known development
  administrator password
- **THEN** startup fails before database migration with an actionable seed-password
  error

#### Scenario: Development defaults remain usable

- **WHEN** the Development environment starts with the documented local-only
  credentials
- **THEN** production-only bootstrap validation does not reject them

### Requirement: Secure container bootstrap

The Docker Compose deployment MUST require operator-provided security secrets,
MUST disable public registration unless explicitly enabled, and MUST provide a
working runtime health check.

#### Scenario: Missing Compose secrets rejected

- **WHEN** an operator starts Docker Compose without the required JWT secret or
  administrator password variables
- **THEN** Compose stops with a configuration error instead of starting with known
  credentials

#### Scenario: Container becomes healthy

- **WHEN** a correctly configured container starts and `/healthz` returns success
- **THEN** the declared container health check can execute and reports the
  container healthy

#### Scenario: Example configuration contains no usable secrets

- **WHEN** an operator reads the example environment file
- **THEN** it documents every required value without providing a production-ready
  JWT secret or administrator password

