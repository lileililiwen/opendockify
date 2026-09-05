# logic-cycle-observability Specification

## Purpose
TBD - created by archiving change logic-cycle-observability. Update Purpose after archive.
## Requirements
### Requirement: Versioned asynchronous draft persistence

Each draft save SHALL carry a monotonically increasing revision per form key,
and storage SHALL never allow an older completion to overwrite a newer draft.
The client SHALL expose saved, saving, and save-failed status.

#### Scenario: Draft writes complete out of order

- **WHEN** revision 2 completes before revision 1
- **THEN** revision 1 is discarded and a subsequent restore returns revision 2

### Requirement: Atomic idempotent workflow transitions

Document finalization and webhook delivery SHALL persist durable state and
associated outbox/idempotency records atomically, enforce allowed transitions,
and return a stable replay result for the same idempotency key.

#### Scenario: Finalize request is retried after a timeout

- **WHEN** the client repeats the request with the same idempotency key
- **THEN** exactly one document/outbox result exists and the replay response
  identifies the original result without duplicating work

### Requirement: Redacted transition observability

Every asynchronous transition SHALL have a correlation ID, outcome, reason, and
duration in structured logs or metrics; logs SHALL exclude secrets, signatures,
request bodies, and personal document values.

#### Scenario: Webhook delivery becomes exhausted

- **WHEN** the retry budget is consumed
- **THEN** the delivery is terminal with a safe reason code, the operator can
  distinguish retryable from blocked states, and no payload or secret is logged

