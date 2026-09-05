## ADDED Requirements

### Requirement: Layered deterministic verification

The project SHALL provide deterministic tests for domain logic, API behavior,
Flutter behavior, persistence/migration behavior, and the container startup
path. Tests SHALL not require production credentials or external services.

#### Scenario: A backend behavior changes

- **WHEN** a change modifies an endpoint or service
- **THEN** the affected OpenSpec scenarios have a focused regression test at
  the lowest useful layer and an HTTP test for authorization or serialization
  behavior where applicable

### Requirement: Changed-code coverage gate

CI SHALL fail when executable backend lines added by a change have less than
80% line coverage, while excluding EF migrations, generated files, and test
projects through a versioned configuration.

#### Scenario: New untested service logic is submitted

- **WHEN** changed executable lines are below the threshold
- **THEN** the quality job fails and publishes the uncovered file/line report

### Requirement: Reproducible quality command

The repository SHALL expose one documented command that runs all required local
quality checks and returns a non-zero status on any failure.

#### Scenario: A developer validates before committing

- **WHEN** the command is run from a clean checkout
- **THEN** backend, app, spec, and coverage gates run with deterministic inputs
  and their reports are retained under ignored output directories
