## 1. Tests First

- [x] 1.1 Add graph/compiler tests for branches, cycles, reachability, operators, and unknown fields
- [x] 1.2 Add session-policy tests for branching, hidden-answer removal, expiry, revision conflicts, and owner isolation
- [x] 1.3 Add Flutter controller/widget contract tests and enumerate HTTP smoke scenarios

## 2. Template and Persistence

- [x] 2.1 Add versioned interview DTOs and bounded JSON validation to `OpenDockify.Templates`
- [x] 2.2 Create `OpenDockify.Interviews` entities, configurations, service, cleanup job, DI registration, and provider-compatible migration
- [x] 2.3 Add template revision binding required by sessions, reusing template-portability revision infrastructure if already shipped

## 3. API and Flutter

- [x] 3.1 Add owner-scoped create/read/answer/back/review/delete session endpoints with structured errors
- [x] 3.2 Connect completion to existing preview/finalize services without bypassing validation
- [x] 3.3 Build the accessible step, progress, error, resume, and review UI with safe local recovery

## 4. Verify and Deliver

- [x] 4.1 Validate OpenSpec and run format, analyzer, architecture, and unit gates
- [x] 4.2 Build with zero warnings/errors and run backend plus Flutter suites
- [x] 4.3 Apply migrations and exercise happy, branch-change, forged-answer, expiry, revision-conflict, and cross-user HTTP scenarios
- [x] 4.4 Update API/user documentation, archive the change, and commit only related paths
