## 1. Workflow foundation

- [x] 1.1 Inventory current workflow triggers, required checks, secrets, and undocumented assumptions.
- [x] 1.2 Add strict OpenSpec validation and a Flutter toolchain/cache setup.
- [x] 1.3 Add Dart format/analyze/test gates and backend quality gates with stable action versions.

## 2. Delivery-path gates

- [x] 2.1 Add web and Linux Flutter build jobs and an Android build matrix entry with explicit runner behavior.
- [x] 2.2 Add Docker build, isolated SQLite startup, health polling, migration/seed assertion, and cleanup.
- [x] 2.3 Add artifact upload for test results, coverage, build logs, and container logs on failure.

## 3. Release safety

- [x] 3.1 Define immutable artifact names, commit SHA labels, and a no-secret logging policy.
- [x] 3.2 Document required status checks and local equivalents in `CONTRIBUTING.md`.
- [x] 3.3 Run the workflow-equivalent commands locally and verify deliberate failures are reported.
