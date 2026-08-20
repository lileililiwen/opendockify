## 1. Audit properties

- [x] 1.1 Add to `Directory.Build.props`:
  `NuGetAudit=true`, `NuGetAuditMode=all`, `NuGetAuditLevel=high`,
  `NuGetAuditSuppress` (empty)
- [x] 1.2 Add the policy to CONTRIBUTING: upgrade → pin → accept order;
  `NuGetAuditSuppress` entries require a rationale comment

## 2. Verify

- [x] 2.1 `dotnet restore` on a clean tree completes without audit errors
- [x] 2.2 Probe: reference a package version with a known high/critical
  advisory → restore/audit fails; remove the probe and confirm clean
- [x] 2.3 `dotnet list OpenDockify.sln package --vulnerable --include-transitive`
  reports no High/Critical findings
- [x] 2.4 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
