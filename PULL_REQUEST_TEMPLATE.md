## Summary

<!-- What does this change do, and why? Reference the OpenSpec change(s) it implements. -->

## OpenSpec change

<!-- e.g. openspec/changes/<name> (archives to openspec/specs/<cap>/spec.md) -->

## AI involvement

- [ ] `AI: generated` — blocks written by an AI with minor human edits
- [ ] `AI: assisted` — human-written with AI suggestions/refactors
- [ ] `AI: none` — human-authored

If generated or assisted, the AI review checklist below also applies.

## Test evidence

<!--
How did you verify this? Include both:
- Automated: what the CI workflow ran and its result
- Manual: the HTTP smoke tests you exercised (happy path AND negative cases), per Agents.md §4.3
-->

## Quality-gate checklist

- [ ] OpenSpec change exists with all four artifacts (proposal / spec / design / tasks) complete and validated
- [ ] `dotnet format --verify-no-changes` passes
- [ ] Build is clean: 0 warnings / 0 errors under `/warnaserror`
- [ ] Tests pass
- [ ] Spec scenarios verified end-to-end (happy path and negative cases)
- [ ] No stubs, `TODO`s, or `NotImplementedException`
- [ ] Server-side validation and authorization on every mutating endpoint
- [ ] No changes outside the change's scope (composition-root one-liners excepted)
- [ ] Commit message is conventional and explains the *why*

## AI review checklist (only if marked generated/assisted)

- [ ] Code matches its OpenSpec change (spec compliance)
- [ ] Ownership/authorization checks on every mutating path; no injection or secret regressions
- [ ] Unit tests exist for new logic, or a stated reason why not
- [ ] No dead code, unused branches, or leftover scaffolding
- [ ] License/attribution for any copied fragments (iText7 AGPL, QuestPDF MIT)

## Notes for the reviewer

<!-- Anything the reviewer should pay attention to (trade-offs, follow-ups). -->
