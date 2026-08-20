# esign-extensions Specification

## Purpose
TBD - created by archiving change esign-extensions. Update Purpose after archive.
## Requirements
### Requirement: Signing status field

The system SHALL include a signing-status field on documents with the values
`NotInitiated`, `PendingSignature`, `Signed`, `Rejected`, `Expired`, defaulting
to `NotInitiated`; in the MVP no flow changes this field.

#### Scenario: Default status

- **WHEN** a document is generated
- **THEN** its signing status is `NotInitiated`

#### Scenario: Status enum available

- **WHEN** the document schema is inspected
- **THEN** the four future states (`PendingSignature`, `Signed`, `Rejected`,
  `Expired`) exist in the enum

### Requirement: Reserved signer entity

The system SHALL provide a `Signer` entity (document id, name, role, signing
order, status, signed-at) for future signing workflows; no MVP endpoint or UI
SHALL create or mutate signers.

#### Scenario: Schema present, no API

- **WHEN** the database schema is inspected
- **THEN** a `Signer` table exists, and no `/api` endpoint references signers

### Requirement: Reserved audit log

The system SHALL provide a `SigningAuditLog` entity (document id, actor,
action, timestamp, detail) for future signing workflows; no MVP endpoint writes
to it.

#### Scenario: Table present, no writes

- **WHEN** the database schema is inspected
- **THEN** a `SigningAuditLog` table exists, and no MVP code path writes to it

### Requirement: Interface skeleton with CA/timestamp responsibility note

The system SHALL expose an `ISigningOrchestrator` interface whose code comments
state that this project orchestrates signing workflow only and does NOT issue
certificates; legally reliable signing requires the deployer to integrate
external CA and timestamping services.

#### Scenario: Comments document responsibility

- **WHEN** a developer opens `ISigningOrchestrator`
- **THEN** the header comment explains the CA/timestamping responsibility and
  the project's non-implementation of certificate issuance

#### Scenario: Skeleton is inert

- **WHEN** the app starts
- **THEN** no signing behavior executes; document generation is unaffected
  beyond the default status field

