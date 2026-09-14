#!/usr/bin/env bash
# Regenerates all game content and FAILS unless generation actually completed.
#
#   ./tools/regenerate.sh
#
# Exists because checking a Unity batch log for compile errors is not the same as checking
# that the method ran. A native crash inside ContentGenerator leaves no "error CS" line, so an
# errors-only check reports success — and every simulation after it silently measures stale
# content. That happened twice before this script existed. It gates on the success line the
# generator prints as its very last act.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export PATH="$HOME/.unity/bin:$PATH"
LOG="$(mktemp -t emberdeck-generate).log"

# shellcheck source=unity-guards.sh
source "$ROOT/tools/unity-guards.sh"
unity_guards "[regenerate]" || exit 1

unity run "$ROOT" --editor-version 6000.5.1f1 --no-banner --timeout 1800 \
  -- -executeMethod EmberDeck.EditorTools.ContentGenerator.Generate -logFile "$LOG" >/dev/null 2>&1 || true

if ! grep -q "\[EmberDeck\] Content and scene generated" "$LOG"; then
  echo "[regenerate] FAILED: generation did not complete. Log: $LOG" >&2
  grep -nE "error CS|Native Crash|Aborting|Exception" "$LOG" | head -6 >&2 || true
  exit 1
fi

echo "[regenerate] ok ($(grep -o 'Generated [0-9]* cards' "$LOG" | head -1))"
