## Context

Ported from the reference project. Husky.Net 0.9.x is declared as a local tool
in `.config/dotnet-tools.json`. A `Husky` MSBuild target in
`Directory.Build.props` runs `dotnet tool restore` + `dotnet husky install`
after `Restore`, stamped once per solution so it doesn't re-run every build:

- `.husky/pre-commit`: sources `_/husky.sh`, collects staged C# files via
  `git diff --cached --name-only --diff-filter=ACM -- '*.cs' '*.cshtml'`, runs
  `dotnet format --verify-no-changes --no-restore --include <files>`. (The
  frontend is React/TS, so no JS hooks needed; if a TS formatter is added
  later, this is the place.)
- `.husky/pre-push`: `dotnet build OpenDockify.sln --no-restore /warnaserror`.

`HUSKY=0` disables the target; `--no-verify` bypasses hooks for one command.
The stamp file is git-ignored.

## Goals / Non-Goals

**Goals:**
- Zero-setup local format + build gates.
- Idempotent install (stamp file).

**Non-Goals:**
- Commit-message linting, branch-name checks, secret scanning (deferred).
- CI gate changes.

## Decisions

- **Husky.Net** (dotnet tool) over Node husky — the reference project's stack,
  works with the .NET toolchain without a Node dependency.
- **pre-commit on staged files only** (fast); **pre-push full build**.
- **Stamp file** to install once; `HUSKY=0` escape hatch.

## Risks / Trade-offs

- [Risk: hooks slow down every commit/push] → Mitigation: staged-only format +
  incremental build; hooks are optional conveniences, CI is authoritative.
- [Risk: dotnet tool restore needs network on first clone] → Mitigation:
  documented in CONTRIBUTING; install is one-time.

## Migration Plan

1. Add `.config/dotnet-tools.json` (husky pinned).
2. Copy `.husky/` (pre-commit, pre-push, `_/husky.sh`) from the reference,
   adapting the solution name.
3. Add the `Husky` MSBuild target to `Directory.Build.props`.
4. Add `.husky/_/` to `.gitignore`.
5. Verify on a fresh clone: restore installs hooks; a formatted-vs-drifted
   commit demonstrates the pre-commit block; a failing build demonstrates the
   pre-push block.

## Open Questions

- Should the pre-commit also format staged TS/TSX? Decision: not in MVP —
  frontend lives in a separate repo; revisit when the React repo lands.
