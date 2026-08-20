## Context

Ported from the reference project. Two layers:

1. **Restore-time audit** via `Directory.Build.props`:
   `NuGetAudit=true`, `NuGetAuditMode=all`, `NuGetAuditLevel=high`. Because
   `TreatWarningsAsErrors=true`, restore-time audit warnings surface as build
   errors. This is the default gate.

2. **CI belt-and-suspenders** (in `ci-pipeline`):
   `dotnet list OpenDockify.sln package --vulnerable --include-transitive`,
   grepping for `High|Critical` and failing the pipeline. It produces a
   readable report and explicitly covers transitive packages.

Accepted-risk policy: `NuGetAuditSuppress` in `Directory.Build.props`, each
entry with a comment stating the rationale (advisory URL + why it can't be
fixed). CONTRIBUTING documents the upgrade → pin → accept order. Suppression is
never silent.

## Goals / Non-Goals

**Goals:**
- Fail on high/critical known vulnerabilities (direct + transitive).
- A documented, reviewed acceptance path.

**Non-Goals:**
- License scanning, SBOM, dependabot automation.

## Decisions

- **`NuGetAuditLevel=high`** — fail on high and critical; medium/low are
  reported by `dotnet list` but don't gate.
- **Keep the audit allowlist minimal**; empty until a real accepted advisory
  exists.
- **Audit is enforced in CI as a required gate** (via ci-pipeline's build
  job).

## Risks / Trade-offs

- [Risk: audit false positives on unrelated advisories] → Mitigation: the
  explicit accept path with rationale; documented in CONTRIBUTING.
- [Risk: transitive audit noise] → Mitigation: `--include-transitive` is
  intentional; pinning parent versions is the documented fix.

## Migration Plan

1. Add NuGet audit properties to `Directory.Build.props`.
2. Document the policy in CONTRIBUTING (upgrade → pin → accept).
3. Verify: a clean restore passes; a probe package with a known advisory fails
   the audit (then remove the probe).

## Open Questions

- Should medium severity also gate? Decision: no — report-only for medium,
   keep the MVP gate at high/critical.
