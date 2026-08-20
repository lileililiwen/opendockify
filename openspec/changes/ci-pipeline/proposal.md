## Why

Every push and pull request must be verified automatically: formatting, build
(0 warnings/errors), unit + architecture tests, and the NuGet vulnerability
audit. The reference project's `ci.yml` is a proven, minimal GitHub Actions
workflow; this change adapts it to OpenDockify, keeping Sonar steps dormant
until secrets are configured (out of MVP).

## What Changes

- `.github/workflows/ci.yml` on push to `main` and every PR:
  - `dotnet restore` (NuGet audit runs at restore — see `nuget-audit`);
  - explicit `dotnet list package --vulnerable --include-transitive` audit step
    that fails on High/Critical;
  - `dotnet format --verify-no-changes` (format gate);
  - `dotnet build OpenDockify.sln -c Release --no-restore /warnaserror`;
  - `dotnet test OpenDockify.sln -c Release --no-build` with OpenCover
    collector + `Coverlet.runsettings`;
  - on PRs only: `python3 scripts/check_incremental_coverage.py` (80% new-code
    gate, from `coverage-gates`);
  - artifact upload of coverage reports and build logs on failure.
- Sonar steps present but gated on `SONAR_TOKEN` being configured; they stay
  inactive until a deployer adds secrets (documented in CONTRIBUTING).

## Capabilities

### New Capabilities

- `ci-pipeline`: CI on push/PR with format, build, test, audit, and
  incremental coverage gates.

### Modified Capabilities

None.

## Non-goals

- No SonarCloud activation (secrets not present in MVP).
- No weekly quality-report workflow (deferred).
- No multi-DB CI matrix (can be added later).

## Impact

- `.github/workflows/ci.yml`.
- Relies on `Coverlet.runsettings`, `scripts/check_incremental_coverage.py`
  (coverage-gates), NuGet audit props (nuget-audit).
- The `build` job becomes the required status check (branch-protection).
