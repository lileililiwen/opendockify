## Context

Current screens use Material 3 with a seed-generated color scheme, but each
feature renders its own state logic. `DocumentsScreen` uses raw
`error.toString()`, integrations uses inline error text, and several dialogs
and list actions lack a common semantic/status contract.

## Decisions

- Add shared `AsyncStateView`, `EmptyState`, and `SafeErrorMessage` widgets;
  callers provide a retry callback and a stable user-facing message while
  diagnostic details remain available to logs only.
- Use adaptive navigation/layout primitives already available in Flutter;
  preserve the existing routes and provide a scrollable wide layout rather
  than adding a second navigation model.
- Define a small accessibility checklist in tests: semantic name/role/state,
  keyboard traversal, visible focus, 24x24 minimum targets, 4.5:1 text and
  3:1 control contrast, and no color-only status.
- Golden tests freeze animations and cover light/dark, 320dp width, desktop
  width, and text scale 2.0 for the highest-value screens.

## Risks and migration

The shared state widgets may alter copy and snapshots; migrate screen by
screen, keeping API error codes mapped to stable copy. No data migration is
needed.
