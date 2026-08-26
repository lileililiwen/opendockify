#!/usr/bin/env bash
set -euo pipefail

# OpenDockify Flutter app — run script
# Usage: ./run-app.sh [android|linux|web]
#
# Android needs: Android SDK at ~/Android/Sdk (already present)
# Linux desktop needs: sudo apt-get install -y libgtk-3-dev
# Web: no extra deps

export PATH="$HOME/flutter/bin:$HOME/bin:$HOME/Android/Sdk/emulator:$HOME/Android/Sdk/platform-tools:$HOME/Android/Sdk/cmdline-tools/latest/bin:$PATH"
export ANDROID_HOME="$HOME/Android/Sdk"
export ANDROID_SDK_ROOT="$HOME/Android/Sdk"
export CXX=clang++

cd "$(dirname "$0")/opendockify-app"

TARGET="${1:-android}"

echo ">> Flutter: $(flutter --version | head -1)"
echo ">> Target: $TARGET"
echo

if [ "$TARGET" = "android" ]; then
  # Start emulator if not already running
  if ! $ANDROID_HOME/platform-tools/adb devices 2>/dev/null | grep -q "emulator"; then
    echo ">> Starting Android emulator..."
    nohup $ANDROID_HOME/emulator/emulator -avd opendockify -no-skin -no-audio -no-boot-anim > /dev/null 2>&1 &
    echo ">> Waiting for emulator to boot..."
    for i in $(seq 1 60); do
      if $ANDROID_HOME/platform-tools/adb devices 2>/dev/null | grep -q "emulator"; then
        echo ">> Emulator ready."
        break
      fi
      sleep 2
    done
  else
    echo ">> Emulator already running."
  fi

  echo ">> Launching on Android..."
  flutter run -d emulator-5554

elif [ "$TARGET" = "linux" ]; then
  echo ">> Launching Linux desktop..."
  flutter run -d linux

elif [ "$TARGET" = "web" ]; then
  echo ">> Building web..."
  flutter build web --no-pub
  PORT=8080
  echo ">> Serving at http://localhost:$PORT"
  (cd build/web && python3 -m http.server $PORT)
fi