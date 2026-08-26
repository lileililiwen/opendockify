## ADDED Requirements

### Requirement: Webhook secret rotation

The system SHALL let an owner rotate a subscription's signing secret without
changing its URL or event types; the new secret MUST be returned once and MUST
be used to sign all subsequent deliveries.

#### Scenario: Rotation preserves the subscription

- **WHEN** an owner rotates a subscription's secret
- **THEN** the response returns the new secret exactly once, the URL and event types are unchanged, and later deliveries verify only against the new secret

## MODIFIED Requirements

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
