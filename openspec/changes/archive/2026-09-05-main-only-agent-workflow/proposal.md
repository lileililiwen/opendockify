## Why

The repository documentation requires feature branches, pull requests, and
review approval, but this project is maintained by one person and the requested
operating model is direct, sequential development on `main`. The contradiction
causes agents to select the wrong branch and makes the documented workflow
unusable for the owner.

## What Changes

- Make `main` the only normal working branch for this repository.
- Keep OpenSpec-first, one-change-at-a-time sequencing and local verification.
- Define how to integrate and remove an already-existing branch without
  creating another branch or force-pushing.
- Keep PR guidance only for external contributors.

## Capabilities

### New Capabilities

- `main-only-agent-workflow`: normative single-maintainer Git/agent workflow.

## Non-goals

- No remote branch deletion, repository-host settings change, or bypass of
  CI/security checks.
