## 1. Tests First

- [x] 1.1 Add canonicalization/digest golden tests and malformed, oversized, deep, tampered, and unknown-version cases
- [x] 1.2 Add revision, conflict-policy, provenance, rollback, authorization, and atomicity tests
- [x] 1.3 Add Flutter import/export interaction tests and HTTP smoke matrix

## 2. Revision Domain

- [x] 2.1 Add stable identity, immutable revision, provenance, constraints, indexes, and provider-compatible migration with backfill
- [x] 2.2 Refactor template reads/edits and document generation to bind the current exact revision
- [x] 2.3 Implement rollback as a new revision and preserve built-in copy semantics

## 3. Package API and Flutter

- [x] 3.1 Implement bounded canonical export and digest verification
- [x] 3.2 Implement validation receipts and atomic import with explicit conflict policy
- [x] 3.3 Add authorized export, validate/import, revision history, and rollback endpoints
- [x] 3.4 Add Flutter provenance, version, export, validation report, and conflict-confirmation controls

## 4. Verify and Deliver

- [x] 4.1 Validate OpenSpec and run formatting, analyzer, architecture, and unit gates
- [x] 4.2 Build zero-warning and run backend plus Flutter suites
- [x] 4.3 Verify migration, deterministic cross-export, all conflict modes, rollback, tampering, and cross-user isolation via HTTP
- [x] 4.4 Document the package schema, archive, and commit only related paths
