## 1. Test project

- [ ] 1.1 Create `tests/OpenDockify.UnitTests` (xUnit) with a trivial passing
  test to validate the harness; add to the solution
- [ ] 1.2 Add `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`
  (pinned versions)

## 2. Coverage collector

- [ ] 2.1 Add `Coverlet.runsettings` at the repo root (XPlat collector,
  OpenCover format)

## 3. Incremental gate script

- [ ] 3.1 Port `scripts/check_incremental_coverage.py` from the reference
  project (default threshold 0.80, excluded paths: tests/Migrations/obj/
  Designer/g.cs)

## 4. Verify

- [ ] 4.1 `dotnet test tests/OpenDockify.UnitTests --collect:"XPlat Code
  Coverage" --settings Coverlet.runsettings --results-directory TestResults`
  passes and produces `coverage.opencover.xml`
- [ ] 4.2 `python3 scripts/check_incremental_coverage.py --base HEAD` reports
  no new executable lines (pass) on a clean checkout
- [ ] 4.3 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
