## Why

The heart of OpenDockify is a database-stored template engine: templates are
JSON definitions with typed form fields, optional clauses that can be toggled
on/off, and a body using `{{variable_name}}` placeholders. The system ships 8
built-in templates (Loan IOU, General IOU, Residential Lease, Mutual NDA,
Outsourcing Service, Part-time/Labor, Repayment Confirmation, Simple Demand
Letter). Built-ins are read-only; users copy them into private templates.
Multi-user isolation applies: users see only their own private templates plus
the global public templates.

## What Changes

- `Template` entity: id, owner id (null/0 for system-built-in), name, category,
  description, risk-warning text, body text, and a `DefinitionJson` holding the
  structured definition (fields + clauses).
- JSON definition model (parsed + validated at save and at load):
  - `fields[]`: name, label, type (string | number | currency | date),
    required, validation rules (e.g. non-negative, interest-rate range,
    max length, regex).
  - `clauses[]`: id, title, text; a boolean toggle decides whether the clause
    text is inserted.
- Rendering rule: `{{field_name}}` in body and clause text is replaced with the
  filled value (currency fields render the RMB-uppercase form; date fields
  render a formatted date). Unknown placeholders error at generation time.
- Template CRUD endpoints (scoped to owner; built-ins immutable), copy
  endpoint (creates a private copy owned by the caller), admin endpoint to
  upload a global public template, and marketplace listing (public + own).
- Strict JSON validation on save: schema shape, unknown field types rejected,
  `{{var}}` references validated against declared fields, clause ids unique,
  size limit to prevent malicious/DoS payloads.

## Capabilities

### New Capabilities

- `template-engine`: template entity + JSON definition (fields, clauses,
  validation), body rendering rules, CRUD, copy-not-edit for built-ins, admin
  global templates, marketplace listing, safe JSON parse/validate.

### Modified Capabilities

None.

## Non-goals

- No document generation flow (that is `document-generation`).
- No AI features (that is `ai-assist`).
- No RMB/locale formatting inside this module beyond delegating to
  `finance-conversion` for currency display.
- No template import/export (deferred).

## Impact

- New module `src/OpenDockify.Templates`: `Models/Template.cs`,
  `Models/TemplateDefinition.cs`, `Services/TemplateDefinitionValidator.cs`,
  `Services/TemplateService.cs`, `TemplatesModuleExtensions.cs`.
- `OpenDockify.Data`: `Template` DbSet + migration.
- `OpenDockify.Finance` referenced for currency uppercase rendering (defined in
  `finance-conversion` change).
- Endpoints under `/api/templates` (CRUD, copy, marketplace) + admin upload
  endpoint under `/api/admin/templates`.
- Seeder: 8 built-in templates inserted when DB empty.
