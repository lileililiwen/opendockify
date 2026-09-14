# storage-abstraction Specification

## Purpose
Object-storage boundary for PDFs and backup bundles so deployers can keep the zero-setup local default or move artifacts to an S3-compatible endpoint (MinIO or AWS) without changing external download URLs. Persists through `IObjectStorage` with atomic local writes, range-capable downloads (`Accept-Ranges: bytes`, `206`/`416`), and a 15-minute presigned fetch for the automation API.
## Requirements
### Requirement: Object storage for PDFs and backups

The system SHALL persist PDFs and backup bundles through `IObjectStorage`; local atomic storage SHALL be the zero-setup default and S3-compatible storage SHALL be selectable via config.

#### Scenario: PDF round-trips via storage

- **WHEN** a document is finalized with `Storage:Provider=local`
- **THEN** the PDF is stored under `pdfs/{userId}/{docId}.pdf` and `GET /api/documents/{id}/download` returns identical bytes with `Accept-Ranges: bytes`

#### Scenario: S3 selectable

- **WHEN** the deployer sets `Storage:Provider=s3` with valid endpoint/bucket
- **THEN** finalize stores to S3 and `/api/admin/operations/status` reports storage provider `s3` healthy without secrets

### Requirement: Range and presigned download

The system SHALL support range requests and presigned automation PDF fetch.

#### Scenario: Range resume

- **WHEN** a client sends `Range: bytes=0-1023` to the download endpoint
- **THEN** the API returns `206` with the requested byte range and correct `Content-Range`

#### Scenario: Presigned automation fetch

- **WHEN** an automation caller GETs `/api/v1/automation/documents/{id}/pdf`
- **THEN** the call succeeds via a 15-minute presigned storage operation without exposing bucket credentials

