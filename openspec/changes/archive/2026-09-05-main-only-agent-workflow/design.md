## Context

`Agents.md` and `CONTRIBUTING.md` currently describe a protected PR/feature
branch workflow. The local checkout previously had `backup-restore-integrity`
checked out while `main` lagged behind its integration history.

## Decisions

- The agent MUST check out `main` before work and MUST NOT create a new branch.
- Existing branches may be fast-forwarded or merged into `main` only after
  reviewing their commit scope and checking the worktree; obsolete local
  branches are deleted only after successful integration.
- OpenSpec proposal/design/spec/tasks, strict validation, implementation,
  verification, archive, and focused commit remain the required sequence.
- Direct main development does not permit force-pushes or skipped quality gates.

## Migration

Update the two normative docs, validate the new change, fast-forward existing
history onto `main`, and remove the obsolete local branch after verification.
