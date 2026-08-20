# flutter-app-templates Specification

## Purpose
TBD - created by archiving change flutter-mobile-app. Update Purpose after archive.

## Requirements

### Requirement: Template marketplace

The app SHALL list available templates from `GET /api/templates/marketplace`
grouped or filterable by `category`, showing name, category, description, and
built-in/private markers, with a refresh action.

#### Scenario: Marketplace loads

- **WHEN** a logged-in user opens the Templates screen
- **THEN** the marketplace list is fetched and displayed with name, category,
  and description

#### Scenario: Empty or failed load

- **WHEN** the API returns an error or the list is empty
- **THEN** the app shows an error/empty state with a retry action

### Requirement: Template detail

The app SHALL fetch a template via `GET /api/templates/{id}` and display its
name, category, description, body preview, and risk notice.

#### Scenario: Open template

- **WHEN** the user taps a marketplace item
- **THEN** the full template is fetched and the detail screen is shown

#### Scenario: Foreign or missing template

- **WHEN** the API returns `404 Not Found`
- **THEN** the app shows "template not found" and returns to the list

### Requirement: Definition JSON parsing

The app SHALL parse a template's `definitionJson` into typed models: `fields`
(each with name, label, `type` Text/Number/Currency/Date, required flag, and
optional validation rules) and `clauses` (each with id, title, text). Malformed
definition JSON SHALL NOT crash the app; it SHALL surface a parse error in the
UI.

#### Scenario: Valid definition

- **WHEN** the definition JSON is well-formed
- **THEN** fields and clauses are parsed and drive the dynamic form

#### Scenario: Malformed definition

- **WHEN** the definition JSON cannot be parsed
- **THEN** the app shows a "template definition is invalid" message and does not
  render a broken form

### Requirement: Dynamic fill-in form

The app SHALL render a form generated from the parsed definition: one input per
field (text/number/currency/date) and one toggle per optional clause, with
required fields marked and client-side validation mirroring the backend rules
(min/max, minLength/maxLength, pattern, dateFrom/dateTo, nonNegative).

#### Scenario: Required field enforcement

- **WHEN** the user tries to submit while a required field is empty
- **THEN** the form highlights the empty required field and blocks submission

#### Scenario: Value validation

- **WHEN** a value violates its field's validation rule (range, length, pattern,
  date bounds)
- **THEN** the field shows a validation error and submission is blocked until
  fixed

#### Scenario: Interest-rate warning

- **WHEN** a field marked `IsInterestRate` has a value the backend warns about
- **THEN** the non-blocking warning returned by generation is displayed to the
  user while the document is still generated

#### Scenario: Clause toggles

- **WHEN** the user toggles an optional clause on or off
- **THEN** the selected clause's id is included/excluded in the generated
  request's `selectedClauseIds`

### Requirement: Template copy

The app SHALL offer copying a template via `POST /api/templates/{id}/copy`,
creating a private editable copy owned by the current user.

#### Scenario: Copy built-in template

- **WHEN** the user taps "Use as my template" on a built-in template
- **THEN** a private copy is created and the user can fill and later edit it

#### Scenario: Copy failure

- **WHEN** the copy request fails (e.g. `404`, `403`)
- **THEN** the app shows the backend error and does not navigate

### Requirement: Private template management

The app SHALL let the owner create, edit, and delete their private templates
(`POST /api/templates`, `PUT /api/templates/{id}`, `DELETE /api/templates/{id}`)
with name, category, description, body, and definition JSON, and SHALL show the
backend's validation errors on failure.

#### Scenario: Create private template

- **WHEN** the user creates a template with valid fields
- **THEN** the API returns `201` and the new template appears in the user's
  private list

#### Scenario: Edit owned template

- **WHEN** the user edits one of their private templates
- **THEN** the changes are saved via `PUT` and the updated template is shown

#### Scenario: Delete owned template

- **WHEN** the user deletes a private template after confirmation
- **THEN** the template is removed from the list

#### Scenario: Foreign template edit denied

- **WHEN** the user tries to edit a template they do not own
- **THEN** the API returns `404` (or `403`) and the app shows the error without
  making local changes