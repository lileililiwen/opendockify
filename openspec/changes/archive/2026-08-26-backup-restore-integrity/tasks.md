## 1. Tests First

- [x] 1.1 Add manifest, authenticated encryption, wrong-key, corruption, truncation, zip-bomb/path traversal, and version-compatibility tests
- [x] 1.2 Add integrity checker and safe-retention tests including symlink and unknown-file cases
- [x] 1.3 Add crash/failure-injection restore tests and recovery fixtures for every supported provider

## 2. Backup and Integrity

- [x] 2.1 Add document content digests, operation metadata, migration, and a bounded backfill report
- [x] 2.2 Implement provider snapshot adapters and consistent file-set capture
- [x] 2.3 Implement streaming manifest, checksum, KDF, authenticated encryption, validation, and read-only integrity services
- [x] 2.4 Implement scheduled local backups and conservative retention against a validated mounted path

## 3. Restore and Operations

- [x] 3.1 Implement maintenance-mode admission control and CLI/admin authorization boundary
- [x] 3.2 Implement digest-bound validation receipts, staged restore, rollback snapshot, and post-restore verification
- [x] 3.3 Add progress/status reporting with secret-free structured logs and cancellation only at safe boundaries

## 4. Verify and Deliver

- [x] 4.1 Validate OpenSpec and run formatting, analyzer, architecture, and unit gates
- [x] 4.2 Build zero-warning and execute provider recovery fixtures plus failure injection
- [x] 4.3 Perform a clean-deployment recovery drill and compare users, templates, document metadata, and PDF digests
- [x] 4.4 Document key custody, downtime, schedules, external-provider tooling, and rollback; archive and commit only related paths
