## Why

The "serious code" standard in Agents.md requires every feature to be
exercised, not just compiled. Unit tests + a coverage gate make that measurable
in CI. The reference project's approach — incremental new-code coverage gating
(80%) while overall/legacy coverage is reported but not gating — keeps quality
high without punishing pre-existing gaps.

## What Changes

- `tests/OpenDockify.UnitTests` (xUnit) as the home for unit tests of core
  services (Finance conversion, template validator/renderer, etc.).
- `Coverlet.runsettings` at the repo root: XPlat code coverage collector,
  OpenCover output format (consumed by CI and the incremental gate).
- `scripts/check_incremental_coverage.py` (ported): on PRs, computes the
  executable lines added by the diff and fails when new-line coverage < 80%.
  Test projects, EF migrations, and generated code are excluded; overall
  coverage is informational only.
- CI wiring happens in the `ci-pipeline` change; this change establishes the
  test project, the collector config, and the script.

## Capabilities

### New Capabilities

- `coverage-gates`: unit test project, coverage collector config, incremental
  new-code coverage gate script (80% threshold, excluded paths).

### Modified Capabilities

None.

## Non-goals

- No coverage threshold on legacy code (report-only).
- No coverage upload to external dashboards (that is Sonar/quality-report
  territory, out of MVP).
- No test framework beyond xUnit.

## Impact

- New `tests/OpenDockify.UnitTests` project in the solution.
- `Coverlet.runsettings` at repo root.
- `scripts/check_incremental_coverage.py`.
- CI (ci-pipeline change) runs the tests and the incremental gate.
