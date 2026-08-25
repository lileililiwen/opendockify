## Why

OpenDockify exposes every template field as one flat form, which makes complex legal documents difficult to complete correctly. Mature document-assembly tools use guided, conditional interviews so users see only relevant questions and can safely resume long sessions.

## What Changes

- Add a declarative, validated interview definition to templates.
- Add server-authoritative branching, progress, review, and resumable answer sessions.
- Add a Flutter step-by-step interview UI with accessible error and review states.
- Preserve the existing flat form for templates without an interview definition.

## Capabilities

### New Capabilities

- `guided-interviews`: Conditional question flows, server-side validation, resumable sessions, and final answer projection into document generation.

### Modified Capabilities

None.

## Non-goals

- No arbitrary code, scripts, legal advice engine, or AI-selected legal outcome.
- No simultaneous editing, public anonymous interviews, or replacement of template field validation.

## Impact

- Template JSON schema and validation, a new interview-session domain module and migration, authenticated API endpoints, and Flutter generation flow.
- Existing templates and generation endpoints remain backward compatible.

