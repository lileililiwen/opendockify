#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

openspec validate --changes --strict --no-interactive
HUSKY=0 dotnet restore
dotnet format OpenDockify.sln --verify-no-changes --no-restore
dotnet build OpenDockify.sln -c Release --no-restore /warnaserror
dotnet test OpenDockify.sln -c Release --no-build
(cd opendockify-app && flutter analyze && flutter test)
