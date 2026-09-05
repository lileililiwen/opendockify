## 1. State contracts

- [x] 1.1 Document state diagrams and invariants for drafts, documents, deliveries, backups, and restore maintenance mode.
- [x] 1.2 Define transition/result/error types and correlation-id logging fields without changing public payload compatibility unnecessarily.
- [x] 1.3 Add injected clock and deterministic failure-injection seams.

## 2. Client and backend reliability

- [x] 2.1 Serialize/version draft writes, clear committed pending state, bound dispose flush, and surface durable-save failure.
- [x] 2.2 Audit finalize/re-edit/outbox/idempotency transactions and make duplicate requests return the original result safely.
- [x] 2.3 Make worker/manual retry transitions race-safe and observable; prevent retry after terminal policy blocks.
- [x] 2.4 Add safe UI status for pending, saved, retrying, exhausted, blocked, and recovery-required outcomes.

## 3. Verification

- [x] 3.1 Add interleaving tests for out-of-order draft writes and duplicate finalize/retry requests.
- [x] 3.2 Add process-restart, timeout, cancellation, and partial-restore tests with invariant assertions.
- [x] 3.3 Add redaction tests for logs, diagnostics, and API responses; publish transition metrics in local logs.
