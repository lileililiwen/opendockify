# automation-integrations Specification

## Purpose
TBD - created by archiving change automation-api-webhooks. Update Purpose after archive.
## Requirements
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

The system SHALL deliver selected owner events to validated HTTPS endpoints with
event id, timestamp, and HMAC signature; delivery SHALL retry with bounded
exponential backoff and retain an inspectable delivery record including a
bounded per-attempt timeline. Transient failures (DNS resolution failures,
timeouts, connection errors) SHALL keep auto-retrying within the attempt budget;
only outbound-safety policy violations SHALL mark a delivery blocked without
further automatic attempts.

#### Scenario: Receiver verifies event

- **WHEN** a webhook is delivered
- **THEN** its signature covers the exact body, event id, and timestamp so the receiver can reject alteration or replay

#### Scenario: Delivery repeatedly fails

- **WHEN** all configured attempts fail
- **THEN** the delivery is marked exhausted without blocking the originating transaction and can be manually retried

#### Scenario: Transient failure keeps retrying

- **WHEN** an attempt fails because DNS resolution fails or the request times out
- **THEN** the delivery stays pending with a scheduled retry inside the attempt budget instead of being blocked

#### Scenario: Attempt timeline is inspectable

- **WHEN** an owner lists webhook deliveries
- **THEN** each delivery exposes its recent attempts (timestamp, status code or error) without exposing bodies or secrets

### Requirement: Outbound request safety

The system MUST reject webhook destinations resolving to loopback, link-local,
private, multicast, or cloud-metadata addresses unless explicitly allowlisted by
the deployer; it SHALL revalidate redirects and DNS resolution on every attempt.
Only these policy violations SHALL block a delivery permanently; unresolvable
destinations SHALL be treated as transient.

#### Scenario: Destination resolves internally

- **WHEN** a configured or redirected destination resolves to a prohibited address
- **THEN** delivery is blocked and the safe reason is recorded without making the request

### Requirement: Integration observability

The owner SHALL be able to list token usage and webhook deliveries without viewing stored token hashes or webhook secrets.

#### Scenario: Secret redaction

- **WHEN** integration configuration or logs are retrieved
- **THEN** credentials, authorization headers, and document content are absent

### Requirement: Machine-readable automation feedback

The automation API SHALL return structured, self-describing feedback on every
outcome: finalize success payloads SHALL include the operation id, authorization
and rate-limit failures SHALL use the same `{error:{code,message}}` shape as
validation failures, and all response timestamps SHALL be UTC ISO-8601 with an
explicit `Z` offset.

#### Scenario: Finalize exposes its operation

- **WHEN** a scoped client finalizes a document (fresh or replayed)
- **THEN** the response payload includes the `operationId` that `GET /operations/{id}` accepts

#### Scenario: Authorization failures are machine-readable

- **WHEN** a request lacks a valid service token or the token lacks the endpoint's scope
- **THEN** the API returns 401/403 with a structured error body (`token_invalid`, `forbidden_scope`) and performs no action

#### Scenario: Rate-limited clients get actionable copy

- **WHEN** an automation client exceeds the automation rate-limit policy
- **THEN** the API returns 429 with a structured body whose message describes automation throttling, not login throttling

#### Scenario: Timestamps are unambiguous UTC

- **WHEN** any automation response includes a timestamp
- **THEN** it is serialized as UTC ISO-8601 with an explicit `Z` offset

### Requirement: Webhook secret rotation

The system SHALL let an owner rotate a subscription's signing secret without
changing its URL or event types; the new secret MUST be returned once and MUST
be used to sign all subsequent deliveries.

#### Scenario: Rotation preserves the subscription

- **WHEN** an owner rotates a subscription's secret
- **THEN** the response returns the new secret exactly once, the URL and event types are unchanged, and later deliveries verify only against the new secret

### Requirement: Machine-readable API contract

The system SHALL expose an OpenAPI 3.1 document describing every
`/api/v1/automation` endpoint (paths, methods, required headers, request and
response schemas including the structured error shape), serve it at
`GET /api/v1/automation/openapi.json`, and keep the committed copy in
`docs/openapi.json` identical to the served document.

#### Scenario: Client discovers the contract

- **WHEN** a client fetches `/api/v1/automation/openapi.json`
- **THEN** the response is a valid OpenAPI 3.1 document whose paths cover templates, preview, finalize, operations, documents, and their error schemas

#### Scenario: Contract cannot drift

- **WHEN** the served document and `docs/openapi.json` are compared
- **THEN** they are byte-identical, enforced by a test

