## Context

`DocumentService` currently applies owner filters everywhere, which is the secure baseline to preserve. Paperless-ngx provides per-document permissions and expiring share links; ONLYOFFICE DocSpace distinguishes authenticated collaboration rooms from public view-only access. OpenDockify needs only document review/delivery, not general co-authoring.

Research: https://github.com/paperless-ngx/paperless-ngx/blob/dev/docs/usage.md and https://github.com/ONLYOFFICE/DocSpace

## Goals / Non-Goals

**Goals:** least-privilege viewing/review, revocable external delivery, non-enumerability, and useful access audit.

**Non-Goals:** ownership transfer, anonymous mutation, office co-authoring, signing, or organization RBAC.

## Decisions

- Create `OpenDockify.Sharing` with grants, external links, and append-only access events. Keep document ownership unchanged.
- Centralize `CanReadDocument` authorization so detail/PDF/version reads cannot diverge. Owner-only methods continue to require ownership directly.
- Generate 256-bit external secrets, return once, store a keyed hash, use constant-time comparison, and make invalid/expired/revoked responses indistinguishable.
- Render public views from immutable stored artifacts. Set `Cache-Control: private, no-store`, restrictive CSP, referrer policy, and content disposition according to link policy.
- Log coarse IP prefix or deployer-selected privacy-safe fingerprint, not raw secrets or content. Bound retention and pagination.

## Risks / Trade-offs

- [Shared legal data leaks through links] -> expiry limits, instant revocation, one-time secret display, download policy, rate limits, and emergency global disable.
- [Authorization refactor weakens isolation] -> deny by default and add endpoint-level owner/grantee/stranger matrices.
- [Audit data becomes personal data] -> minimal fields, configured retention, owner/admin access only.

## Migration Plan

Add isolated sharing tables and read policies; no existing document becomes shared. Deploy authenticated grants before enabling public routes.

## Open Questions

- Reviewer comments should be a separate follow-up after access-control use is validated.

