## ADDED Requirements

### Requirement: Document generation

The app SHALL generate a document by POSTing the filled `values` map and
`selectedClauseIds` to `POST /api/documents/generate`, and SHALL show the
returned rendered text, any non-blocking warnings, and a download action.

#### Scenario: Generate happy path

- **WHEN** the user submits a valid fill-in form
- **THEN** the document is generated and the detail screen shows the rendered
  text and the `downloadUrl`

#### Scenario: Validation failure

- **WHEN** the API returns `400 Bad Request`
- **THEN** the app shows the backend validation error on the form and the user
  can correct and resubmit

#### Scenario: Warnings shown

- **WHEN** generation returns `warnings` (e.g. interest rate above LPR)
- **THEN** the warnings are displayed as a non-blocking notice on the generated
  document

#### Scenario: Forbidden or not found

- **WHEN** the API returns `403`/`404`
- **THEN** the app shows the backend error message and returns to the template
  list

### Requirement: Document list

The app SHALL list the current user's documents with pagination
(`GET /api/documents?page=&pageSize=`), showing creation time, status, and
template reference, with a refresh action and paging controls.

#### Scenario: First page loads

- **WHEN** the user opens the Documents screen
- **THEN** the first page of documents is fetched and displayed

#### Scenario: Pagination

- **WHEN** there are more pages
- **THEN** the app loads further pages on demand and shows the current page
  indicator

#### Scenario: Empty list

- **WHEN** the user has no documents
- **THEN** an empty state with a "create document" call-to-action is shown

### Requirement: Document detail

The app SHALL fetch a document via `GET /api/documents/{id}` and display its
rendered text, risk notice, creation time, and status.

#### Scenario: Open document

- **WHEN** the user taps a list item
- **THEN** the full document view (rendered text + metadata) is shown

#### Scenario: Foreign or missing document

- **WHEN** the API returns `404`
- **THEN** the app shows "document not found" and returns to the list

### Requirement: PDF download and sharing

The app SHALL download the PDF from the absolute-ized `downloadUrl` and SHALL
offer to open it in the platform viewer and/or share it via the platform share
sheet. PDF bytes SHALL be written to the app documents directory and never held
in memory beyond the download buffer.

#### Scenario: Download and open

- **WHEN** the user taps "Download PDF"
- **THEN** the PDF is downloaded and presented via the platform viewer or share
  sheet

#### Scenario: Download failure

- **WHEN** the download fails or returns a non-PDF response
- **THEN** the app shows an error and offers retry

### Requirement: Re-edit (immutable history)

The app SHALL support re-editing a document via
`POST /api/documents/{id}/reedit`, which SHALL create a new document whose
parent is the original; the original SHALL remain unchanged and the app SHALL
navigate to the new document.

#### Scenario: Re-edit creates new version

- **WHEN** the user re-edits a document and submits
- **THEN** a new document is created (linked to the original as parent) and the
  app shows the new document

#### Scenario: Original preserved

- **WHEN** the re-edited document is saved
- **THEN** the original document's rendered text and snapshot are unchanged in
  the list

### Requirement: Document deletion

The app SHALL let the owner delete a document after an explicit confirmation
(`DELETE /api/documents/{id}`) and SHALL update the list on success.

#### Scenario: Delete confirmed

- **WHEN** the user confirms deletion of their document
- **THEN** the API returns `204` and the document disappears from the list

#### Scenario: Delete cancelled

- **WHEN** the user cancels the confirmation dialog
- **THEN** no request is made and the document stays

#### Scenario: Foreign document delete denied

- **WHEN** the user tries to delete a document they do not own
- **THEN** the API returns `404` and the app shows the error