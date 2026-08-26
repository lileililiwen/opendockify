## Context

The app uses Riverpod + go_router with a single typed `ApiClient` (dio) that
maps failures to `ApiError` kinds; admin screens already model list+create+
delete flows against owner-scoped endpoints. The backend surface is complete:
`/api/integrations/tokens`, `/webhooks/subscriptions` (+`rotate-secret`),
`/webhooks/deliveries` (+retry).

## Goals / Non-Goals

**Goals:** parity with the management API; one-time secret reveal patterns;
confirmation before destructive actions; redaction by construction.

**Non-Goals:** using service tokens from the app itself; editing event-type
catalogs; push notifications for deliveries.

## Decisions

- DTOs in `lib/core/models/integrations.dart`; client methods appended to
  `ApiClient` following the existing `_mapError` pattern.
- Feature module `lib/features/integrations/`: `providers.dart` (Riverpod
  controllers), `presentation/integrations_screen.dart` (tabbed: Tokens /
  Webhooks / Deliveries), create sheets as modal bottom sheets, secrets shown
  via a one-time dialog with copy button that dismisses permanently.
- Route `/integrations` + Settings entry ("Integrations", visible to any signed-in
  owner); deliveries tab filters by selected subscription.
- Destructive actions (revoke/delete) use `AlertDialog` confirmation; retry and
  rotate are non-destructive but rotate still confirms because it invalidates
  the old secret.

## Risks / Trade-offs

- [Secrets visible on screen] -> mitigated: shown once in a dialog requiring
  explicit dismissal, with copy button; never persisted, never in lists.
- [Delivery polling] -> manual refresh only in this change; no background poll.

## Migration Plan

Additive screens behind normal navigation; no data migration.

## Open Questions

- None.
