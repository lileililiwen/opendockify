## 1. Test architecture

- [x] 1.1 Map existing tests to source-of-truth OpenSpec scenarios and record uncovered capabilities.
- [x] 1.2 Add deterministic clock, database, authenticated-user, and fake-transport fixtures.
- [x] 1.3 Add a single local quality command that runs backend format/build/test, coverage, Flutter analyze/test, and OpenSpec validation.

## 2. Missing regression suites

- [x] 2.1 Add API-host tests for login/401 recovery, ownership isolation, validation, migrations/seeding, and representative document generation.
- [x] 2.2 Add failure-injection tests for draft persistence, finalization, backup restore, AI guard, sharing expiry, and webhook retry/idempotency.
- [x] 2.3 Expand Flutter tests across route guards, forms, loading/error/empty/busy states, and desktop/web compilation.
- [x] 2.4 Add OpenAPI/spec scenario contract checks so endpoint payload changes fail clearly.

## 3. Verification

- [x] 3.1 Configure changed-line coverage at 80%, with explicit exclusions for migrations/generated code.
- [x] 3.2 Upload machine-readable test and coverage artifacts in CI.
- [x] 3.3 Verify the full local command on a clean checkout and document expected failure diagnostics.
