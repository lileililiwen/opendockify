## 1. Copy shared configuration

- [ ] 1.1 Copy `.editorconfig` from the reference repo; adapt subtree paths to
  `src/OpenDockify.Data/Migrations/*.cs` and `tests/**/*.cs`
- [ ] 1.2 Copy `Directory.Build.props`; adapt solution name and remove the
  Husky/NuGet-audit targets until their changes land (or keep them commented)
- [ ] 1.3 Add `SonarAnalyzer.CSharp` (`PrivateAssets=all`, pinned version) to
  `Directory.Build.props`
- [ ] 1.4 Confirm `Nullable=enable`, `AnalysisLevel=latest-recommended`,
  `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`,
  `Deterministic=true`

## 2. Verify

- [ ] 2.1 `dotnet format --verify-no-changes` passes (no drift)
- [ ] 2.2 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
- [ ] 2.3 A deliberate analyzer violation (e.g. unused private field) fails
  the build; remove the probe after confirming
- [ ] 2.4 Migration-subtree relaxation verified: generated EF migration files
  do not trip CA1861
