## 1. Tests First

- [x] 1.1 Add token hashing, scopes, expiry, revocation, owner isolation, and audit-redaction tests
- [x] 1.2 Add idempotency concurrency/replay/conflict tests and stable API contract snapshots
- [x] 1.3 Add webhook signature, retry, lease recovery, redirect, DNS rebinding, prohibited-range, and allowlist tests

## 2. Integration Domain

- [x] 2.1 Create token, idempotency, subscription, outbox, and delivery entities/configuration with provider-compatible migration
- [x] 2.2 Implement one-time token/secret services and shared request authentication
- [x] 2.3 Implement transactional idempotency and outbox recording around existing generation services
- [x] 2.4 Implement leased delivery worker, HMAC envelope, bounded retry, retention, and SSRF-safe HTTP transport

## 3. API and Operations

- [x] 3.1 Add `/api/v1/automation` template, preview, finalize, status, and result endpoints with structured errors
- [x] 3.2 Add user-owned token/subscription/delivery management and manual retry endpoints
- [x] 3.3 Publish OpenAPI examples, event schemas, signature verification examples, limits, and firewall guidance

## 4. Verify and Deliver

- [x] 4.1 Validate OpenSpec and run formatting, analyzer, architecture, and unit gates
- [x] 4.2 Build zero-warning and run concurrency plus provider integration suites
- [x] 4.3 Exercise scopes, retries, duplicate requests, restart recovery, signature verification, SSRF blocks, and secret redaction end to end
- [ ] 4.4 Archive and commit only related paths
