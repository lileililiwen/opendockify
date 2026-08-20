## Why

The spec-first workflow depends on a protected `main`: every change merges
through a reviewed pull request that passes required checks. Without explicit
branch protection and a documented contribution flow, direct pushes and
unreviewed merges silently bypass the quality gates.

## What Changes

- `CONTRIBUTING.md` documenting: `main` is protected (PR required, 1 approving
  review, required CI `build` check, no force push, squash preferred); feature
  branch naming (`feat/<change-name>`); conventional commits; CI behavior; the
  review checklist.
- `PULL_REQUEST_TEMPLATE.md` (shared with `ai-code-conventions`) prompts for
  summary, test evidence, quality-gate checklist.
- The repository host settings (applied by a maintainer; documented since
  settings can't be code-committed): require PR, 1 review, required checks,
  no force pushes.

## Capabilities

### New Capabilities

- `branch-protection`: protected `main` policy documentation, contribution
  flow, PR template requirements, required-check configuration.

### Modified Capabilities

None.

## Non-goals

- No automated branch-protection-as-code (that is host-specific; documented
  instead).
- No CI changes (that is `ci-pipeline`).

## Impact

- `CONTRIBUTING.md`, `PULL_REQUEST_TEMPLATE.md`.
- Maintainer applies the documented host settings.
