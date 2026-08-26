## Why

Webhook secrets cannot be rotated without deleting and recreating a subscription
(delivery gap), transient DNS/network blips permanently mark deliveries `Blocked`
even though they are self-healing, and delivery records expose only the last
attempt — making exhausted webhooks undiagnosable.

## What Changes

- Add owner-scoped webhook secret rotation that returns the new secret once, without touching the subscription URL or event types.
- Distinguish transient failures (DNS resolution, timeouts, connection errors) which keep auto-retrying from policy violations which block.
- Record a bounded per-attempt timeline on each delivery and expose it in delivery views.

## Capabilities

### Modified Capabilities

- `automation-integrations`: Signed webhook delivery resilience and observability tightened; outbound request safety clarified.

## Non-goals

- No dual-secret grace window, no UI, no changes to signing format or retry budget defaults.

## Impact

- `OpenDockify.Integrations` delivery service/models (+ one migration for the attempt log column),
  management endpoints (rotate route), docs, unit tests.
