## Why

Strict owner-only isolation prevents legitimate review and delivery workflows, while ad-hoc file sharing loses revocation and access accountability. OpenDockify needs narrowly scoped sharing that preserves ownership and immutable document content.

## What Changes

- Add document grants for authenticated viewers and reviewers.
- Add revocable, expiring external view/download links with hashed secrets.
- Record append-only access and grant events without logging document contents or tokens.
- Add owner-facing Flutter sharing and access-history controls.

## Capabilities

### New Capabilities

- `document-sharing`: Owner-controlled authenticated grants, external share links, revocation, and access audit.

### Modified Capabilities

None.

## Non-goals

- No ownership transfer, public indexing, anonymous editing, real-time co-authoring, signing, email delivery, or organization-wide RBAC.

## Impact

- New sharing domain module and tables, authorization policies on document reads, public rate-limited endpoints, audit retention settings, and Flutter document detail actions.

