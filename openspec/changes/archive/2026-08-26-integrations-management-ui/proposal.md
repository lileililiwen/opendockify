## Why

The automation capability shipped API-only: owners must curl to create tokens,
manage webhook subscriptions, or diagnose deliveries. The mobile app is the
primary self-hoster surface, so integration management is effectively invisible
to the people who own the resources.

## What Changes

- Add an Integrations section to the app (reachable from Settings) with three management surfaces:
  - Service tokens: redacted list with usage/expiry, create (name, scopes, expiry) with one-time clear-token reveal + copy, revoke with confirmation.
  - Webhook subscriptions: list, create (HTTPS URL + event types), delete, rotate secret with one-time reveal.
  - Deliveries: per-subscription log with state, status, and attempt timeline; manual retry.
- Extend the typed `ApiClient` with the `/api/integrations` endpoints, normalizing failures into the existing `ApiError`.

## Capabilities

### New Capabilities

- `integrations-management-ui`: owner-facing mobile management of service tokens, webhook subscriptions, and delivery diagnostics.

## Non-goals

- No in-app automation API calls using service tokens; no QR/pairing flows; no admin-surface changes.

## Impact

- New `lib/features/integrations/` feature module (models DTOs live in core/models), ApiClient additions,
  router entry, Settings entry point, widget/unit tests.
