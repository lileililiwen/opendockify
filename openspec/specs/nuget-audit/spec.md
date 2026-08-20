# nuget-audit Specification

## Purpose
TBD - created by archiving change nuget-audit. Update Purpose after archive.
## Requirements
### Requirement: Dependency vulnerabilities are scanned

The system SHALL scan direct and transitive NuGet packages for known
vulnerabilities during build and CI, failing on high/critical findings.

#### Scenario: High-severity vulnerability

- **WHEN** a direct or transitive package has a known high/critical
  vulnerability
- **THEN** the restore or CI audit reports it and the pipeline fails

#### Scenario: Clean dependencies

- **WHEN** no package has a known high/critical vulnerability
- **THEN** the audit passes and does not block the pipeline

### Requirement: Audit covers transitive packages

The system SHALL audit transitive as well as direct dependencies
(`NuGetAuditMode=all`).

#### Scenario: Transitive vulnerability

- **WHEN** a transitive dependency (not directly referenced) has a high/
  critical advisory
- **THEN** the audit reports it and the pipeline fails

### Requirement: Accepted risks are explicit

The system SHALL allow a documented, reviewed exception for vulnerabilities
that cannot be fixed by upgrading, and SHALL NOT silently ignore findings.

#### Scenario: Suppressed advisory

- **WHEN** a vulnerability is accepted
- **THEN** it is recorded in the explicit suppress list (`NuGetAuditSuppress`)
  with a rationale and the rest of the audit still runs

#### Scenario: No silent ignore

- **WHEN** no suppress entry exists for a finding
- **THEN** the audit does not pass that finding

