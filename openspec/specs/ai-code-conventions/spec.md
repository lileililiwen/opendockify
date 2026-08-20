# ai-code-conventions Specification

## Purpose
TBD - created by archiving change ai-code-conventions. Update Purpose after archive.
## Requirements
### Requirement: AI involvement is recorded

The system SHALL record whether a change is AI-generated, AI-assisted, or
human-authored in the pull request.

#### Scenario: Marker present

- **WHEN** a contributor opens a pull request
- **THEN** the PR records AI involvement (generated, assisted, or none)

### Requirement: AI-marked code is reviewed with extra care

The system SHALL require an AI-specific review checklist for generated or
assisted code.

#### Scenario: AI review checklist

- **WHEN** a PR is marked generated or assisted
- **THEN** the review confirms spec compliance, authorization/ownership checks,
  injection safety, test coverage or a stated reason, and no dead code

### Requirement: Large unmarked diffs get a soft warning

The system SHALL post a non-blocking warning comment when a pull request adds a
large amount of code without any AI involvement marker.

#### Scenario: Large unmarked diff

- **WHEN** a PR adds 500+ lines with no AI marker
- **THEN** a soft warning comment is posted (non-blocking)

#### Scenario: Marked large diff

- **WHEN** a PR adds 500+ lines and carries an AI marker
- **THEN** no warning comment is posted

#### Scenario: Small diff

- **WHEN** a PR adds fewer than 500 lines with no AI marker
- **THEN** no warning comment is posted

### Requirement: Convention is documented

The system SHALL document the AI-involvement convention and the review
checklist in CONTRIBUTING.

#### Scenario: Documentation present

- **WHEN** a contributor reads the contribution guide
- **THEN** the markers and the AI review checklist are documented

