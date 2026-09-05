## Context

`.github/workflows/ci.yml` currently centers on .NET and optional Sonar. Docker
Compose documents the supported SQLite deployment, and Flutter has web/Linux
targets, but none are exercised by CI.

## Decisions

- Keep one required `quality` workflow for source/spec/app checks and a
  separate `container-smoke` job that depends on the image build.
- Run `openspec validate --changes --strict --no-interactive` before code gates
  so malformed change packages fail quickly.
- Build the Docker image with safe test secrets, start it with an isolated
  SQLite volume, poll `/healthz`, and verify migrations/seed complete; always
  collect logs on failure.
- Use Flutter stable pinned by the repository metadata and cache pub packages;
  build web and Linux at minimum, with Android as an explicit matrix/runner
  capability rather than silently skipped.
