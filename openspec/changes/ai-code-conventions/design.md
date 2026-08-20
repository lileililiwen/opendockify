## Context

Ported from the reference project. The convention is opt-in and non-blocking:
PR authors mark AI involvement; reviewers apply the checklist for
generated/assisted code. The enforcement is a soft comment, not a gate.

Three artifacts:

1. **`PULL_REQUEST_TEMPLATE.md`** — includes the AI involvement checkbox group
   and, conditionally, the AI review checklist. The template is static Markdown
   (both sections present; the checklist is "only if marked"), same as the
   reference.
2. **`CONTRIBUTING.md`** — documents markers, the checklist, and the policy
   that AI-marked code is reviewed with extra care but never blocked.
3. **`.github/workflows/ai-marker-check.yml`** — on `opened`/`synchronize`,
   computes added lines (`git diff --numstat base...head`) and counts AI
   markers in commit messages + PR body; if `added > 500 && markers == 0`,
   posts a soft-warning comment (deduped). Never fails the pipeline.

## Goals / Non-Goals

**Goals:**
- Record AI involvement consistently.
- Extra review care for AI-generated/assisted code.
- Soft warning for large unmarked diffs.

**Non-Goals:**
- Blocking AI-marked code.
- Detecting AI-generated content automatically.

## Decisions

- **Marker grammar**: `AI: generated` / `AI: assisted` / `AI: none` (exact
  strings) so the workflow can grep deterministically.
- **500-line threshold**, matching the reference project.
- **Non-blocking by design** — the workflow exits 0 regardless; it only posts
  a comment.

## Risks / Trade-offs

- [Risk: marker gamed by authors] → Mitigation: this is a convention with
  reviewer diligence, not an enforcement tool; documented as such.
- [Risk: workflow noise on huge feature PRs] → Mitigation: comment is
  deduped (checked against existing comments).

## Migration Plan

1. Add `PULL_REQUEST_TEMPLATE.md` (adapted from the reference).
2. Add the AI-involvement section to `CONTRIBUTING.md`.
3. Add `.github/workflows/ai-marker-check.yml` (adapted).
4. Verify the workflow logic by locally simulating `git diff --numstat` +
   marker grep on a sample PR.

## Open Questions

- Should markers also be required in commit footers for single-commit PRs?
  Decision: PR description is sufficient; commit footer markers are optional.
