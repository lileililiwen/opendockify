## Why

The audit found asynchronous and transactional paths whose state transitions
are difficult to observe and whose failure behavior is not consistently
specified. In particular, draft writes are debounced and flushed in fire-and-
forget code, while webhook retries and document finalization cross database,
worker, and UI boundaries. These paths can lose the latest state, duplicate
work, or leave the user without an actionable recovery signal.

## What Changes

- Define explicit state machines and invariants for draft save, preview,
  finalize/re-edit, outbox delivery, retry, and restore operations.
- Make asynchronous operations single-flight or version-aware, cancellable,
  and observable with correlation IDs and structured outcome metrics.
- Enforce transaction/idempotency boundaries and test crash/retry/replay cases.
- Expose safe operational status to the app without leaking payloads/secrets.

## Capabilities

### New Capabilities

- `logic-cycle-observability`: reliable state transitions and diagnostics.

## Non-goals

- No general event-sourcing rewrite, distributed queue adoption, or telemetry
  SaaS dependency.
