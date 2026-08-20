# flutter-app-admin Specification

## Purpose
TBD - created by archiving change flutter-mobile-app. Update Purpose after archive.

## Requirements

### Requirement: Admin role gating

Admin features SHALL only be reachable by users whose profile role is
`Administrator`; the app SHALL hide admin navigation for `Regular` users and
SHALL refuse direct navigation to admin routes for non-admins.

#### Scenario: Administrator sees admin menu

- **WHEN** a user with role `Administrator` opens the app
- **THEN** the admin section (settings, templates, AI usage) is available

#### Scenario: Regular user denied

- **WHEN** a `Regular` user attempts to open an admin route
- **THEN** the route is refused and a "requires administrator" message is shown

### Requirement: System settings browse and edit

The app SHALL list system settings from `GET /api/admin/settings`, showing key,
effective value, and source, and SHALL let an administrator update a setting via
`PUT /api/admin/settings/{key}`. Secrets (e.g. `Ai.ApiKey`) SHALL be shown
masked and SHALL be editable only by typing a replacement value.

#### Scenario: Settings list with masked secrets

- **WHEN** an administrator opens the settings screen
- **THEN** all allowlisted settings are listed; secret values are masked

#### Scenario: Update a non-secret setting

- **WHEN** the administrator edits a setting value
- **THEN** the `PUT` succeeds and the updated value is reflected in the list

#### Scenario: Invalid value rejected

- **WHEN** the administrator submits a value that fails the backend's type
  validation (e.g. a non-boolean for `Ai.Enabled`)
- **THEN** the app shows the backend error message and keeps the previous value

### Requirement: Admin template upsert

The app SHALL let an administrator create or update a global (public) template
via `POST /api/admin/templates` (optional `id` for update), with the same field
set as private templates.

#### Scenario: Upsert global template

- **WHEN** the administrator saves a global template
- **THEN** the template is created (or updated when an `id` is supplied) and is
  available in the marketplace

#### Scenario: Upsert validation failure

- **WHEN** the definition JSON or body is invalid
- **THEN** the app shows the backend validation error and nothing is saved

### Requirement: AI usage log

The app SHALL let an administrator view recent AI usage entries from
`GET /api/admin/ai-usage?limit=` showing user id, action, request/response
snippets, success flag, and timestamp.

#### Scenario: View usage log

- **WHEN** an administrator opens the AI usage screen
- **THEN** the most recent usage entries are listed with their metadata

#### Scenario: Usage log failure

- **WHEN** the API returns an error
- **THEN** the app shows an error state with retry

### Requirement: Admin error surfacing

Admin screens SHALL surface backend `error` messages verbatim (they are already
human-readable) and SHALL reflect `403` responses as a role problem, routing the
user back to a non-admin screen.

#### Scenario: Admin call returns 403

- **WHEN** a request on an admin screen returns `403`
- **THEN** the app shows the role error and removes admin navigation from the
  session