## ADDED Requirements

### Requirement: Module boundaries are enforced by tests

The system SHALL encode the modular-monolith architecture rules as automated
tests that run in CI.

#### Scenario: Illegal module reference

- **WHEN** a module references `OpenDockify.Data` or forms a reference cycle
- **THEN** the architecture test suite fails

#### Scenario: Composition root

- **WHEN** a new module is not wired into the `OpenDockify.Api` composition
  root
- **THEN** the architecture test suite fails

### Requirement: Module dependency graph is declared and verified

The system SHALL declare the allowed module dependencies (per Agents.md §2) and
SHALL verify that no module depends on a module outside its allowed set.

#### Scenario: Forbidden dependency detected

- **WHEN** module A depends on module B and B is not in A's declared allowed
  set
- **THEN** the test fails naming the forbidden dependency

#### Scenario: Dependency graph matches documentation

- **WHEN** the module graph matches the declared map
- **THEN** the tests pass

### Requirement: Services depend on the base DbContext only

The system SHALL ensure module services inject the base
`Microsoft.EntityFrameworkCore.DbContext` and never the concrete
`AppDbContext`.

#### Scenario: Concrete DbContext injected

- **WHEN** a module type references `OpenDockify.Data.AppDbContext`
- **THEN** the architecture test fails

### Requirement: Architecture rules are documented as testable

The system SHALL keep the architecture rules in a test fixture aligned with the
documented module graph, and SHALL require the fixture to be updated when a new
module is added.

#### Scenario: New module

- **WHEN** a new module is added
- **THEN** the fixture must be updated to cover it, signaled by a failing
  architecture test
