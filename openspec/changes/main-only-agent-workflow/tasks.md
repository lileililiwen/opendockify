## 1. Normative workflow

- [ ] 1.1 Update `Agents.md` to declare direct `main` work, no new branches, sequential OpenSpec delivery, and no force-push.
- [ ] 1.2 Update `CONTRIBUTING.md` to match single-maintainer delivery while retaining external-contributor guidance.
- [ ] 1.3 Add a concise recovery procedure for an existing branch and dirty worktree.

## 2. History integration

- [ ] 2.1 Inspect branch ancestry and integrate the intended existing branch into `main` using fast-forward or a documented merge.
- [ ] 2.2 Verify status, ancestry, tests, and spec validation on `main`.
- [ ] 2.3 Delete only the obsolete local branch after successful integration; do not force-push or delete remote branches.

## 3. Verification

- [ ] 3.1 Add a documentation/spec lint check that rejects contradictory branch instructions.
- [ ] 3.2 Confirm a fresh agent can identify `main` as the active branch and process one change at a time.
