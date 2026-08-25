## 1. Tests First

- [x] 1.1 Add authorization matrix tests for owner, viewer, reviewer, stranger, revoked, and deleted-document states
- [x] 1.2 Add token entropy/hash, expiry, indistinguishable failure, rate-limit, headers, download-policy, and audit-redaction tests
- [x] 1.3 Add Flutter sharing interaction tests and HTTP smoke matrix

## 2. Sharing Domain

- [x] 2.1 Create sharing module entities/configurations/services, retention cleanup, indexes, and provider-compatible migration
- [x] 2.2 Implement central read authorization while retaining direct owner checks for all mutations
- [x] 2.3 Implement secure link generation/verification and append-only privacy-bounded audit events

## 3. API and Flutter

- [x] 3.1 Add owner-only grant/link lifecycle and paginated audit endpoints
- [x] 3.2 Add rate-limited public view/download endpoints with defensive response headers
- [x] 3.3 Add Flutter internal grant, external link, one-time copy, revoke, and audit controls

## 4. Verify and Deliver

- [x] 4.1 Validate OpenSpec and run formatting, analyzer, architecture, and unit gates
- [x] 4.2 Build zero-warning and run backend plus Flutter suites
- [x] 4.3 Exercise the complete authorization matrix, expiry/revocation, download denial, rate limiting, and emergency disable over HTTP
- [x] 4.4 Update privacy/operator docs, archive, and commit only related paths
