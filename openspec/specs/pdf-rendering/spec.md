# pdf-rendering Specification

## Purpose
TBD - created by archiving change document-generation. Update Purpose after archive.

## Requirements


### Requirement: Text to PDF rendering

The system SHALL render document text to a PDF using QuestPDF, with embedded
CJK/Chinese font support so Chinese characters display correctly.

#### Scenario: Generate PDF from rendered text

- **WHEN** a document's rendered text (including risk notice) is submitted to
  the PDF renderer
- **THEN** a PDF binary is produced whose content matches the text and renders
  Chinese characters correctly

#### Scenario: Missing CJK font fails clearly

- **WHEN** the runtime environment lacks a CJK font
- **THEN** rendering fails with a clear error instructing the operator to
  install fonts (the Docker image pre-installs them)

### Requirement: PDF download

The system SHALL let the owner download the generated PDF binary for a
document.

#### Scenario: Download PDF

- **WHEN** the owner calls `GET /api/documents/{id}/download`
- **THEN** the PDF binary is returned with an appropriate
  `application/pdf` content type

#### Scenario: Foreign download denied

- **WHEN** a user calls download on a document they do not own
- **THEN** the API returns `404 Not Found`

### Requirement: Existing-PDF overlay seam (iText7)

The system SHALL provide an interface for overlaying content (e.g. seals or
stamps) on existing PDFs (needed for cross-page seals in a future
e-signature flow), SHALL NOT implement the overlay in the MVP, and SHALL
document the AGPL license implications of the optional iText7 dependency in
code comments wherever the seam is referenced.

#### Scenario: Seam exists but is inert

- **WHEN** the renderer module is inspected
- **THEN** an `IPdfOverlay` interface exists with a no-op default
  implementation and comments stating iText7 is AGPL-licensed and that
  legally-reliable stamping requires external CA/timestamping

#### Scenario: iText dependency removable

- **WHEN** the overlay feature is unused
- **THEN** no iText7 package is required to build or run the system

### Requirement: PDF storage location

The system SHALL store the generated PDF binary at a stable, owner-scoped
location (filesystem path or blob key) recorded on the document record so it
can be served and deleted.

#### Scenario: Binary persisted

- **WHEN** a document is generated
- **THEN** the PDF bytes exist at the recorded location and the record
  references it

#### Scenario: Delete removes binary

- **WHEN** a document is deleted
- **THEN** the stored PDF binary is removed as well
