## Why

Consistent, enforced code style and static analysis prevent whole classes of
defects and keep the modular monolith maintainable as modules are added.
Without shared build properties and a root `.editorconfig`, each project drifts
and analyzer warnings silently accumulate — contradicting the "serious code"
standard (0 warnings / 0 errors) in Agents.md.

## What Changes

- Root `.editorconfig`: charset/EOF/indentation defaults, C# ordering, naming
  rules (private fields `_camelCase`, public PascalCase, `I` interfaces, `T`
  type parameters), style preferences, new-line/space formatting, and scoped
  relaxations for generated migration code and xUnit test naming.
- `Directory.Build.props` shared by every project: `Nullable=enable`,
  `LangVersion=latest`, `AnalysisLevel=latest-recommended`,
  `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`,
  `Deterministic=true`, plus NuGet audit properties (nuget-audit change) and
  the Husky restore target (git-hooks change).
- `SonarAnalyzer.CSharp` referenced solution-wide with `PrivateAssets=all` so
  analyzer violations fail the build.
- `dotnet format` is deterministic and CI-verifiable (`--verify-no-changes`).

## Capabilities

### New Capabilities

- `editorconfig-and-analyzers`: enforced code style + analyzers across the
  solution, deterministic formatting, scoped relaxations.

### Modified Capabilities

None.

## Non-goals

- No per-project style overrides beyond the documented exceptions.
- No third-party formatting tooling (e.g. dotnet-format standalone) beyond the
  SDK's built-in `dotnet format`.

## Impact

- Root `.editorconfig`, `Directory.Build.props` in the solution root.
- `OpenDockify.Data` (and future projects) inherit properties; generated
  migration code (`src/OpenDockify.Data/Migrations/*.cs`) gets the CA1861
  suppression scoped.
- `tests/**/*.cs` gets CA1707 suppression for underscore test names.
- Build gate becomes: format clean + 0 warnings/0 errors.
