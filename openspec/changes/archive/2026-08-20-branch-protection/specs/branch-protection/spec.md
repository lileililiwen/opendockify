## ADDED Requirements

### Requirement: The main branch is protected

The system SHALL protect the `main` branch so that changes merge only through
pull requests that pass required checks and review.

#### Scenario: No direct push

- **WHEN** a contributor attempts to push directly to `main`
- **THEN** the push is rejected

#### Scenario: PR required

- **WHEN** a contributor wants to merge a change
- **THEN** it must go through a pull request with at least one approving
  review and a passing required CI check

### Requirement: Contribution flow is documented

The system SHALL provide contribution and pull-request guidance.

#### Scenario: Contribution guide

- **WHEN** a contributor reads the repository documentation
- **THEN** branch naming, commit conventions, CI behavior, and the review
  checklist are documented

#### Scenario: PR template

- **WHEN** a contributor opens a pull request
- **THEN** the template prompts for a summary, test evidence, and a
  quality-gate checklist

### Requirement: Required checks are identified

The system SHALL identify the CI checks required to merge as documented policy
(the `build` job of the CI workflow).

#### Scenario: Required check listed

- **WHEN** a contributor reads the contribution guide
- **THEN** the required CI check(s) are named and the host is configured to
  require them
