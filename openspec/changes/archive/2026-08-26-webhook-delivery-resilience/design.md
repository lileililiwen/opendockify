## Context

`WebhookDeliveryService.DeliverAsync` maps every failed route plan to
`Blocked`, including `dns-resolution-failed`. Sender-level errors (timeout,
connection refused) already retry as `Pending`. Delivery rows store only
`LastStatusCode`/`LastError`. Secrets are immutable on `WebhookSubscription`.

## Goals / Non-Goals

**Goals:** rotate secrets in place; auto-retry transient failures; bounded
attempt history; no signing or budget changes.

**Non-Goals:** dual-secret verification windows; per-subscription retry tuning;
UI.

## Decisions

- Rotation: new service method generates a fresh `whsec_…`, updates the row, and
  returns it once through a management endpoint (`POST …/subscriptions/{id}/rotate-secret`,
  owner-scoped 404 otherwise). Old secret invalid immediately — documented.
- Transient classification: a static set `{dns-resolution-failed}` for plan
  failures plus existing sender-error path stay `Pending` with backoff;
  everything else from the planner (scheme/prohibited ranges) remains `Blocked`.
- Attempt timeline: append-only JSON column `AttemptLog` on `WebhookDelivery`
  (max 5 entries, newest last: `{atUtc, statusCode?, error?}`), surfaced as a
  typed array in `WebhookDeliveryView`; one additive migration.

## Risks / Trade-offs

- [JSON column instead of child table] -> bounded to 5 entries and read-only for
  receivers of the view; avoids join fan-out for a diagnostic feature.
- [Old secret dies at rotation instant] -> in-flight retries signed with the old
  secret fail receiver verification once; documented as rotate-during-quiet-window.

## Migration Plan

Additive column + endpoint; no behavior change for healthy deliveries.

## Open Questions

- None.
