# Automation API & Webhooks

Machine-to-machine access to OpenDockify: scoped service tokens, a stable
versioned API under `/api/v1/automation`, mandatory idempotency for
mutations, and signed webhook delivery with SSRF-safe outbound validation.

All automation requests act **only** on the token owner's resources.

---

## 1. Service tokens

Create and manage tokens with your normal user JWT:

```http
POST /api/integrations/tokens
Authorization: Bearer <user-jwt>
Content-Type: application/json

{
  "name": "ci-runner",
  "scopes": ["templates:read", "documents:preview", "documents:write", "operations:read"],
  "expiresInDays": 90
}
```

- `201` returns the **clear token exactly once**: `{"token": {...}, "clearToken": "odk_<prefix>_<secret>"}`.
  Only a keyed SHA-256 verifier is stored; a lost clear token cannot be recovered — revoke and reissue.
- `GET /api/integrations/tokens` lists your tokens (name, prefix, scopes, expiry, usage counters). Verifiers are never returned.
- `DELETE /api/integrations/tokens/{id}` revokes immediately.
- Limits: at most `Integrations:MaxTokensPerOwner` (default 20) tokens per owner; lifetime capped at 3650 days.

### Scopes

| Scope | Grants |
|---|---|
| `templates:read` | List usable templates |
| `documents:preview` | Validate answers / preview rendering |
| `documents:write` | Finalize documents (mutation, idempotent) |
| `documents:read` | Retrieve finalized document metadata + PDF |
| `operations:read` | Check operation status |

Requests outside a token's scopes, or after expiry/revocation, return `401/403` and perform no action.

---

## 2. Automation API (`/api/v1/automation`)

Authenticate with `Authorization: Bearer odk_...`. Requests are rate limited
(60/min per token).

| Endpoint | Scope | Description |
|---|---|---|
| `GET /api/v1/automation/templates` | `templates:read` | Templates the owner can generate from |
| `POST /api/v1/automation/preview` | `documents:preview` | Validate answers, render preview text |
| `POST /api/v1/automation/finalize` | `documents:write` | Create one document (**requires `Idempotency-Key`**) |
| `GET /api/v1/automation/operations/{id}` | `operations:read` | Status/outcome of a finalize operation |
| `GET /api/v1/automation/documents/{id}` | `documents:read` | Owner-visible document metadata |
| `GET /api/v1/automation/documents/{id}/pdf` | `documents:read` | Download the finalized PDF |

### Finalize example

```http
POST /api/v1/automation/finalize
Authorization: Bearer odk_xxxx_yyyy...
Idempotency-Key: order-2026-08-26-0001
Content-Type: application/json

{
  "templateId": "0f0e0d0c-1111-2222-3333-444455556666",
  "values": { "amount": "12000", "borrower": "Acme Ltd" },
  "selectedClauseIds": []
}
```

Success — `201 Created`, body shape (stable contract):

```json
{
  "documentId": "aa00...",
  "operationId": "bb11...",
  "templateId": "0f0e...",
  "templateName": "Loan IOU",
  "title": "Loan IOU",
  "contentSha256": "9f2a...",
  "warnings": [],
  "createdAtUtc": "2026-08-26T00:00:00Z"
}
```

### Idempotency rules

- The key binds to **(token, route, request digest)** for `Integrations:IdempotencyRetentionDays` (default 30).
- Same token + key + same body → the original status and response bytes are replayed; no second document is created.
- Same token + key + different body → `409 Conflict`
  (`error.code = "conflict_idempotency_key"`); no mutation.
- Keys are opaque strings of 8–128 ASCII letters/digits/`-`/`_`.
- The mutation result, idempotency record, and webhook outbox event commit in
  **one database transaction**.

### Structured errors

Validation failures return `422` with machine-readable field errors and create nothing:

```json
{
  "error": {
    "code": "validation_failed",
    "message": "One or more fields are invalid.",
    "fields": [
      { "field": "amount", "error": "Field 'amount' must not be negative." }
    ]
  }
}
```

Other codes: `missing_idempotency_key` (400), `invalid_idempotency_key` (400),
`invalid_request` (400), `token_invalid` (401), `forbidden_scope` (403),
`not_found` (404), `conflict_idempotency_key` (409), `rate_limited` (429),
`render_error` (500).

Use the returned `operationId` with `GET /api/v1/automation/operations/{id}`
to check an operation later. All timestamps are UTC ISO-8601 with a `Z` offset.

---

## 3. Webhooks

> Outbound delivery is **disabled by default**. Enable it explicitly:
> `Integrations__Webhooks__Enabled=true`.

### Subscriptions

```http
POST /api/integrations/webhooks/subscriptions
Authorization: Bearer <user-jwt>
Content-Type: application/json

{ "url": "https://crm.example.com/hooks/opendockify", "eventTypes": ["document.finalized"] }
```

- `201` returns the signing **secret exactly once**: `{"subscription": {...}, "secret": "whsec_..."}`.
- Destinations are validated at creation and re-validated on every attempt (see §5).
- `GET /api/integrations/webhooks/subscriptions` — list (no secrets).
- `DELETE /api/integrations/webhooks/subscriptions/{id}` — remove.
- Limits: at most `Integrations:MaxSubscriptionsPerOwner` (default 10) per owner.

### Event envelope (`document.finalized`)

The delivered body is canonical JSON (sign these exact bytes):

```json
{
  "eventId": "6b5f...",
  "type": "document.finalized",
  "occurredAtUtc": "2026-08-26T00:00:01.20Z",
  "ownerId": "1111...",
  "data": {
    "documentId": "aa00...",
    "templateId": "0f0e...",
    "templateName": "Loan IOU",
    "title": "Loan IOU",
    "contentSha256": "9f2a...",
    "warnings": [],
    "createdAtUtc": "2026-08-26T00:00:00Z"
  }
}
```

Delivery headers:

| Header | Meaning |
|---|---|
| `X-OpenDockify-Event-Id` | Stable event id — deduplicate on it (delivery is at-least-once) |
| `X-OpenDockify-Event-Type` | e.g. `document.finalized` |
| `X-OpenDockify-Delivery` | Per-attempt delivery id |
| `X-OpenDockify-Signature` | `t=<unix-seconds>, v1=<hex>` |

### Signature verification

`v1 = HMAC-SHA256(secret, "{eventId}.{timestamp}.{exactBody}")` (hex, lowercase).

Python receiver example:

```python
import hmac, hashlib, time

def verify(secret, event_id, body: bytes, header: str, tolerance=300):
    parts = dict(p.strip().split("=", 1) for p in header.split(","))
    if abs(time.time() - int(parts["t"])) > tolerance:
        return False
    expected = hmac.new(
        secret.encode(),
        f'{event_id}.{parts["t"]}.'.encode() + body,
        hashlib.sha256,
    ).hexdigest()
    return hmac.compare_digest(expected, parts["v1"])
```

Respond `2xx` to acknowledge. Retries use bounded exponential backoff
(`BaseRetryDelaySeconds * 2^(attempt-1)`, capped at 1 hour) up to
`MaxAttempts` (default 8); then the delivery is marked `Exhausted`.
Inspect deliveries via `GET /api/integrations/webhooks/deliveries` and
re-trigger with `POST /api/integrations/webhooks/deliveries/{id}/retry`.
Terminal delivery records are retained for `RetentionDays` (default 30).

---

## 4. Outbound request safety (SSRF)

Every destination — configured or redirect hop — must be **HTTPS**, resolves
to public addresses only, and connects to the validated address (pinned, so
DNS rebinding between check and connect fails closed). Blocked before any
bytes leave the host:

loopback · link-local (incl. cloud metadata `169.254.169.254`) · private
(RFC1918, `fc00::/7`) · shared address space (`100.64/10`) · multicast /
reserved / broadcast · unspecified.

Blocked destinations are recorded as `Blocked` with the reason and never retried automatically.

Deployers can punch explicit holes for internal receivers:

```json
"Integrations": {
  "Webhooks": {
    "Enabled": true,
    "Allowlist": [ "10.0.0.0/8", "metrics.internal" ]
  }
}
```

Defense in depth: also firewall egress from the OpenDockify container so it
can only reach intended segments.

---

## 5. Configuration reference

| Key | Default | Meaning |
|---|---|---|
| `Integrations:Webhooks:Enabled` | `false` | Master switch for outbound delivery |
| `Integrations:Webhooks:MaxAttempts` | `8` | Bounded retry budget |
| `Integrations:Webhooks:BaseRetryDelaySeconds` | `30` | Exponential backoff base |
| `Integrations:Webhooks:TimeoutSeconds` | `15` | Per-attempt HTTP timeout |
| `Integrations:Webhooks:LeaseMinutes` | `5` | Worker lease; expired leases are reclaimed after crashes |
| `Integrations:Webhooks:RetentionDays` | `30` | Terminal delivery record retention |
| `Integrations:Webhooks:MaxRedirects` | `3` | Redirect hops (each re-validated) |
| `Integrations:Webhooks:Allowlist` | `[]` | Hostnames / CIDRs exempt from prohibited-range blocking |
| `Integrations:IdempotencyRetentionDays` | `30` | Replay window for operations |
| `Integrations:TokenPepper` | derived from `Jwt:Secret` | Keying material for token verifiers |

Secrets never appear in listings, logs, or delivery records: token verifiers,
clear tokens, subscription secrets, request bodies, and signatures are all excluded.
