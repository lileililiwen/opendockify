## Why

The supplied Docker deployment persists critical legal documents and secrets but offers no application-consistent backup, restore verification, or archive health check. A self-hosted-only product needs a tested recovery path before users can trust it with durable records.

## What Changes

- Add administrator-triggered, application-consistent backup bundles with manifest and checksums.
- Add offline restore validation, explicit destructive confirmation, and provider-compatible restore rules.
- Add scheduled retention and a read-only integrity checker.
- Document and test disaster recovery for SQLite and external database providers.

## Capabilities

### New Capabilities

- `backup-restore`: Encrypted backup export, integrity validation, controlled restore, retention, and recovery verification.

### Modified Capabilities

None.

## Non-goals

- No hosted backup service, cross-provider database conversion, continuous replication, high availability, or replacement for infrastructure snapshots.

## Impact

- New operations module and CLI/admin endpoints, maintenance-mode coordination, Docker volume documentation, encryption-key handling, scheduled job, and provider-specific integration tests.

