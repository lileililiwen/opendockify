## 1. Test architecture

- [ ] 1.1 Map existing tests to source-of-truth OpenSpec scenarios and record uncovered capabilities.
- [ ] 1.2 Add deterministic clock, database, authenticated-user, and fake-transport fixtures.
- [ ] 1.3 Add a single local quality command that runs backend format/build/test, coverage, Flutter analyze/test, and OpenSpec validation.

## 2. Missing regression suites

- [ ] 2.1 Add API-host tests for login/401 recovery, ownership isolation, validation, migrations/seeding, and representative document generation.
- [ ] 2.2 Add failure-injection tests for draft persistence, finalization, backup restore, AI guard, sharing expiry, and webhook retry/idempotency.
- [ ] 2.3 Expand Flutter tests across route guards, forms, loading/error/empty/busy states, and desktop/web compilation.
- [ ] 2.4 Add OpenAPI/spec scenario contract checks so endpoint payload changes fail clearly.

## 3. Verification

- [ ] 3.1 Configure changed-line coverage at 80%, with explicit exclusions for migrations/generated code.
- [ ] 3.2 Upload machine-readable test and coverage artifacts in CI.
- [ ] 3.3 Verify the full local command on a clean checkout and document expected failure diagnostics.
