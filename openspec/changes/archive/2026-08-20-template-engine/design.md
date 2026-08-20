## Context

The template engine is the core. Design principles:

1. **Structured definition, not freeform body parsing.** The JSON definition
   (fields + clauses) is the contract; the body and clause texts are plain
   text with `{{field}}` placeholders. Validation happens at save time
   (schema, placeholders, size) so generation-time failures are limited to
   missing values (which the generation flow checks first).

2. **Two-tier ownership.** `Template.OwnerId` is null for system built-ins
   (read-only for everyone), and set for private templates. Admin-uploaded
   global templates are marked `IsPublic=true` + `OwnerId=null` but editable
   only by admins. Marketplace = public templates + owned private templates.

3. **Renderer output is plain text** with markup placeholders for the PDF layer
   (paragraph breaks) — the `pdf-rendering` capability in
   `document-generation` consumes it. The renderer delegates currency
   formatting to `OpenDockify.Finance.AmountToChinese` (from
   `finance-conversion`) and date formatting to a locale formatter.

4. **Safety.** `TemplateDefinitionValidator` enforces: known field types,
   unique clause ids, placeholder→field consistency, size caps, and valid
   ranges. Parsing uses `JsonSerializerOptions` with a max depth and bounded
   string length to resist JSON bombs.

5. **Built-ins seeding.** The 8 templates are defined in C# static data
   (content strings), seeded when the DB is empty, and flagged immutable.

## Goals / Non-Goals

**Goals:**
- Full CRUD + copy + marketplace + admin upload.
- Save-time definition validation (schema, placeholders, size).
- Rendering substitution incl. currency/date formatting.
- Multi-user isolation (public + own only).

**Non-Goals:**
- Generation workflow, records, PDF (document-generation).
- AI polish (ai-assist).
- Template import/export, batch ops (deferred).

## Decisions

- **`DefinitionJson` stored as TEXT, validated into a typed
  `TemplateDefinition` on every read/write.** Source of truth for validation is
  the typed model.
- **Built-in immutability by flag + null owner**, not just convention: the
  service refuses update/delete for `IsBuiltIn`.
- **Copy endpoint** performs a deep clone of the definition and re-validates it.
- **Renderer is pure** (no DB access): input = template + filled values +
  selected clause ids; output = final text or error. Generation orchestrates it.
- **Placeholders regex** `\{\{\s*([A-Za-z0-9_]+)\s*\}\}`; field name matching
  is case-sensitive (documented convention).

## Risks / Trade-offs

- [Risk: JSON injection / deep-nesting DoS] → Mitigation: max depth, size cap,
  length bounds, unknown-property rejection in `JsonSerializerOptions`.
- [Risk: malicious content in body/clause text (XSS when previewed in React)]
  → Mitigation: dual validation — backend stores only validated definitions;
  frontend escapes all rendered text (React default) and never uses
  `dangerouslySetInnerHTML`. Documented in design + tasks.
- [Risk: placeholder naming collisions with clause toggles] → Mitigation:
  placeholder names and clause ids live in separate namespaces; convention
  `clause.` prefix for clause references if needed later.
- [Risk: built-in content typos in legal text] → Mitigation: content is seed
  data, copyable and editable by users; built-ins are starting points only.

## Migration Plan

1. Add `OpenDockify.Templates` module: entities, definition models, validator,
   renderer, service.
2. Register `Template` DbSet in `OpenDockify.Data`; add migration `AddTemplates`.
3. Seed 8 built-ins (static C# content) when DB empty.
4. Implement endpoints: CRUD, copy, marketplace, admin upload.
5. Reference `OpenDockify.Finance` for currency/date formatting (after
   `finance-conversion` lands; stub dependency until then).
6. Verify HTTP scenarios incl. negative ones (edit built-in → 403, foreign
   private → 404, malformed/oversized/unknown-type/undeclared-placeholder →
   400).

## Open Questions

- Should admin global templates be editable by other admins? Decision: yes —
  any Administrator can edit public templates; only built-ins are immutable.
- Case-sensitivity of placeholders: confirmed case-sensitive for MVP.
