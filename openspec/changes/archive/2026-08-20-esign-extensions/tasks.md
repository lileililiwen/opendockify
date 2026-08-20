## 1. Esign Module (skeletons)

- [x] 1.1 Create `src/OpenDockify.Esign`; add `Models/SigningStatus.cs`
  (NotInitiated, PendingSignature, Signed, Rejected, Expired),
  `Models/Signer.cs` (Id, DocumentId, Name, Role, SigningOrder, Status,
  SignedAt), `Models/SigningAuditLog.cs` (Id, DocumentId, Actor, Action,
  Timestamp, Detail)
- [x] 1.2 Add `Configuration/SignerConfiguration.cs` and
  `Configuration/SigningAuditLogConfiguration.cs`
- [x] 1.3 Add `Services/ISigningOrchestrator.cs` — interface skeleton with
  header comment: project orchestrates signing workflow only; does NOT issue
  certificates; deployer MUST integrate external CA + timestamping for
  legally-reliable signatures
- [x] 1.4 `EsignModuleExtensions.cs` — registers no executable signing
  implementation (interface only)
- [x] 1.5 Wire `EsignModuleExtensions` into `OpenDockify.Api`

## 2. Document Status Field

- [x] 2.1 Add `SigningStatus` to `OpenDockify.Generation`'s `Document` entity,
  default `NotInitiated`
- [x] 2.2 Expose it read-only in the document DTOs

## 3. Data + Migration

- [x] 3.1 Register `Signer`, `SigningAuditLog` DbSets in `OpenDockify.Data`
- [x] 3.2 `dotnet ef migrations add AddEsignReservedSchema` and apply
- [x] 3.3 README note: e-signature not implemented; CA/timestamping is the
  deployer's responsibility

## 4. Build & Verify

- [x] 4.1 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
- [x] 4.2 Verify DB: `Signer`, `SigningAuditLog` tables exist;
  `Documents.SigningStatus` column exists
- [x] 4.3 Generate a document → status is `NotInitiated` in API response
- [x] 4.4 Confirm no `/api` endpoint creates or mutates signers/audit rows;
  confirm `ISigningOrchestrator` comments carry the CA/timestamp responsibility
  statement