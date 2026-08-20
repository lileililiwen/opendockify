## 1. Tool manifest + hooks

- [ ] 1.1 Add `.config/dotnet-tools.json` with `husky` pinned (0.9.x)
- [ ] 1.2 Copy `.husky/pre-commit` (staged `*.cs`/`*.cshtml` format check)
  and `.husky/pre-push` (`dotnet build OpenDockify.sln --no-restore
  /warnaserror`) + `.husky/_/husky.sh` from the reference project
- [ ] 1.3 Add `task-runner.json` schema placeholder
- [ ] 1.4 Add the `Husky` MSBuild target (AfterTargets=Restore, stamp file,
  `HUSKY=0` guard) to `Directory.Build.props`
- [ ] 1.5 Add `.husky/_/` to `.gitignore`

## 2. Verify

- [ ] 2.1 Fresh-restore scenario: `dotnet restore` installs hooks (stamp
  created); second restore does not reinstall
- [ ] 2.2 Commit a deliberately misformatted staged file → pre-commit blocks
  with a format error; commit a clean file → succeeds
- [ ] 2.3 Introduce a build error → pre-push blocks the push
- [ ] 2.4 `HUSKY=0 dotnet restore` does not install hooks
- [ ] 2.5 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
