# Document sharing operations and privacy

Authenticated grants are limited to `view` and `review`. They never transfer ownership and do not authorize
rename, archive, re-edit, delete, finalization, or share administration. Revocation is effective on the next
request.

External links contain a 256-bit secret shown only in the create response. The database stores an HMAC-SHA-256
digest, never the secret. Configure `Sharing:HashKey` with a deployment secret distinct from database backups;
the JWT secret is used as a compatibility fallback. Public responses set `Cache-Control: private, no-store`,
`Content-Security-Policy`, `Referrer-Policy`, and `X-Content-Type-Options`, and are limited per client address.

Administrators can change these allowlisted settings through the existing system-settings API:

- `Sharing.Enabled`: emergency external-access switch; defaults to `true`.
- `Sharing.MaximumLifetimeHours`: maximum owner-selected link lifetime; defaults to 72 hours.
- `Sharing.AuditRetentionDays`: retention for access events and expired link records; defaults to 90 days.

Audit events contain action, success state, actor category/identifier when authenticated, timestamp, and a
truncated coarse client fingerprint. They never contain share tokens, document answers, rendered content, or
PDF bytes. Owners can inspect bounded, newest-first pages. Invalid, expired, revoked, and malformed public links
return the same 404 response.
