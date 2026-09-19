## ADDED Requirements

### Requirement: SMTP notifications

The system SHALL send share, expiry, and finalization notifications via self-hosted SMTP when enabled, disabled by default with a safe dev fallback.

#### Scenario: Share notifies when enabled

- **WHEN** `Notifications:Enabled=true` with valid SMTP and a grant is created
- **THEN** a plaintext notification mail is accepted by `IMailService` with outcome `Sent` and no secret in logs

#### Scenario: Disabled by default

- **WHEN** `Notifications:Enabled=false` and a grant is created
- **THEN** no SMTP call is made and creation still succeeds

### Requirement: Per-user rate and quota

The system SHALL enforce per-user+IP rate limits on generate/preview/finalize/AI and per-day quotas with RFC9457 429 responses.

#### Scenario: Generate rate limited per user

- **WHEN** one user exceeds the `generate` policy within its window from two IPs
- **THEN** further generate calls for that user return `429` with `Retry-After`, while other users are unaffected

#### Scenario: AI quota exceeded

- **WHEN** a user exceeds `Ai:MaxItemsPerDay`
- **THEN** polish endpoints return `429` with reset time and no LLM call is made

### Requirement: Platform idempotency and webhooks

The system SHALL replay automation finalize via `IIdempotencyStore` fingerprint match and deliver webhooks with HMAC retry through jobs.

#### Scenario: Idempotent replay

- **WHEN** `POST /api/v1/automation/finalize` repeats with the same `Idempotency-Key` and body
- **THEN** the identical stored response returns without creating a second document

#### Scenario: Webhook retry

- **WHEN** a webhook delivery transiently fails
- **THEN** it is retried with backoff via the jobs dispatcher and signed `t,v1` HMAC, never logging the secret

### Requirement: Link passwords

The system SHALL support optional passwords on public share links with hashed storage and lockout.

#### Scenario: Password-protected link

- **WHEN** a link with password is created and anonymous access supplies the correct password to `/s/{token}`
- **THEN** access is granted; wrong passwords fail with `401` without revealing the hash, and 5 failures lock the link temporarily
