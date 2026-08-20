## Why

AI-generated and AI-assisted code is now a normal part of contributions.
Recording AI involvement on pull requests and applying an AI-specific review
checklist ensures generated code is scrutinized for spec compliance, security,
and test coverage — especially important in a legal-document system where
amounts, IDs, and clause logic must be exact. This mirrors the reference
project's convention.

## What Changes

- `PULL_REQUEST_TEMPLATE.md` with an **AI involvement** section
  (`AI: generated` / `AI: assisted` / `AI: none`) and an **AI review
  checklist** that applies when generated/assisted is marked.
- `CONTRIBUTING.md` documents the markers, the checklist, and that AI-marked
  code is not blocked — only reviewed with extra care.
- `.github/workflows/ai-marker-check.yml`: non-blocking workflow that posts a
  soft-warning comment when a PR adds 500+ lines without an AI marker. It never
  fails the pipeline.
- AI review checklist covers: spec compliance with the OpenSpec change,
  ownership/authorization on mutating paths, no injection/secret regressions,
  unit tests (or stated reason), no dead code, license/attribution for copied
  fragments.

## Capabilities

### New Capabilities

- `ai-code-conventions`: AI-involvement markers in PRs, AI review checklist,
  and a non-blocking large-diff soft-warning workflow.

### Modified Capabilities

None.

## Non-goals

- No blocking of AI-marked or large PRs.
- No automated AI-content detection beyond the marker convention.

## Impact

- `PULL_REQUEST_TEMPLATE.md`, `CONTRIBUTING.md`, `.github/workflows/ai-marker-check.yml`.
- Review process documentation only; no runtime behavior.
