## Context

Direct port of the proven approach from the reference project. ArchUnitNET
loads the compiled assemblies by name and checks the rules; no runtime DB
needed. The fixture declares:

- `_moduleAssemblyNames`: every module assembly (`OpenDockify.Auth`,
  `OpenDockify.Templates`, `OpenDockify.Generation`, `OpenDockify.Rendering`,
  `OpenDockify.Finance`, `OpenDockify.AiAssist`, `OpenDockify.Esign`,
  `OpenDockify.SystemConfig`, `OpenDockify.Data`, `OpenDockify.Api`).
- `_allowedModuleDependencies`: the declared dependency map (csproj graph):
  - `Auth` → none
  - `Templates` → Auth, Finance, SystemConfig
  - `Generation` → Templates, Finance, Rendering, SystemConfig
  - `Rendering` → SystemConfig (font/config), Finance (formatting)
  - `Finance` → SystemConfig (ISystemConfigReader)
  - `AiAssist` → SystemConfig, Templates
  - `Esign` → none (skeleton module)
  - `SystemConfig` → none
  - `Data` → all modules (central reference)
  - `Api` → all modules (composition root)
- Tests: no `OpenDockify.Data` dependency outside Data; no concrete
  `AppDbContext` reference outside Data; module graph match; no module→Api
  dependency; Api references every module.

## Goals / Non-Goals

**Goals:**
- Machine enforcement of the modular-monolith boundaries.
- Fail loudly when the fixture is out of date (new module added).

**Non-Goals:**
- Runtime dependency analysis of third-party packages.
- Enforcing UI-layer rules (no UI in MVP; frontend is a separate repo).

## Decisions

- **ArchUnitNET + xUnit** (same stack as the reference project): load real
  assemblies, keep the fixture data-driven so new modules only extend the two
  arrays + composition-root loop.
- **Dependency map is the source of truth** for what modules may reference;
  documented in the fixture header and cross-referenced with Agents.md §2.

## Risks / Trade-offs

- [Risk: fixture drift when module graph evolves] → Mitigation: tests fail on
  ANY unknown module relationship; the header comment explains how to update
  the fixture.
- [Risk: ArchUnitNET version churn] → Mitigation: pin exact versions.

## Migration Plan

1. Add `tests/OpenDockify.ArchitectureTests` (xUnit, ArchUnitNET).
2. Port `ModuleArchitectureTests.cs`, adapting module names + dependency map.
3. Add project to the solution.
4. Verify `dotnet test` passes with the current (scaffold-only) module set;
   update the fixture as modules get real references in later changes.

## Open Questions

- Should `Rendering` depend on `Finance` for formatting? Decision: yes, keep
  formatting concerns centralized in Finance; adjust the map if the dependency
  is not needed at implementation time.
