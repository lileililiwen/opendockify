## Why

E-signature is explicitly out of MVP scope, but the data model must reserve the
seams now so a future change can implement signing without a schema redesign:
a signing-status field on documents, signer entities, and an audit-log table.
Code comments must make clear that legally reliable signing requires the
deployer to integrate external CA and timestamping services — this project only
orchestrates workflow and never issues certificates.

## What Changes

- `Document.SigningStatus` enum field (NotInitiated / PendingSignature /
  Signed / Rejected / Expired), default `NotInitiated`, added to the `Document`
  entity with a migration.
- `Signer` entity (reserved): id, document id, name, role, signing order,
  status, signed-at. Created only by future signing workflows; no MVP UI/API.
- `SigningAuditLog` entity (reserved): id, document id, actor, action,
  timestamp, detail. Also write-only seam for future signing workflows.
- `EsignModuleExtensions` registers the services/interfaces as
  skeletons: `ISigningOrchestrator` (not implemented in MVP — documented
  interface only), with comments about CA/timestamp integration being the
  deployer's responsibility.
- No user-facing endpoints; no changes to document generation flow other than
  the new status field defaulting to `NotInitiated`.

## Capabilities

### New Capabilities

- `esign-extensions`: reserved signing status on documents, signer entities,
  signing audit log, and interface skeletons with explicit CA/timestamp
  responsibility notes.

### Modified Capabilities

- `document-generation`: `Document` gains `SigningStatus` (default
  `NotInitiated`); no behavior change.

## Non-goals

- Actual signing, certificate issuance, stamping, or CA/timestamp integration.
- Any signer/audit UI or API.
- Signature image upload or seal management.

## Impact

- New module `src/OpenDockify.Esign`: `Models/Signer.cs`,
  `Models/SigningAuditLog.cs`, `Models/SigningStatus.cs`,
  `Configuration/SignerConfiguration.cs`, `Configuration/SigningAuditLogConfiguration.cs`,
  `Services/ISigningOrchestrator.cs` (skeleton + AGPL/CA comments),
  `EsignModuleExtensions.cs`.
- `OpenDockify.Data`: `Signer`, `SigningAuditLog` DbSets + `Document.SigningStatus`
  migration.
- README note: signing not implemented; deployer must integrate CA +
  timestamping for legally reliable signatures.
