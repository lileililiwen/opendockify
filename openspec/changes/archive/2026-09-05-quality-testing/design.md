## Context

`tests/OpenDockify.UnitTests` and architecture tests exercise many services,
but most checks are in-memory unit tests. The app has one focused integration
widget test file, while `CONTRIBUTING.md` documents gates not all enforced in
CI. Several documented behaviors require HTTP, database migration, or a real
Flutter build to validate.

## Decisions

- Keep xUnit and Flutter test as the native frameworks; add shared fixtures
  instead of introducing a new runner.
- Add an API test host using SQLite and a temporary data directory, with tests
  for auth/ownership, migration/seed idempotency, and representative HTTP
  journeys.
- Use deterministic clocks, IDs, and webhook transports for retry/idempotency
  tests. Never call external AI, network, or production databases in CI.
- Gate changed backend executable lines at 80% and require all tests plus app
  analysis/tests; report overall coverage without making legacy code a blocker.
