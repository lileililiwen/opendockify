## 1. Tests First

- [x] 1.1 Add backend/API tests and smoke assertions for non-persisting preview,
  validation parity, exact rendered-text parity, and finalization compatibility
- [x] 1.2 Add Flutter DTO/API contract tests for preview and finalize
- [x] 1.3 Add secure draft model/store tests for serialization, user/form key
  isolation, restore, and clear behavior
- [x] 1.4 Extend dynamic form widget tests for change snapshots without validation

## 2. Backend Preview and Finalize

- [x] 2.1 Extract shared template access, validation, warning, rendering, and risk
  notice orchestration into a non-persisting preview result
- [x] 2.2 Make generation consume the successful preview result before PDF/database
  work
- [x] 2.3 Add authenticated preview and finalize endpoints and retain generate alias

## 3. Secure Flutter Drafts

- [x] 3.1 Add user/form-scoped draft model and secure-storage abstraction/implementation
- [x] 3.2 Wire secure draft storage into application providers
- [x] 3.3 Emit raw form snapshots for all field/date/clause changes
- [x] 3.4 Restore, debounce-save, flush, retain-on-error, and clear-on-success drafts

## 4. Flutter Workflow

- [x] 4.1 Add preview DTO and API client methods for preview/finalize
- [x] 4.2 Add distinct Preview and Finalize controls with shared busy protection
- [x] 4.3 Show rendered preview text and warnings without creating/navigating to a
  document

## 5. Verify and Deliver

- [x] 5.1 Validate OpenSpec strictly and run C#/Dart formatting and diff checks
- [x] 5.2 Run zero-warning backend build/tests and Flutter analyze/tests
- [x] 5.3 Execute two-user HTTP preview/finalization persistence and parity scenarios
- [x] 5.4 Archive the completed change and commit only its related paths
