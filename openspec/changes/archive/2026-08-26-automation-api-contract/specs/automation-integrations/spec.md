## ADDED Requirements

### Requirement: Machine-readable API contract

The system SHALL expose an OpenAPI 3.1 document describing every
`/api/v1/automation` endpoint (paths, methods, required headers, request and
response schemas including the structured error shape), serve it at
`GET /api/v1/automation/openapi.json`, and keep the committed copy in
`docs/openapi.json` identical to the served document.

#### Scenario: Client discovers the contract

- **WHEN** a client fetches `/api/v1/automation/openapi.json`
- **THEN** the response is a valid OpenAPI 3.1 document whose paths cover templates, preview, finalize, operations, documents, and their error schemas

#### Scenario: Contract cannot drift

- **WHEN** the served document and `docs/openapi.json` are compared
- **THEN** they are byte-identical, enforced by a test
