## 1. Architecture test project

- [ ] 1.1 Create `tests/OpenDockify.ArchitectureTests` (xUnit) with ArchUnitNET
  packages (pinned); add to the solution
- [ ] 1.2 Port `ModuleArchitectureTests.cs` from the reference project;
  adapt module assembly names for OpenDockify
- [ ] 1.3 Declare `_allowedModuleDependencies` per Agents.md §2 (Auth/Templates/
  Generation/Rendering/Finance/AiAssist/Esign/SystemConfig/Data/Api)
- [ ] 1.4 Implement tests:
  - modules don't depend on `OpenDockify.Data`
  - modules don't depend on concrete `AppDbContext`
  - module dependency graph matches the declared map
  - no module depends on `OpenDockify.Api`
  - `OpenDockify.Api` references every module

## 2. Verify

- [ ] 2.1 `dotnet test tests/OpenDockify.ArchitectureTests` passes with the
  scaffold-only module set
- [ ] 2.2 Negative probe: temporarily add a forbidden reference → test fails;
  remove probe
- [ ] 2.3 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
