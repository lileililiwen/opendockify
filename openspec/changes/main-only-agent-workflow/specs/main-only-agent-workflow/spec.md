## ADDED Requirements

### Requirement: Main-only agent execution

Agents SHALL work on the existing `main` branch for normal repository changes,
SHALL NOT create feature branches, and SHALL process one OpenSpec change from
proposal through verification, archive, and focused commit before starting the
next.

#### Scenario: An agent starts a requested change

- **WHEN** the checkout is on another local branch and the worktree is clean
- **THEN** the agent switches to `main`, verifies ancestry/status, and performs
  the work there without creating a branch

### Requirement: Safe legacy-branch integration

An existing branch SHALL be integrated only after its scope and ancestry are
reviewed; the integration SHALL avoid force-pushes, preserve unrelated work,
and remove the obsolete local branch only after verification succeeds.

#### Scenario: A legacy feature branch contains intended commits

- **WHEN** its commits are ancestors of or can be fast-forwarded into `main`
- **THEN** `main` receives the commits, required gates run, and the local branch
  is deleted only after the gates pass

### Requirement: Direct-main quality safeguards

Direct commits to `main` SHALL still require OpenSpec validation, applicable
format/build/test gates, security review, and a conventional focused commit.

#### Scenario: A change skips its required gate

- **WHEN** validation or a required test fails
- **THEN** the agent does not archive or claim completion and records the actual
  blocker for the owner
