## Why

The existing CI workflow verifies .NET restore, format, build, tests, NuGet
audit, and optional Sonar analysis, but it does not verify the Flutter app,
OpenSpec changes, Docker image startup, migration/seed behavior, or release
artifacts. The documented product surface includes Android, web, Linux, and
Docker, so the current pipeline leaves major delivery paths untested.

## What Changes

- Add fast spec, C# and Flutter checks to every push and pull request trigger
  retained for external contributors.
- Add a container build and SQLite health/migration smoke job.
- Add Flutter analyze/test/build checks for web and Linux, with Android build
  validation where runner capacity permits.
- Pin actions/tool versions, upload diagnostics, and define release artifact
  provenance and failure retention.

## Capabilities

### New Capabilities

- `ci-cd-release-gates`: complete automated delivery gates.

## Non-goals

- No automatic production deployment, signing-key storage, or cloud-specific
  hosting setup.
