## Context

Branch protection is part policy documentation (committed to the repo) and
part host configuration (applied by a maintainer; cannot be code-committed).
This change writes the documentation and names the required checks; the
maintainer mirrors those settings in the repo host (GitHub / Gitee / CodeBuddy
per deployment).

- `CONTRIBUTING.md` carries: the protected-`main` policy (PR required, 1
  approving review, required `build` check from `ci-pipeline`, no force
  pushes, squash preferred), branch naming, conventional commits, CI
  behavior, the full review checklist, the AI-involvement convention
  (shared with `ai-code-conventions`).
- `PULL_REQUEST_TEMPLATE.md` is the shared PR template (with `ai-code-conventions`).

## Goals / Non-Goals

**Goals:**
- Explicit protected-`main` policy.
- Documented, repeatable contribution flow.
- Named required checks.

**Non-Goals:**
- Automating host settings via branch-protection-as-code.
- Enforcing anything at runtime (CI already enforces format/build/test).

## Decisions

- **Document-then-configure**: the repo owns the policy text; a maintainer
  applies the host settings and points at the CONTRIBUTING section (same
  approach as the reference project).
- **Squash merge preferred** for linear history; merge commits acceptable.

## Risks / Trade-offs

- [Risk: host settings drift from documentation] → Mitigation: the docs are
  the contract; a maintainer change checklist references them.
- [Risk: required checks not configured] → Mitigation: CONTRIBUTING names the
  exact `build` job and instructs asking a maintainer.

## Migration Plan

1. Port `CONTRIBUTING.md`, adapting project names and the required `build`
   check reference.
2. Port `PULL_REQUEST_TEMPLATE.md` (co-authored with `ai-code-conventions`).
3. Verify: docs read consistently; the checklist references existing workflows.

## Open Questions

- Should `main` require Sonar checks once configured? Decision: yes, listed in
  CONTRIBUTING as "add Sonar/audit checks to the required list as their gates
  land".
