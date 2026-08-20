# ci-pipeline Specification

## Purpose
TBD - created by archiving change ci-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Every push and PR is verified by CI

The system SHALL run a CI pipeline on every push to `main` and every pull
request that:
- checks formatting with `dotnet format --verify-no-changes`,
- builds the solution with warnings-as-errors,
- runs the unit and architecture test suites,
- audits NuGet packages for known vulnerabilities.

#### Scenario: Format violation fails

- **WHEN** a change deviates from the configured formatting
- **THEN** CI fails with a format error before build/tests run

#### Scenario: Build warning fails

- **WHEN** a change introduces a compiler or analyzer warning
- **THEN** CI fails on the build step

#### Scenario: Test failure fails

- **WHEN** a unit or architecture test fails
- **THEN** CI reports the failing test and the pipeline is red

#### Scenario: Vulnerability found

- **WHEN** a direct or transitive package has a known High/Critical
  vulnerability
- **THEN** the audit step fails the pipeline

### Requirement: CI results gate merges

The system SHALL surface the CI pipeline result as a required check on pull
requests.

#### Scenario: Required check

- **WHEN** a pull request is considered for merge
- **THEN** the CI `build` job must pass before merge

### Requirement: Incremental coverage runs on PRs

The system SHALL run the incremental new-code coverage gate on pull requests
only (where the base ref exists), failing when new-line coverage is below the
threshold.

#### Scenario: PR below threshold

- **WHEN** a PR adds new executable lines covered below 80%
- **THEN** CI fails on the coverage step

#### Scenario: Push without PR

- **WHEN** CI runs for a push to `main`
- **THEN** the incremental coverage step is skipped (no diff base)

### Requirement: Artifacts on failure

The system SHALL upload coverage reports and build logs as artifacts so
failures are diagnosable.

#### Scenario: Artifacts uploaded

- **WHEN** a CI run completes (success or failure)
- **THEN** coverage reports (and build logs on failure) are uploaded

