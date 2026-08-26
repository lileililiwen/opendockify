## Context

The API has user JWT routes but no non-interactive credential lifecycle, idempotency store, event outbox, or webhook worker. DocuSeal exposes APIs/webhooks and Documenso documents a resource-oriented documents API. The self-hosted trust boundary requires stricter outbound controls than a generic SaaS integration.

Research: https://github.com/docusealco/docuseal and https://github.com/documenso/documenso/blob/main/apps/docs/content/docs/developers/api/documents.mdx

## Goals / Non-Goals

**Goals:** least-privilege machine access, stable versioning, exactly-once mutation effects under retry, durable signed events, and SSRF resistance.

**Non-Goals:** public relay, arbitrary scripting, general workflow automation, or signing automation.

## Decisions

- Create `OpenDockify.Integrations` for tokens, idempotency records, subscriptions, outbox events, and deliveries.
- Use opaque 256-bit service tokens with a public prefix/id for lookup and an Argon2id or keyed SHA-256 verifier appropriate to high-entropy secrets. Return clear values once.
- Namespace routes under `/api/v1/automation`; reuse template and generation services instead of duplicating domain behavior.
- Commit mutation result plus idempotency record and outbox event in the same database transaction. Persist a request digest and replay the serialized stable response.
- Sign exact webhook bytes with per-subscription secret, event id, and timestamp. A background worker claims deliveries with database leases and bounded exponential retry.
- Resolve and validate every address before connection and every redirect; disable redirects by default. Deployer allowlists are explicit CIDR/host entries.

## Risks / Trade-offs

- [Webhook SSRF reaches internal services] -> deny non-public destinations by default, pin validated resolution for the connection, and document egress firewall defense in depth.
- [Duplicate documents on timeout] -> mandatory idempotency keys and transactional result storage.
- [Worker crashes duplicate delivery] -> stable event ids and at-least-once delivery contract; receivers verify ids/timestamps.
- [Long-lived tokens leak] -> expiry required, scopes, one-time display, immediate revocation, usage history, and no query-string credentials.

## Migration Plan

Add integration tables with automation disabled by default. Enable token endpoints first, then API, then webhooks after worker health is visible.

## Open Questions

- Bulk generation remains a separate change built on this idempotent API.

