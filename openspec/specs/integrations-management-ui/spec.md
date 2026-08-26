# integrations-management-ui Specification

## Purpose
TBD - created by archiving change integrations-management-ui. Update Purpose after archive.
## Requirements
### Requirement: Service token management screen

The app SHALL let an owner view their service tokens (name, prefix, scopes,
expiry, usage) without displaying any verifier or clear value, create a token by
choosing a name, scopes, and expiry, reveal and copy the clear token exactly
once from the creation response, and revoke a token after an explicit
confirmation step.

#### Scenario: Clear token is shown once

- **WHEN** token creation succeeds
- **THEN** the clear token is displayed with a copy affordance and is no longer retrievable after the screen is dismissed

#### Scenario: Revocation requires confirmation

- **WHEN** the owner taps revoke on a token
- **THEN** a confirmation dialog is shown first and confirming removes it from the list

### Requirement: Webhook subscription management screen

The app SHALL let an owner list webhook subscriptions (URL, event types, active
state), create a subscription with an HTTPS URL and event types, delete a
subscription after confirmation, and rotate its secret with the new secret
revealed exactly once.

#### Scenario: Rotation reveals the new secret once

- **WHEN** rotation succeeds
- **THEN** the new secret is displayed with a copy affordance and is not stored or shown again afterwards

#### Scenario: Invalid destination is reported

- **WHEN** creation is rejected because the URL is not HTTPS or fails outbound-safety checks
- **THEN** the app surfaces the server's reason without losing the entered values

### Requirement: Delivery diagnostics and manual retry

The app SHALL let an owner list recent deliveries for their subscriptions with
state, attempt count, last status/error or blocked reason, and a bounded attempt
timeline, and SHALL offer manual retry that refreshes the delivery state.

#### Scenario: Owner retries an exhausted delivery

- **WHEN** the owner taps retry on a terminal delivery
- **THEN** the app requests the retry and reflects the reset pending state on refresh

#### Scenario: Diagnostics stay redacted

- **WHEN** deliveries are listed
- **THEN** no signing secrets, request bodies, or signatures are rendered

