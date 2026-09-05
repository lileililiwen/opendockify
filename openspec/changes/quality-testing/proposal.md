## Why

The backend has valuable unit and architecture tests, but coverage is uneven:
the Flutter suite is small, CI does not run the app checks, and there is no
repeatable end-to-end or provider-matrix evidence for the documented flows.
The current quality process can therefore report green while the shipped app
or deployment path is broken.

## What Changes

- Define a test pyramid covering domain/unit, API integration, Flutter widget,
  contract, migration, and container smoke tests.
- Make coverage thresholds meaningful for changed executable code and exclude
  generated/migration files explicitly.
- Add deterministic fixtures and failure-injection tests for auth, drafts,
  finalization, sharing, AI guard, backups, and webhook delivery.
- Publish test/coverage artifacts and a reproducible local quality command.

## Capabilities

### New Capabilities

- `quality-testing`: layered, deterministic verification for backend and app.

## Non-goals

- No arbitrary global 100% coverage target or replacement test framework.
