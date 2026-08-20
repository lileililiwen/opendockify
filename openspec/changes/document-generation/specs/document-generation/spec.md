## ADDED Requirements

### Requirement: Document record

The system SHALL persist generated documents with: id, owner id, template id,
status, a parameter snapshot (filled values + selected clause ids), the
rendered full text (risk notice appended), the PDF binary location, creation
timestamp, and optional parent id linking to the document it was re-edited
from.

#### Scenario: Generation creates a record

- **WHEN** a user generates a document from a template
- **THEN** a document record is persisted with the snapshot, rendered text, PDF
  reference, and the owner set to the user

#### Scenario: Record carries template id

- **WHEN** a document is read
- **THEN** the originating template id is available on the record

### Requirement: Generation validation

The system SHALL validate that all required fields have values and that
provided values satisfy their validation rules (non-negative amounts,
interest-rate range) BEFORE rendering; interest-rate warnings SHALL be
returned with the result but SHALL NOT block generation.

#### Scenario: Missing required field blocks generation

- **WHEN** a user generates with a required field left empty
- **THEN** generation fails with `400 Bad Request` listing the missing fields
  and no document is created

#### Scenario: Invalid amount blocks generation

- **WHEN** a currency field is negative or malformed
- **THEN** generation fails with `400 Bad Request`

#### Scenario: Over-cap interest warns but generates

- **WHEN** the interest rate exceeds 4× LPR
- **THEN** generation succeeds and the response includes a warning about the
  rate; the PDF is still produced

### Requirement: Clause selection

The system SHALL insert into the rendered text only the optional clauses whose
toggle is `true`, in the template's declared order.

#### Scenario: Selected clauses included

- **WHEN** a user toggles clauses A and C on and clause B off
- **THEN** the rendered text contains A and C and does not contain B

#### Scenario: No clauses selected

- **WHEN** all optional clauses are toggled off
- **THEN** the rendered text contains only the body

### Requirement: Risk notice appended

The system SHALL append the template's risk-warning text at the end of every
generated document's rendered text and PDF.

#### Scenario: Risk notice present

- **WHEN** a document is generated
- **THEN** the rendered text ends with the template's risk-notice content

### Requirement: Immutable history and re-edit

The system SHALL treat generated documents as immutable; re-editing SHALL
create a new document record whose parent id references the original, and the
original record SHALL remain unchanged.

#### Scenario: Re-edit creates new record

- **WHEN** a user re-edits a generated document with changed values
- **THEN** a new document record is created with `parentId` = original id and
  the original record is unchanged

#### Scenario: Original remains intact

- **WHEN** the original document is read after a re-edit
- **THEN** its snapshot, rendered text, and PDF are exactly as first generated

### Requirement: Document listing

The system SHALL return a paginated list of the authenticated user's documents
(newest first), scoped so a user never sees another user's documents.

#### Scenario: Paginated own list

- **WHEN** a user requests `/api/documents?page=1&pageSize=20`
- **THEN** only their own documents are returned, ordered newest first, with
  pagination metadata

#### Scenario: Foreign document hidden

- **WHEN** a user requests a document owned by another user
- **THEN** the API returns `404 Not Found`

### Requirement: Document deletion

The system SHALL allow a user to delete their own document, removing the record
and its stored PDF blob.

#### Scenario: Delete own document

- **WHEN** an owner deletes a document
- **THEN** the record and its PDF blob are removed

#### Scenario: Delete foreign document denied

- **WHEN** a user attempts to delete a document they do not own
- **THEN** the API returns `404 Not Found` and nothing is deleted
