#!/usr/bin/env bash
# Rebuilds the local NuGet feed (~/local-nuget) from the dotnet-platform-libs
# repository. Run this from the dotnet-platform-libs root after a platform
# change; opendockify's nuget.config picks up the new packages automatically.

set -euo pipefail

PLATFORM_REPO="${PLATFORM_REPO:-$HOME/code/dotnet-platform-libs}"
FEED_DIR="${LOCAL_NUGET:-$HOME/.local-nuget}"

echo "Packing platform libs from $PLATFORM_REPO into $FEED_DIR"
mkdir -p "$FEED_DIR"
dotnet pack "$PLATFORM_REPO" \
    --output "$FEED_DIR" \
    --configuration Release \
    --verbosity quiet

echo "Done. $(ls -1 "$FEED_DIR"/Platform.*.nupkg 2>/dev/null | wc -l) packages in $FEED_DIR"
