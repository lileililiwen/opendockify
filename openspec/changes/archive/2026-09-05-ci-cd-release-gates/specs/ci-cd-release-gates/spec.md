## ADDED Requirements

### Requirement: Complete source and app CI gate

CI SHALL validate OpenSpec changes, C# formatting/build/tests, Flutter
formatting/analyze/tests, and supported web/Linux build targets on every push
to `main` and on pull requests from external contributors.

#### Scenario: A Dart analyzer error is committed

- **WHEN** the workflow runs for the commit
- **THEN** the required quality job fails before a release artifact is marked
  publishable

### Requirement: Container startup smoke gate

CI SHALL build the declared Docker image, start it with non-default safe test
credentials and isolated SQLite storage, poll `/healthz`, and verify migration
and seed completion before the image is accepted.

#### Scenario: A migration breaks startup

- **WHEN** the container cannot become healthy or seed the database
- **THEN** the container job fails and uploads application/container logs

### Requirement: Reproducible diagnostics

Every failed required job SHALL expose enough version, command, test, coverage,
and build-log information to reproduce the failure without exposing secrets.

#### Scenario: A quality job fails on CI only

- **WHEN** a required job exits non-zero
- **THEN** its reports and tool versions are available as retained artifacts
  while secret values are masked and absent from logs
