## Context

This change is deliberately minimal and "schema-only". The intent is to keep
the door open for a future e-signature capability without redesigning the
document model, while being honest in code about what this project does and
does not do.

Three reserved pieces:

1. `Document.SigningStatus` enum (NotInitiated default; PendingSignature,
   Signed, Rejected, Expired reserved) — added to the `Document` entity defined
   by `document-generation`. No MVP code transitions it; it is written by the
   future signing capability.

2. `Signer` and `SigningAuditLog` entities — the future workflow's tables. No
   endpoints, no UI, no service writes. Their configurations are registered so
   the schema exists and migrations are stable.

3. `ISigningOrchestrator` interface skeleton with a strongly-worded header
   comment: **this project orchestrates signing workflow only; it does not
   issue certificates or provide legally reliable timestamps — the deployer
   must integrate an external CA and timestamping service.** This mirrors the
   requirement text and keeps expectations explicit for anyone reading the
   code.

A README note is added: "E-signature is not implemented; integrating a CA and
timestamping service is the deployer's responsibility."

## Goals / Non-Goals

**Goals:**
- Reserved schema (status + signers + audit log).
- Interface skeleton with responsibility documentation.
- Zero behavior change to generation flow.

**Non-Goals:**
- Any signing functionality, cert issuance, seals, stamping.
- Signer/audit APIs or UI.
- iText7 overlay wiring (that seam lives in `document-generation`'s
  `pdf-rendering` capability).

## Decisions

- **Status on the `Document` entity**, not a separate table — single source of
  truth, cheap to query, matches requirements.
- **Separate `Esign` module** for signer/audit entities + orchestrator
  skeleton, so future work lands in a dedicated domain and `document-generation`
  only gains the enum field.
- **No DI registration of a signing implementation** — only the interface is
  present; the module registers nothing executable.

## Risks / Trade-offs

- [Risk: reserved schema is dead weight in MVP] → Accepted; cost is one small
  migration, benefit is a stable contract for future work.
- [Risk: someone assumes signing exists because tables exist] → Mitigation:
  explicit comments + README note + inert skeleton (no endpoints).

## Migration Plan

1. Add `OpenDockify.Esign` module: enums, entities, configurations, interface
   skeleton with CA/timestamp comments.
2. Add `SigningStatus` to `Document` (in `OpenDockify.Generation`) default
   `NotInitiated`.
3. Register `Signer`, `SigningAuditLog` DbSets in `OpenDockify.Data`.
4. Migration `AddEsignReservedSchema`.
5. README note about signing not being implemented.
6. Verify: build 0/0; DB has the new tables + column; generation flow
   unchanged (status = NotInitiated); no `/api` signer endpoints exist.

## Open Questions

- Should the signing-status field also appear in the API's document DTO now?
  Decision: yes — expose it read-only so the frontend can render "未签署"
  state; no write path exists.
