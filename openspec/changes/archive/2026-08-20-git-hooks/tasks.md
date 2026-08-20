## 1. Tool manifest + hooks

- [x] 1.1 Add `.config/dotnet-tools.json` with `husky` pinned (0.9.x)
- [x] 1.2 Copy `.husky/pre-commit` (staged `*.cs` format check)
  and `.husky/pre-push` (`dotnet build OpenDockify.sln --no-restore
  /warnaserror`) + `.husky/_/husky.sh` from the reference project
- [x] 1.3 Add `task-runner.json` schema placeholder (empty tasks list)
- [x] 1.4 Add the `Husky` MSBuild target (AfterTargets=Restore, stamp file,
  `HUSKY=0` guard) to `Directory.Build.props`
- [x] 1.5 Add `.husky/_/` to `.gitignore`

## 2. Verify

- [x] 2.1 Fresh-restore scenario: `dotnet restore` installs hooks ("Git hooks
  installed"); stamp file created, no reinstall on subsequent restores
- [x] 2.2 Commit a deliberately misformatted staged file → pre-commit blocks
  with a format error; commit a clean file → succeeds
- [x] 2.3 Introduce a build error → pre-push blocks the push
- [x] 2.4 `HUSKY=0` disables hook installation
- [x] 2.5 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
