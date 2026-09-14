#!/usr/bin/env bash
# Runs the full-run simulation and prints its report, FAILING unless the report was written.
#
#   ./tools/simulate.sh
#
# Same gate as regenerate.sh: a batch run that crashes or never reaches the simulator writes no
# report, and an empty grep must not read as "no problems". Run ./tools/regenerate.sh first after
# any content change, or this measures the previous content.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export PATH="$HOME/.unity/bin:$PATH"
LOG="$(mktemp -t emberdeck-simulate).log"

# shellcheck source=unity-guards.sh
source "$ROOT/tools/unity-guards.sh"
unity_guards "[simulate]" || exit 1

unity run "$ROOT" --editor-version 6000.5.1f1 --no-banner --timeout 3600 \
  -- -executeMethod EmberDeck.EditorTools.RunSimulator.RunFromMenu -logFile "$LOG" >/dev/null 2>&1 || true

if ! grep -q "=== EmberDeck full runs" "$LOG"; then
  echo "[simulate] FAILED: no report. Log: $LOG" >&2
  grep -nE "error CS|Native Crash|Aborting|Exception" "$LOG" | head -6 >&2 || true
  exit 1
fi

# The report is one multi-line Debug.Log; Unity follows it with the call site.
awk '/=== EmberDeck full runs/{on=1} on && /^UnityEngine\.Debug|^\(Filename:/{exit} on' "$LOG"
