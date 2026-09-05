# Contributing to OpenDockify

Thanks for contributing! This guide describes the workflow every change goes through — including documentation-only and spec-only changes.

## Main-only maintenance

This is a single-maintainer repository. Work directly on `main`; do not create
feature branches or pull requests for routine changes. Keep commits focused,
run the local gates, and push `main` only after verification.

- **Do not allow force pushes** unless recovering from a documented repository incident.
- Keep `main` linear where practical; use fast-forward integration when importing
  an existing local branch, then remove the obsolete local branch.

If you cannot edit repository settings yourself, ask a maintainer and point at this section.

## Getting started

- Read [`Agents.md`](Agents.md) — it is the normative contract for how changes are specified, implemented, and verified in this repository.
- The project follows **spec-first development**: every change starts as an OpenSpec change in `openspec/changes/` and is implemented, verified, archived, and committed one change at a time.

## Branch policy

- Do not create a feature branch for normal work. The active checkout is `main`.
- Keep one OpenSpec change focused and process changes sequentially.
- If an old branch is discovered, integrate only its intended commits into
  `main`, verify, then delete the obsolete local branch.

## Commits

- Follow [Conventional Commits](https://www.conventionalcommits.org/) with a short imperative title:
  `feat:`, `fix:`, `docs:`, `refactor:`, `chore:`, `test:`.
- Title under ~72 characters; use the body for the *why* and *what*.
- Commit with your own identity; never overwrite history (`--force` push is blocked on `main` anyway).

## How CI verifies your change

The `CI` and `Release gates` workflows run on every push to `main` and every
pull request:

1. `dotnet restore`
2. `dotnet format OpenDockify.sln --verify-no-changes` — fails on any formatting drift from `.editorconfig`
3. `dotnet build OpenDockify.sln -c Release /warnaserror` — fails on any compiler or analyzer warning
4. `dotnet test OpenDockify.sln -c Release --no-build` — fails on any failing test

`Release gates` additionally validates active OpenSpec changes, builds Flutter
web/Linux targets, and starts the Docker image against SQLite to verify
`/healthz`. The local equivalents are `openspec validate --changes
--strict --no-interactive`, `flutter analyze`, `flutter test`, and
`flutter build web --release` / `flutter build linux --release`.

The workflow **must pass before merge** (required status check).

## SonarCloud quality gate (optional, host-side setup)

The CI workflow already contains the Sonar steps; they activate automatically once the repository secrets below are set. To turn on continuous analysis and the merge-request quality gate:

1. Create a project on [SonarCloud](https://sonarcloud.io) for this repository.
2. Add repository secrets so the `Sonar Begin/End` steps run:
   - `SONAR_TOKEN` — a SonarCloud user/analysis token.
   - `SONAR_ORG` — your SonarCloud organization key.
   - `SONAR_PROJECT_KEY` — the project key from step 1.
   - `SONAR_HOST_URL` — `https://sonarcloud.io` (or your self-hosted SonarQube URL).
3. In the SonarCloud project, enable the **New Code** quality gate (e.g. coverage ≥ 80%, bugs/vulnerabilities/smells = 0, duplicated lines < 3%). Old code is measured but not gating.
4. Add the Sonar analysis check to the required status checks on `main` (alongside the `build` check) so a red gate blocks merges.

The `Sonar End` step runs with `sonar.qualitygate.wait=true`, so CI fails when the new-code gate is red. Coverage XML is produced by `dotnet test` via `Coverlet.runsettings` (OpenCover format). If the secrets are absent the pipeline runs the format/build/test gates only.

## AI involvement markers

Every pull request records the AI involvement of its code in the PR description (and, for large changes, as a commit footer):

- `AI: generated` — a block was written by an AI with only minor human edits.
- `AI: assisted` — human-written with AI suggestions, refactors, or helpers.
- `AI: none` — human-authored.

AI-marked code is not blocked, but it is reviewed with extra care. For `AI: generated` / `AI: assisted` changes, the reviewer confirms:

- Spec compliance: the change matches its OpenSpec change and the four canonical artifacts.
- Security: ownership/authorization checks are present on every mutating path; no injection or secret-handling regressions.
- Tests: unit tests exist for new logic, or a stated reason why not.
- No dead code, unused branches, or leftover scaffolding.
- License/attribution for any copied fragments (the repo is MIT; copied code must be compatible and attributed).

An optional, non-blocking CI workflow (`.github/workflows/ai-marker-check.yml`) posts a soft-warning comment when a large diff (500+ added lines) has no AI marker. It never fails the pipeline.

## Local coverage

Run the unit and architecture tests with line coverage locally:

```bash
dotnet test OpenDockify.sln --collect:"XPlat Code Coverage" \
  --settings Coverlet.runsettings --results-directory TestResults
```

Reports are written to `TestResults/<guid>/coverage.opencover.xml` (one per test project, OpenCover format — the same XML consumed by SonarCloud). To check the incremental gate locally (as CI does on PRs):

```bash
python3 scripts/check_incremental_coverage.py --base origin/main \
  --threshold 0.80 --reports 'TestResults/**/coverage.opencover.xml'
```

The gate compares executable lines added by the branch against the coverage report: new lines must be ≥ 80% covered. Test projects, EF migrations, and generated code are excluded; overall coverage is reported but never blocks.

## NuGet vulnerability policy

NuGet packages are audited for known vulnerabilities at restore time (direct and transitive, high/critical severity) and by a CI step (`dotnet list package --vulnerable --include-transitive`). Restore-time audit failures surface as build errors because warnings are treated as errors.

When the audit flags a vulnerability, follow this order:

1. **Upgrade** — update the package (or a transitive dependency's parent) to a patched version. This is the default fix; upgrades are reviewed like any change.
2. **Pin** — if a patched version is unavailable for the framework band, pin the minimal safe version and record why.
3. **Accept** — only when the advisory cannot be fixed by upgrading (e.g. no fix exists for the referenced version). Add the advisory URL to `NuGetAuditSuppress` in `Directory.Build.props` **with a comment stating the rationale**, and note it in the PR for review. Accepted advisories are never silent.

## Local Git hooks

Husky.Net hooks are opt-in because normal restores must remain offline-safe and
must not trigger tool downloads. Install them explicitly with `HUSKY=1 dotnet
restore`:

- **pre-commit** — runs `dotnet format --verify-no-changes` on the staged C#/Razor files; the commit is blocked on formatting drift.
- **pre-push** — runs `dotnet build OpenDockify.sln --no-restore /warnaserror`; the push is blocked on build/analyzer errors.

Hooks are a **fast local pre-check** only — CI is the authoritative gate. They
can be skipped for a single command with `--no-verify` (e.g. `git commit
--no-verify`).

## Handling format / analyzer failures

- **Formatting drift** → run `dotnet format OpenDockify.sln`, review the diff, commit it. The SDK is pinned by [`global.json`](global.json); use a matching SDK so local and CI agree.
- **Analyzer warning (now an error)** → fix the code. If the rule genuinely cannot apply, use a *scoped* `#pragma warning disable <ID>` with a justification comment — never a blanket suppression. See the rationale in `openspec/specs/editorconfig-and-analyzers/spec.md`.
- **Test failure** → fix the code and re-run `dotnet test` before re-requesting review.

## Delivering a change

- Record the summary, test evidence, and quality-gate checklist in the commit
  body or handoff notes; the PR template is retained only for external
  contributors.
- Tag the OpenSpec change implemented (`openspec/changes/<name>` becomes
  `openspec/specs/<cap>/spec.md` after archiving).
- Archive with `openspec archive <name> -y`, then commit the archive directly on
  `main`.

## Review checklist

For reviewers (and for the author before requesting review):

- [ ] OpenSpec change exists and all four artifacts (`proposal.md`, `spec.md`, `design.md`, `tasks.md`) are complete and validated
- [ ] Change is implemented one at a time, in roadmap order
- [ ] `dotnet format --verify-no-changes` passes
- [ ] Build is clean (0 warnings / 0 errors under `/warnaserror`)
- [ ] Tests pass; scenarios in the spec are exercised (happy path **and** negative cases)
- [ ] No stubs, `TODO`s, or `NotImplementedException`
- [ ] Server-side validation and authorization on every mutating page; no secrets in logs/URLs
- [ ] No changes outside the change's scope (composition-root one-liners excepted)
- [ ] Commit message is conventional and explains *why*
- [ ] AI involvement marker recorded in the PR (generated / assisted / none); if generated or assisted, the AI review checklist applies
