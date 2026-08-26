## ADDED Requirements

### Requirement: Scoped service tokens

The system SHALL let an authenticated user create expiring service tokens with named least-privilege scopes and SHALL store only a strong hash; the clear token MUST be returned once and support immediate revocation.

#### Scenario: Scoped request succeeds

- **WHEN** a valid token calls an endpoint covered by its scope
- **THEN** the action runs only within the token owner's resources and is attributed to that token

#### Scenario: Excess scope denied

- **WHEN** a token calls an endpoint outside its scopes or after expiry/revocation
- **THEN** the API returns an authorization error and performs no action

### Requirement: Versioned automation API

The system SHALL expose stable versioned endpoints for listing usable templates, validating/previewing answers, finalizing a document, checking operation status, and retrieving owner-visible results using bounded requests and structured errors.

#### Scenario: Automated finalization

- **WHEN** a scoped client submits valid answers to finalization
- **THEN** the existing validation and immutable generation pipeline creates exactly one owner-scoped document

#### Scenario: Invalid payload

- **WHEN** required answers are absent or malformed
- **THEN** the API returns machine-readable field errors and creates no document

### Requirement: Idempotent mutations

The system SHALL require an idempotency key for automation mutations and SHALL bind it to token, route, and request digest for a configurable retention period.

#### Scenario: Safe retry

- **WHEN** the same token repeats the same request with the same key
- **THEN** the original status and response are returned without repeating the mutation

#### Scenario: Key reused with different payload

- **WHEN** the same token and key are used with a different request digest
- **THEN** the API returns `409 Conflict` and performs no mutation

### Requirement: Signed webhook delivery

The system SHALL deliver selected owner events to validated HTTPS endpoints with event id, timestamp, and HMAC signature; delivery SHALL retry with bounded exponential backoff and retain an inspectable delivery record.

#### Scenario: Receiver verifies event

- **WHEN** a webhook is delivered
- **THEN** its signature covers the exact body, event id, and timestamp so the receiver can reject alteration or replay

#### Scenario: Delivery repeatedly fails

- **WHEN** all configured attempts fail
- **THEN** the delivery is marked exhausted without blocking the originating transaction and can be manually retried

### Requirement: Outbound request safety

The system MUST reject webhook destinations resolving to loopback, link-local, private, multicast, or cloud-metadata addresses unless explicitly allowlisted by the deployer; it SHALL revalidate redirects and DNS resolution on every attempt.

#### Scenario: Destination resolves internally

- **WHEN** a configured or redirected destination resolves to a prohibited address
- **THEN** delivery is blocked and the safe reason is recorded without making the request

### Requirement: Integration observability

The owner SHALL be able to list token usage and webhook deliveries without viewing stored token hashes or webhook secrets.

#### Scenario: Secret redaction

- **WHEN** integration configuration or logs are retrieved
- **THEN** credentials, authorization headers, and document content are absent

