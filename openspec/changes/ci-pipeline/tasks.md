## 1. CI workflow

- [ ] 1.1 Add `.github/workflows/ci.yml` (ported, adapted to
  `OpenDockify.sln`):
  - trigger: push to `main`, PR to `main`
  - Setup .NET 8, restore, audit step (`dotnet list package --vulnerable
    --include-transitive` fail on High/Critical)
  - `dotnet format --verify-no-changes`
  - `dotnet build OpenDockify.sln -c Release --no-restore /warnaserror`
  - `dotnet test OpenDockify.sln -c Release --no-build --collect:"XPlat Code
    Coverage" --settings Coverlet.runsettings --results-directory TestResults`
  - PR-only incremental coverage step
  - artifact uploads (coverage always; build logs on failure)
- [ ] 1.2 Include dormant Sonar steps gated on `SONAR_TOKEN`

## 2. Verify

- [ ] 2.1 Run the exact CI command sequence locally (restore → audit → format
  → build → test) and confirm each passes
- [ ] 2.2 Simulate the PR coverage step:
  `python3 scripts/check_incremental_coverage.py --base origin/main
  --threshold 0.80 --reports 'TestResults/**/coverage.opencover.xml'`
- [ ] 2.3 Confirm the workflow file is valid YAML and references the right
  solution/test settings
