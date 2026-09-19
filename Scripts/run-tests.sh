#!/usr/bin/env bash
set -euo pipefail

# Runs the self-contained RoweMod check suite (real mod source compiled against
# stubs) on the .NET 6 SDK. These verify guard/logic behavior only; native
# trampolines, Unity object lifetimes, and in-game behavior still need the game.

cd "$(dirname "$0")/.."

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

BUNDLE="$PWD/Bundles/rowemod_custom_emotes"
failed=0

run() {
  local name="$1"; shift
  echo "===== $name ====="
  if dotnet run -c Release --project "Tests/$name" -- "$@"; then
    echo
  else
    echo "FAILED: $name"
    failed=1
  fi
}

run TrickTweakGuard
run AnimationStudio
run EmoteBundleLoader "$BUNDLE"
run LateNativeHooks
run MainMenuCharacterPreview

if [ "$failed" -ne 0 ]; then
  echo "One or more check suites failed."
  exit 1
fi
echo "All RoweMod check suites passed."
