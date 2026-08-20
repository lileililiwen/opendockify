## Why

The modular-monolith rules in Agents.md §2 (modules never reference
`OpenDockify.Data`, no reference cycles, composition root references every
module) are currently documentation. The reference project proves these rules
are machine-checkable via an architecture test suite (ArchUnitNET). Encoding
them as tests turns a documented convention into a build-blocking guarantee.

## What Changes

- `tests/OpenDockify.ArchitectureTests` (xUnit + ArchUnitNET):
  - module namespaces never depend on `OpenDockify.Data`;
  - module services depend only on the base EF `DbContext`, never the concrete
    `AppDbContext`;
  - the module dependency graph matches the declared `_allowedModuleDependencies`
    map (csproj graph from Agents.md §2);
  - no module depends on the composition root (`OpenDockify.Api`);
  - the composition root references every module assembly.
- Fixture maintenance note: adding a module requires updating the module list,
  dependency map, and composition-root expectation — a failing test is the
  signal.

## Capabilities

### New Capabilities

- `architecture-enforcement`: automated, CI-runnable tests that enforce the
  modular-monolith boundary rules.

### Modified Capabilities

None.

## Non-goals

- No changes to the module graph itself (that belongs to
  `platform-foundation` and future capability changes).
- No other architecture analysis (e.g. dependency metrics, layer checks beyond
  the module graph).

## Impact

- New test project `tests/OpenDockify.ArchitectureTests` referencing ArchUnitNET
  and xUnit; added to the solution.
- CI runs it via `dotnet test` (coverage-gates / ci-pipeline changes).
