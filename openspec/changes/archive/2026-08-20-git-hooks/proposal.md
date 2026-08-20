## Why

Fast local feedback prevents formatting drift and broken builds from ever
reaching CI. The reference project uses Husky.Net to install two local hooks
automatically on restore: pre-commit checks formatting of staged files,
pre-push builds with warnings-as-errors. CI remains the authoritative gate;
hooks are a convenience that requires zero manual setup for contributors.

## What Changes

- `.config/dotnet-tools.json` declaring `husky` as a local tool.
- `.husky/pre-commit` — `dotnet format --verify-no-changes` on staged
  `*.cs`/`*.cshtml` files (no such files yet since frontend is React, but the
  hook covers future server-side code).
- `.husky/pre-push` — `dotnet build OpenDockify.sln --no-restore /warnaserror`.
- A `Husky` MSBuild target in `Directory.Build.props` (AfterTargets=Restore)
  that installs hooks once per solution via a stamp file; `HUSKY=0` disables.
- `task-runner.json` schema placeholder (Husky 0.9.x).

## Capabilities

### New Capabilities

- `git-hooks`: automatic Husky.Net hook installation + local pre-commit
  (format) and pre-push (build) gates.

### Modified Capabilities

None.

## Non-goals

- No CI enforcement changes (that is `ci-pipeline`).
- No hooks beyond format/build.
- No commit message linting (conventions are documented, not enforced locally).

## Impact

- `.config/dotnet-tools.json`, `.husky/{pre-commit,pre-push,_/husky.sh}`,
  `task-runner.json`.
- `Directory.Build.props` gains the `Husky` restore target (installed once).
- `.husky/_/install.stamp` is git-ignored.
