# template-engine Specification

## Purpose
TBD - created by archiving change template-engine. Update Purpose after archive.

## Requirements

### Requirement: Template entity

The system SHALL store templates in the database with: id, owner id (null for
system built-ins), name, category, description, risk-warning text, body text,
and a JSON definition describing fields and optional clauses.

#### Scenario: Create a private template

- **WHEN** a Regular user creates a template with a definition
- **THEN** the template is persisted with the caller as owner

#### Scenario: Template fields persisted

- **WHEN** a template with fields and clauses is saved
- **THEN** the full definition JSON is retrievable on read

### Requirement: Built-in templates are read-only

The system SHALL ship 8 built-in templates (Loan IOU, General IOU, Residential
Lease, Mutual NDA, Outsourcing Service, Part-time/Labor Agreement, Repayment
Confirmation, Simple Demand Letter) that users SHALL NOT be able to edit or
delete; users SHALL copy them to create private editable versions.

#### Scenario: Built-ins present on fresh DB

- **WHEN** the app starts against an empty database
- **THEN** the 8 built-in templates exist and are visible in the marketplace

#### Scenario: Editing a built-in is rejected

- **WHEN** a user attempts to update or delete a built-in template
- **THEN** the API returns `403 Forbidden` and the built-in is unchanged

#### Scenario: Copy a built-in

- **WHEN** a user copies a built-in template
- **THEN** a new private template owned by the user is created with the same
  name (suffixed "（副本）" or similar), body, definition, and risk text, and
  the user can edit the copy

### Requirement: JSON definition schema

The system SHALL define template definitions with fields of type
`string | number | currency | date` and a clauses array; each field MAY carry
validation rules (required, non-negative, interest-rate range, min/max length,
regex, date range) and each clause SHALL have a unique id, title, and text.

#### Scenario: Valid definition accepted

- **WHEN** a definition contains only known field types and unique clause ids
- **THEN** the template saves successfully

#### Scenario: Unknown field type rejected

- **WHEN** a definition uses an unsupported field type
- **THEN** the API returns `400 Bad Request` with a validation error

#### Scenario: Duplicate clause ids rejected

- **WHEN** a definition has two clauses with the same id
- **THEN** the API returns `400 Bad Request`

### Requirement: Placeholder validation

The system SHALL validate that every `{{variable}}` placeholder in the body
and in clause texts references a declared field, and SHALL reject definitions
whose placeholders reference undeclared variables.

#### Scenario: Placeholder matches field

- **WHEN** the body contains `{{borrowerName}}` and a field named
  `borrowerName` is declared
- **THEN** the definition is valid

#### Scenario: Undeclared placeholder rejected

- **WHEN** the body contains `{{missingVar}}` but no such field is declared
- **THEN** the API returns `400 Bad Request`

### Requirement: Safe JSON parsing

The system SHALL cap the definition size and SHALL reject malformed or
structurally invalid JSON so that a malicious payload cannot crash the server
or inject invalid rows.

#### Scenario: Oversized definition rejected

- **WHEN** a definition JSON exceeds the configured size limit
- **THEN** the API returns `413 Payload Too Large` (or `400`) and nothing is
  saved

#### Scenario: Malformed JSON rejected

- **WHEN** the definition is not valid JSON
- **THEN** the API returns `400 Bad Request` with a parse error

### Requirement: Multi-user isolation for templates

The system SHALL expose to a user only: the global public templates and the
templates owned by that user. A user SHALL NOT be able to read, edit, copy, or
delete another user's private template.

#### Scenario: Marketplace shows public + own

- **WHEN** a Regular user opens the template marketplace
- **THEN** they see the 8 built-ins, any admin global templates, and their own
  private templates only

#### Scenario: Foreign private template hidden

- **WHEN** a user requests a private template owned by a different user
- **THEN** the API returns `404 Not Found`

### Requirement: Template rendering substitution

The system SHALL provide a renderer that replaces `{{field}}` placeholders in
the body and in selected clause texts with filled values, using the
RMB-uppercase form for `currency` fields and a formatted date for `date`
fields; a missing value for a referenced field SHALL produce a generation
error.

#### Scenario: Render body with values

- **WHEN** a filled template renders with all fields provided
- **THEN** the output text contains the field values in place of placeholders

#### Scenario: Currency field renders uppercase

- **WHEN** a `currency` field with value `1234` renders
- **THEN** the output contains the RMB-uppercase form (e.g. `壹仟贰佰叁拾肆元整`)

#### Scenario: Missing value errors

- **WHEN** a required referenced field has no value during render
- **THEN** rendering fails with a clear error identifying the field
