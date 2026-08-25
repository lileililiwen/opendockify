## ADDED Requirements

### Requirement: Canonical template package

The system SHALL export a template as a UTF-8 JSON package containing format version, stable template identity, metadata, definition, provenance, and a SHA-256 content digest; exports MUST be deterministic and MUST contain no user or secret data.

#### Scenario: Export private template

- **WHEN** the owner exports a private template
- **THEN** the API streams a bounded package whose digest verifies against its canonical content

#### Scenario: Built-in template export

- **WHEN** an administrator exports a built-in template
- **THEN** the package identifies it as built-in-derived without granting edit rights on import

### Requirement: Validate before import

The system SHALL parse imports with bounded size and depth, verify the digest and supported format version, and run the existing template-definition validator before writing any row.

#### Scenario: Valid package preview

- **WHEN** an administrator uploads a valid supported package for validation
- **THEN** the API returns metadata, provenance, compatibility, and conflict information without persisting it

#### Scenario: Tampered or unsupported package

- **WHEN** the digest is invalid, the version unsupported, or the definition invalid
- **THEN** the API rejects the package with structured errors and persists nothing

### Requirement: Explicit import conflicts

The system SHALL require an explicit `create-copy`, `new-revision`, or `reject` policy when a stable identity already exists and MUST apply import atomically.

#### Scenario: Import as copy

- **WHEN** an administrator chooses `create-copy`
- **THEN** a new private template identity is created with source provenance retained

#### Scenario: Conflicting import rejected

- **WHEN** a stable identity exists and no valid conflict policy is supplied
- **THEN** the API returns `409 Conflict` and changes nothing

### Requirement: Immutable template revisions

The system SHALL retain immutable template revisions and SHALL bind every new interview session or generated document to the exact revision used.

#### Scenario: Edit creates revision

- **WHEN** an owner changes a private template definition
- **THEN** a new revision becomes current while prior revisions remain readable by authorized users

#### Scenario: Roll back template

- **WHEN** an owner selects a prior revision for rollback
- **THEN** the system creates a new current revision with that content and preserves the complete history

### Requirement: Template portability controls

The Flutter application SHALL allow authorized users to validate/import packages, export templates, inspect provenance and revisions, and confirm conflicts explicitly.

#### Scenario: Import requires confirmation

- **WHEN** validation reports an identity conflict
- **THEN** the UI presents the permitted conflict policies and imports only after explicit confirmation

