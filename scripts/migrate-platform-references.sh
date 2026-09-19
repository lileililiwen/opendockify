#!/usr/bin/env bash
# Migrate all `dotnet-platform-libs` project references to PackageReference
# against the local feed (see nuget.config). The platform libs are
# already packed into ~/.local-nuget by scripts/rebuild-platform-feed.sh.

set -euo pipefail

VERSION="${PLATFORM_VERSION:-0.1.0}"

for f in $(rg -l "dotnet-platform-libs" src/ -g "*.csproj" 2>/dev/null); do
  echo "Migrating: $f"
  perl -i -pe 's#<ProjectReference Include="[^"]*dotnet-platform-libs\\src\\Platform\.([^\\]+)\\Platform\.\1\.csproj" />#<PackageReference Include="Platform.\1" Version="'"$VERSION"'" />#g' "$f"
done

echo "Verifying..."
remaining=$(rg "dotnet-platform-libs" src/ -g "*.csproj" 2>/dev/null | wc -l)
if [[ "$remaining" -ne 0 ]]; then
  echo "FAILED: $remaining references still reference the platform repo"
  exit 1
fi
echo "All references migrated to PackageReference."
