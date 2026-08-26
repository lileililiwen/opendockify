#!/usr/bin/env bash
set -euo pipefail

export PATH="$HOME/flutter/bin:$HOME/bin:$HOME/Android/Sdk/emulator:$HOME/Android/Sdk/platform-tools:$HOME/Android/Sdk/cmdline-tools/latest/bin:$PATH"
export ANDROID_HOME="$HOME/Android/Sdk"
export ANDROID_SDK_ROOT="$HOME/Android/Sdk"
export ANDROID_AVD_HOME="$HOME/.android/avd"
export CXX=clang++

cd "$(dirname "$0")/opendockify-app"

echo ">> Starting Android emulator..."
$ANDROID_HOME/emulator/emulator -avd opendockify -no-snapshot -gpu swiftshader_indirect -no-audio -no-boot-anim > /tmp/emulator_run.log 2>&1 &
EMULATOR_PID=$!
echo ">> Emulator PID: $EMULATOR_PID"

echo ">> Waiting for adb..."
for i in $(seq 1 60); do
  if adb devices 2>/dev/null | grep -q "emulator"; then
    echo ">> Emulator ready after ${i}0s"
    break
  fi
  sleep 2
done

echo ">> Installing app and running..."
flutter run -d emulator-5554