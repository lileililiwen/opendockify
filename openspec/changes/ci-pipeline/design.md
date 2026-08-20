## Context

Ported from the reference project's `ci.yml`, adapted to OpenDockify. The
pipeline is deliberately a single `build` job:

1. Checkout (full history, `fetch-depth: 0` — needed for the diff-based
   coverage gate).
2. Setup .NET 8.
3. `dotnet restore` (NuGet audit at restore; see `nuget-audit`).
4. Explicit audit step: `dotnet list package --vulnerable --include-transitive`
   greps for High/Critical and fails the pipeline.
5. `dotnet format --verify-no-changes`.
6. `dotnet build ... -c Release --no-restore /warnaserror`.
7. `dotnet test ... --collect:"XPlat Code Coverage" --settings
   Coverlet.runsettings --results-directory TestResults`.
8. PR-only: `python3 scripts/check_incremental_coverage.py --base origin/main
   --threshold 0.80 --reports 'TestResults/**/coverage.opencover.xml'`.
9. Artifacts: coverage reports always; build logs on failure.

Sonar steps (Begin/Build/End) are included but gated on `SONAR_TOKEN`; they
remain dormant until a deployer configures secrets. This keeps the door open
for Sonar quality gates without coupling the MVP to an external service.

## Goals / Non-Goals

**Goals:**
- A single, understandable CI workflow with strict gates.
- Required-check readiness for branch protection.

**Non-Goals:**
- SonarCloud activation in MVP.
- Weekly quality-report workflow.
- Multi-DB test matrix.

## Decisions

- **One `build` job** — keeps the required check simple; the reference project
  proved this is sufficient.
- **Audit step is belt-and-suspenders** over the restore-time audit: it
  produces a readable failure and covers transitive packages explicitly.
- **Coverage gate PR-only** — there is no diff base for pushes to `main`; the
  script handles a missing base gracefully (skips, exit 0).

## Risks / Trade-offs

- [Risk: CI environment differs from local (SDK version, OS)] → Mitigation:
  `actions/setup-dotnet` pins 8.0.x; a `global.json` can pin the SDK band
  (added in a later change if needed).
- [Risk: Sonar steps add noise when unconfigured] → Mitigation: `if:
  env.SONAR_TOKEN != ''` guards; zero effect until secrets exist.

## Migration Plan

1. Port `.github/workflows/ci.yml`, adapting the solution name.
2. Confirm it depends on artifacts from `coverage-gates` and `nuget-audit`
   (those changes land first or together).
3. Verify locally the exact commands CI runs (restore/audit/format/build/test).

## Open Questions

- Should CI also build the Docker image? Decision: no for MVP — Docker build is
  verified manually and can be a separate workflow later.
