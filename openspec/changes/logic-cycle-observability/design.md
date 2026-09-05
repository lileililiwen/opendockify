## Context

`DocumentFillScreen` debounces `_flushDraft()` and invokes it unawaited during
dispose; writes do not carry a revision and `_pendingDraft` is not cleared
after success. Backend services use EF transactions for some flows, while
workers and manual retry need explicit ownership of delivery state. Existing
tests cover important happy paths but not all interleavings or crash points.

## Decisions

- Add a monotonic draft revision and a serialized writer per form key; only the
  newest revision may replace stored state, and dispose awaits a bounded flush
  when possible.
- Model backend transitions as named states with allowed transitions and
  structured reason codes. Persist durable state before enqueueing work and use
  idempotency keys for replay.
- Add correlation IDs to API responses/log scopes and metrics for transition,
  retry, exhaustion, and recovery counts. Redact document bodies, tokens,
  signatures, and personal data.
- Use injected clock and fake transport in tests to deterministically exercise
  cancellation, timeout, duplicate delivery, and process restart behavior.
