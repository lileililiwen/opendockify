## Why

Self-hosted software ships its own supply chain; vulnerable dependencies are a
direct security risk to deployers. NuGet's restore-time audit plus a CI
`dotnet list package --vulnerable` step catches known high/critical advisories
in direct and transitive packages, with a documented path for accepted risks
instead of silent suppressions.

## What Changes

- `Directory.Build.props` gains NuGet audit properties:
  `NuGetAudit=true`, `NuGetAuditMode=all` (direct + transitive),
  `NuGetAuditLevel=high` (fail on high/critical).
- `NuGetAuditSuppress` allowlist (empty by default) for documented, reviewed
  exceptions — each entry requires a rationale comment (see CONTRIBUTING).
- The CI audit step (`ci-pipeline` change) runs
  `dotnet list package --vulnerable --include-transitive` and fails on
  High/Critical.

## Capabilities

### New Capabilities

- `nuget-audit`: restore-time and CI dependency vulnerability scanning with an
  explicit, documented suppression policy.

### Modified Capabilities

None.

## Non-goals

- No license-compliance scanning (out of scope).
- No automated dependency-update PRs (dependabot/renovate deferred).
- No SBOM generation.

## Impact

- `Directory.Build.props` property additions.
- CONTRIBUTING documents the upgrade → pin → accept order and the rationale
  requirement for `NuGetAuditSuppress`.
- CI step referenced by `ci-pipeline`.
