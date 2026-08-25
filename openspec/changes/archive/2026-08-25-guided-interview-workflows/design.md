## Context

The current template definition, `TemplateDefinitionValidator`, preview/finalize endpoints, and Flutter draft store already provide field rules and immutable output. The gap is orchestration. Docassemble demonstrates the value of guided interviews and dependency-driven questions; this design adopts the interaction pattern without allowing its arbitrary Python/YAML execution model.

Research: https://github.com/jhpyle/docassemble and https://github.com/jhpyle/docassemble/blob/master/docassemble_base/docassemble/base/data/questions/examples/docx-template.yml

## Goals / Non-Goals

**Goals:** deterministic conditional flows, resumability, strict isolation, compatibility with existing validation, and an accessible mobile workflow.

**Non-Goals:** executable rules, legal conclusions, anonymous sessions, or concurrent co-editing.

## Decisions

- Extend template JSON with a schema-versioned `interview` object containing steps, field references, and a small allowlisted condition AST (`equals`, `notEquals`, `in`, `and`, `or`). Never evaluate code or string expressions.
- Introduce `OpenDockify.Interviews`; persist sessions against both template id and immutable revision id. Store answers as validated JSON plus expiry and optimistic concurrency token.
- Compile and validate the graph when a template revision is saved. Require every template-required field to be reachable on every applicable completion path.
- Make the API return the current visible step rather than trusting a client-supplied step id. On branch changes, discard answers that are no longer applicable.
- Completion produces the existing generation request and calls preview/finalize services; it does not create a second renderer or validation path.
- Retain Flutter local draft state only as an offline recovery cache; server state wins after an acknowledged save.

## Risks / Trade-offs

- [Rule language grows into a programming system] -> ship a versioned, deliberately small operator set and reject unknown operators.
- [Template edits strand sessions] -> pin revision and offer owner-only answer export instead of silently migrating answers.
- [Sensitive answers remain longer] -> configurable short expiry, explicit delete, no answers in logs, and cleanup job.
- [Backtracking creates stale hidden data] -> recompute the effective answer set on every accepted update.

## Migration Plan

Add nullable interview definitions and a new sessions table. Existing templates continue to use the flat form. Deploy backend support before the Flutter guided UI.

## Open Questions

- A later change may add administrator-authored reusable step fragments after real template repetition is measured.

