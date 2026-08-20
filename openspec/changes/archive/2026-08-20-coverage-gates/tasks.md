## 1. Test project

- [x] 1.1 Create `tests/OpenDockify.UnitTests` (xUnit) with a trivial passing
  test to validate the harness; add to the solution
- [x] 1.2 Add `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`
  (pinned versions)

## 2. Coverage collector

- [x] 2.1 Add `Coverlet.runsettings` at the repo root (XPlat collector,
  OpenCover format)

## 3. Incremental gate script

- [x] 3.1 Port `scripts/check_incremental_coverage.py` from the reference
  project (default threshold 0.80, excluded paths: tests/Migrations/obj/
  Designer/g.cs)

## 4. Verify

- [x] 4.1 `dotnet test tests/OpenDockify.UnitTests --collect:"XPlat Code
  Coverage" --settings Coverlet.runsettings --results-directory TestResults`
  passes and produces `coverage.opencover.xml`
- [x] 4.2 `python3 scripts/check_incremental_coverage.py --base HEAD` reports
  no new executable lines (pass) on a clean checkout
- [x] 4.3 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
