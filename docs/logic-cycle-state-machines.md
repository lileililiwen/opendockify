# Logic-cycle invariants

This document is the operational contract for asynchronous document and
integration work.

## Drafts

`editing -> saving -> saved` is the normal path. A write failure produces
`save-failed` and keeps the latest in-memory draft available for retry. Each
form key has a monotonic revision; a completion may persist only its revision
or a newer one. Clearing a draft waits for already queued writes and then
removes the revision state.

## Documents and outbox

Finalization persists the immutable document, its outbox event, and its
idempotency record in one transaction. A repeated idempotency key replays the
original response; a different request digest is a conflict. Preview never
creates a document or outbox event.

## Webhook deliveries

`pending -> delivering -> delivered` is success. A failed attempt returns to
`pending` until the bounded retry budget is exhausted, then becomes
`exhausted`. An outbound-safety refusal becomes `blocked` and sends no bytes.
Manual retry atomically claims every state except `delivering`, resets the
attempt budget, and returns to `pending`.

## Restore maintenance

Restore enters maintenance mode before replacing the database. Successful
verification exits maintenance; failed verification keeps traffic blocked and
requires operator recovery. Logs identify the transition and correlation ID,
but never include document payloads, secrets, signatures, or personal values.
