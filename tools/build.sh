#!/usr/bin/env bash
# Builds players and FAILS unless every requested build reports success.
#
#   ./tools/build.sh                     # both release builds (Build/Release/macOS, Build/Release/Windows)
#   ./tools/build.sh BuildMac            # any BuildScript method: BuildMac, BuildMacRelease,
#                                        # BuildWindows, BuildWindowsRelease, BuildAllRelease,
#                                        # BuildAndroid, BuildAndroidRelease
#
# Gates on BuildScript's own "Build Succeeded" lines, counted, rather than on the absence of
# errors: a batch run that dies early prints no error and no success.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export PATH="$HOME/.unity/bin:$PATH"
METHOD="${1:-BuildAllRelease}"
LOG="$(mktemp -t emberdeck-build).log"

# shellcheck source=unity-guards.sh
source "$ROOT/tools/unity-guards.sh"
unity_guards "[build]" || exit 1

expected=1
[ "$METHOD" = "BuildAllRelease" ] && expected=2

unity run "$ROOT" --editor-version 6000.5.1f1 --no-banner --timeout 2400 \
  -- -executeMethod "EmberDeck.EditorTools.BuildScript.$METHOD" -logFile "$LOG" >/dev/null 2>&1 || true

succeeded=$(grep -c '\[EmberDeck\] Build Succeeded' "$LOG" || true)
if [ "$succeeded" -lt "$expected" ]; then
  echo "[build] FAILED: $succeeded of $expected builds succeeded. Log: $LOG" >&2
  grep -E '\[EmberDeck\]|error CS' "$LOG" | sort -u | head -8 >&2 || true
  exit 1
fi

grep '\[EmberDeck\] Build Succeeded' "$LOG" | sed 's/^\[EmberDeck\] /[build] /'
