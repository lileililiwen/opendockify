## ADDED Requirements

### Requirement: Local Git hooks run on commit and push

The system SHALL install Husky.Net hooks automatically so that commits fail on
formatting drift and pushes fail on build errors.

#### Scenario: Automatic install

- **WHEN** a contributor restores the project on a fresh clone
- **THEN** the Husky hooks are installed without manual setup

#### Scenario: Commit blocked on format

- **WHEN** a contributor commits code that deviates from the configured
  formatting
- **THEN** the commit is blocked with a format error

#### Scenario: Push blocked on build

- **WHEN** a contributor pushes code that does not build cleanly under
  warnings-as-errors
- **THEN** the push is blocked with a build error

### Requirement: Hooks are skippable and CI stays authoritative

The system SHALL let a contributor bypass hooks for a single command
(`--no-verify`) or disable them entirely (`HUSKY=0`), and SHALL treat CI as the
authoritative gate.

#### Scenario: Skip hooks

- **WHEN** a contributor uses `git commit --no-verify` or sets `HUSKY=0`
- **THEN** the commit/push proceeds without the local checks, and CI still
  enforces the gates on the pushed branch
