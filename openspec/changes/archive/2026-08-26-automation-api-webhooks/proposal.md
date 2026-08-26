## Why

OpenDockify’s user JWT endpoints require interactive orchestration and provide no completion signal, blocking reliable integration with local CRM, case-management, or batch systems. Self-hosted automation needs scoped machine credentials, idempotent operations, and observable outbound events.

## What Changes

- Add owner-scoped service tokens with explicit permissions, expiry, rotation, and revocation.
- Add a versioned automation API for template discovery, preview, finalization, and document retrieval.
- Add idempotency keys for mutating automation requests.
- Add signed webhooks with retry, replay protection, delivery logs, and SSRF-safe destination validation.

## Capabilities

### New Capabilities

- `automation-integrations`: Scoped machine authentication, stable API contracts, idempotency, and secure webhook delivery.

### Modified Capabilities

None.

## Non-goals

- No public SaaS relay, workflow builder, arbitrary callback headers/code, e-signature automation, or access beyond the token owner.

## Impact

- New integrations module and worker, token/delivery persistence, `/api/v1/automation` endpoints, outbound-network security configuration, API documentation, and admin/client controls.

