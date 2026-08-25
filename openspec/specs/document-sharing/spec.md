# document-sharing Specification

## Purpose
TBD - created by archiving change document-sharing-access-control. Update Purpose after archive.
## Requirements
### Requirement: Authenticated document grants

The system SHALL let a document owner grant an existing user `view` or `review` access and SHALL keep ownership, content, metadata mutation, deletion, and finalization restricted to the owner.

#### Scenario: Viewer reads shared document

- **WHEN** a grantee with an active `view` grant requests document detail or PDF
- **THEN** the resource is returned without owner-only controls

#### Scenario: Grantee attempts mutation

- **WHEN** a grantee attempts rename, archive, delete, re-edit, or finalize
- **THEN** the request is forbidden and no state changes

#### Scenario: Owner revokes grant

- **WHEN** the owner revokes a grant
- **THEN** subsequent grantee access is denied immediately

### Requirement: Expiring external share links

The system SHALL allow an owner to create a random, single-purpose share link with expiry and view/download policy; it MUST store only a hash of the secret and MUST never expose the secret again after creation.

#### Scenario: Valid external view

- **WHEN** an unexpired, unrevoked link permits viewing
- **THEN** the public endpoint returns the immutable document view without exposing owner or internal identifiers beyond the shared resource

#### Scenario: Expired or revoked link

- **WHEN** a link is expired, revoked, malformed, or unknown
- **THEN** the endpoint returns the same not-found response and reveals no link state

#### Scenario: Download disabled

- **WHEN** a link permits view but not download
- **THEN** the PDF download endpoint denies the request

### Requirement: Sharing audit trail

The system SHALL append audit events for grant creation/revocation and successful or denied share access, including time, action, actor category, and coarse request metadata; it MUST NOT log tokens, answers, rendered text, or PDF content.

#### Scenario: Owner inspects access history

- **WHEN** the owner requests a document's sharing history
- **THEN** events are returned newest-first with bounded pagination

#### Scenario: Foreign history hidden

- **WHEN** a non-owner requests sharing history
- **THEN** the API returns `404 Not Found`

### Requirement: Abuse-resistant public access

The system SHALL rate-limit public share endpoints, use non-cacheable responses for private content, and support an administrator-configured maximum lifetime and emergency disable switch.

#### Scenario: Public limit exceeded

- **WHEN** a client exceeds the configured share-access limit
- **THEN** the API returns `429 Too Many Requests` without performing document retrieval

### Requirement: Flutter sharing controls

The Flutter application SHALL let owners create, list, copy once, revoke, and audit shares while clearly distinguishing internal grants from external links.

#### Scenario: Secret shown once

- **WHEN** an external link is created
- **THEN** the UI offers copy/share actions for that response and explains that the secret cannot be retrieved later

