## ADDED Requirements

### Requirement: Document library metadata

The system SHALL store a title and archive state for each generated document,
SHALL allow only its owner to update those fields, and MUST keep rendered content,
snapshot, PDF identity, template identity, ownership, and signing state immutable.

#### Scenario: New document receives a title

- **WHEN** a document is generated from a template
- **THEN** it receives the template name as its initial title and is active

#### Scenario: Owner renames and archives a document

- **WHEN** the owner updates a document with a non-blank title of at most 200
  characters and an archive state
- **THEN** the metadata is persisted without changing immutable document content

#### Scenario: Invalid title rejected

- **WHEN** the owner submits a blank title or a title longer than 200 characters
- **THEN** the API returns `400 Bad Request` and does not modify the document

#### Scenario: Foreign metadata update hidden

- **WHEN** a user attempts to update another user's document metadata
- **THEN** the API returns `404 Not Found` and does not modify the document

#### Scenario: Migrated document has a display title

- **WHEN** an existing document has no stored title after migration
- **THEN** API views use its template name as a non-destructive display fallback

### Requirement: Searchable and sortable owner library

The system SHALL expose an owner-scoped paginated document library whose summaries
include title, template id, template name, archive state, status, signing status,
parent id, and creation time; it SHALL support bounded search, archive filtering,
and deterministic sorting.

#### Scenario: Active library default

- **WHEN** an authenticated user lists documents without optional query parameters
- **THEN** only that user's active documents are returned newest-first

#### Scenario: Search title or template name

- **WHEN** a user supplies a search term of at most 100 characters
- **THEN** only their documents whose title or template name contains the term
  case-insensitively are returned

#### Scenario: Filter archived documents

- **WHEN** a user selects `archived` or `all` archive mode
- **THEN** the result contains respectively only archived documents or both active
  and archived documents owned by that user

#### Scenario: Sort library

- **WHEN** a user selects `newest`, `oldest`, or `title` sorting
- **THEN** results are ordered accordingly with document id as a deterministic
  tie-breaker

#### Scenario: Invalid query rejected

- **WHEN** search exceeds 100 characters or archive/sort has an unknown value
- **THEN** the API returns `400 Bad Request` and performs no unbounded query

#### Scenario: Library remains isolated

- **WHEN** two users use identical search, filter, and sort values
- **THEN** each response contains only documents owned by its authenticated user

### Requirement: Document version history

The system SHALL expose the owner-visible connected revision history of a document
using immutable parent relationships and SHALL order versions oldest-first.

#### Scenario: Re-edit inherits library title

- **WHEN** an owner re-edits a document
- **THEN** the new immutable version inherits the current title and remains linked
  to its parent

#### Scenario: View connected history

- **WHEN** an owner requests version history for any revision
- **THEN** every owner-visible ancestor, descendant, and branch connected to that
  revision is returned oldest-first with library summary metadata

#### Scenario: Foreign history hidden

- **WHEN** a user requests version history starting from another user's document
- **THEN** the API returns `404 Not Found` and reveals no revisions

### Requirement: Flutter document library controls

The Flutter application SHALL present document titles and template names and SHALL
provide search, archive filter, sorting, rename, archive/restore, and version
history navigation using the owner-scoped API.

#### Scenario: Search and refine library

- **WHEN** a user enters search text or changes archive/sort controls
- **THEN** pagination resets and the list clearly reflects the active criteria

#### Scenario: Rename from document detail

- **WHEN** a user confirms a valid new title
- **THEN** the detail and library refresh with the new title

#### Scenario: Archive and restore

- **WHEN** a user archives or restores a document
- **THEN** its state changes and it appears only under the matching library filter

#### Scenario: Navigate versions

- **WHEN** a document has related revisions and the user opens version history
- **THEN** revisions are shown oldest-first and selecting one opens its detail
