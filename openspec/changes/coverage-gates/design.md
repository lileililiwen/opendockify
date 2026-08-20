## Context

Ported from the reference project. Three artifacts:

1. **`tests/OpenDockify.UnitTests`** — xUnit test host. At this stage it's a
   minimal project (the `finance-conversion` change adds the first real tests).
2. **`Coverlet.runsettings`** — XPlat Code Coverage collector, OpenCover format.
   The format matters: the incremental gate script parses OpenCover XML
   (`SequencePoint` + `File` elements), and Sonar (later) consumes the same
   format.
3. **`scripts/check_incremental_coverage.py`** — ported verbatim (adapted to
   the solution name). It:
   - diffs `*.cs` against `--base origin/main...HEAD`,
   - parses the OpenCover reports,
   - computes new executable lines (excluded: `/tests/`, `/Migrations/`,
     `/obj/`, `.Designer.cs`, `.g.cs`),
   - fails if new-line coverage < `--threshold` (default 0.80),
   - prints overall coverage for information.

CI integration (running the gate on PRs) belongs to `ci-pipeline`.

## Goals / Non-Goals

**Goals:**
- Measurable unit test coverage for new code.
- Local + CI-consistent gate behavior.

**Non-Goals:**
- Gating legacy coverage.
- Coverage dashboards/upload in MVP.

## Decisions

- **80% new-code threshold**, matching the reference project.
- **Python script over .NET tooling** for the gate — it's a pure diff+XML
  analysis; no extra .NET dependency; works in CI and locally.
- **OpenCover format** chosen once, reused by the gate and future Sonar.

## Risks / Trade-offs

- [Risk: gate false-negatives when report paths differ] → Mitigation: the
  script matches by absolute path and by raw path; CI uses a fixed results
  directory.
- [Risk: no tests yet in MVP] → Mitigation: the gate only fails on PRs with
  new executable lines; scaffold commits with no production lines pass.

## Migration Plan

1. Add `tests/OpenDockify.UnitTests` (xUnit) to the solution.
2. Add `Coverlet.runsettings`.
3. Port `scripts/check_incremental_coverage.py`.
4. Verify: `dotnet test` runs and produces OpenCover; the script reports
   "no new executable lines" on a clean checkout.

## Open Questions

- Should the threshold be configurable per module? Decision: single 80%
   solution-wide threshold for MVP; revisit if needed.
