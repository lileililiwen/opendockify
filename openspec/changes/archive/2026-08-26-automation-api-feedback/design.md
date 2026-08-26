## Context

The automation surface already returns structured `{error:{code,message,fields}}`
bodies for validation and conflict outcomes (snapshot-tested), but auth-layer
short-circuits bypass endpoint code: ASP.NET's default 401 challenge and 403
forbidden emit empty bodies. The rate limiter has one global `OnRejected` writer
authored for login throttling. `DateTime` values round-tripped through SQLite
lose `DateTimeKind`, so some responses serialize without a `Z` offset. The
finalize payload carries `documentId` only; the idempotency record's operation
id is never surfaced.

Research: existing contract snapshots in
`tests/OpenDockify.UnitTests/AutomationIdempotencyTests.cs`.

## Goals / Non-Goals

**Goals:** one feedback shape for every automation outcome; self-service status
checks; unambiguous timestamps; no behavior change to idempotency or delivery.

**Non-Goals:** new endpoints/scopes; UI work; changing JWT endpoints' error
bodies (interactive clients are out of scope).

## Decisions

- Add `operationId` to `AutomationDocumentPayload` as the field right after
  `documentId`; the idempotency record already stores it as the row id, so both
  fresh and replayed executions can populate it from persisted state.
- Override `HandleChallengeAsync` in `ServiceTokenHandler` to write the
  structured 401 body (`token_invalid`) with `WWW-Authenticate` retained.
- Implement `IAuthorizationMiddlewareResultHandler` to intercept forbidden
  automation outcomes and write the structured 403 body (`forbidden_scope`);
  non-automation schemes keep default behavior.
- Branch the global rate-limiter `OnRejected` on path prefix: `/api/v1/automation`
  gets `{error:{code:"rate_limited",...}}`; login/registration keep existing copy.
- Normalize read-side timestamps with `DateTime.SpecifyKind(..., Utc)` in
  `AutomationOperationView` mapping so serialization always emits `Z`.

## Risks / Trade-offs

- [Contract bytes change] -> success/error snapshots updated in the same change;
  replay stores new-format payloads going forward, old stored rows replay their
  original bytes until retention expires (documented).
- [Authorization handler global] -> handler checks the scheme claim and falls
  through to default rendering for everything else.

## Migration Plan

Ship behind no flag: additive fields plus corrected failure bodies. Integrators
parsing strictly by position are unaffected (JSON object); docs examples update
in the same commit.

## Open Questions

- None.
