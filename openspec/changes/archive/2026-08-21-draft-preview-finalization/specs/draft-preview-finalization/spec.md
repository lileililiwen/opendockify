## ADDED Requirements

### Requirement: Non-persisting document preview

The system SHALL expose an authenticated preview operation that applies the same
template access, input validation, interest warnings, clause selection, rendering,
and mandatory risk notice as finalization, but MUST NOT create a document row or
PDF file.

#### Scenario: Valid preview

- **WHEN** an owner previews a complete valid template snapshot
- **THEN** the API returns rendered text, template name, and warnings without a
  document id or download URL

#### Scenario: Invalid preview

- **WHEN** preview input is incomplete, invalid, or references an inaccessible
  template
- **THEN** the API returns the same validation/not-found status as finalization and
  creates no persistent artifact

#### Scenario: Preview matches final content

- **WHEN** unchanged preview input is subsequently finalized
- **THEN** the finalized document's rendered text is identical to the preview text

### Requirement: Explicit immutable finalization

The system SHALL expose explicit finalization that creates exactly one immutable
document and PDF after successful preview-path validation, while retaining the
legacy generate route as a compatibility alias.

#### Scenario: Finalize new document

- **WHEN** a user finalizes a valid template snapshot
- **THEN** one generated document and PDF are created and returned with warnings

#### Scenario: Legacy generate remains compatible

- **WHEN** an existing client posts the same valid request to `/generate`
- **THEN** it receives the same finalization behavior and response contract

#### Scenario: Re-edit finalization remains immutable

- **WHEN** a user previews and finalizes a re-edit
- **THEN** a new version is created with its existing parent/title inheritance and
  the original remains unchanged

### Requirement: Private recoverable Flutter drafts

The Flutter application SHALL securely autosave bounded form values and selected
clauses locally, scoped by authenticated user and form identity, SHALL restore them
when the same form is reopened, and SHALL clear them only after successful
finalization or explicit discard.

#### Scenario: Form changes autosave

- **WHEN** a signed-in user changes a field or optional clause
- **THEN** the latest snapshot is saved after a short debounce without requiring
  the form to be valid

#### Scenario: Draft restored for same user and form

- **WHEN** the same user reopens that template or re-edit form
- **THEN** locally saved values and clause selections replace the initial values

#### Scenario: Draft isolated between users

- **WHEN** another user opens the same template on the device
- **THEN** the first user's local draft is not loaded

#### Scenario: Failed finalization retains draft

- **WHEN** preview or finalization fails
- **THEN** the local draft remains recoverable

#### Scenario: Successful finalization clears draft

- **WHEN** finalization succeeds
- **THEN** the matching local draft is deleted before the result is presented

### Requirement: Flutter preview and finalize workflow

The Flutter form SHALL provide distinct Preview and Finalize actions, SHALL show
rendered preview text and warnings, and SHALL prevent duplicate submissions while
either operation is running.

#### Scenario: Review preview

- **WHEN** a user selects Preview with valid form data
- **THEN** a review surface shows the exact rendered text and every server warning
  without navigating to a document detail

#### Scenario: Finalize deliberately

- **WHEN** a user selects Finalize and the server succeeds
- **THEN** the generated result is shown and can be opened from the document library
