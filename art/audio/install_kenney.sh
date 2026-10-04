#!/usr/bin/env bash
# Installs the recorded sound effects: Kenney's CC0 packs, several takes per effect.
#
#   ./art/audio/install_kenney.sh [folder the four packs were unzipped into]
#
# The packs (all CC0 — public domain, no attribution required, commercial use allowed):
#   https://kenney.nl/assets/casino-audio      card slides, places, shoves, fans, the shuffle
#   https://kenney.nl/assets/impact-sounds     punches, metal, plate, soft thuds, glass
#   https://kenney.nl/assets/rpg-audio         cloth
#   https://kenney.nl/assets/interface-sounds  clicks, selects, confirmations
#
# Each effect becomes sfx_<name>_1 ... sfx_<name>_N in Resources/Audio; AudioDirector picks a different
# take each time. Any older file for these effects is removed first, so a stale
# sfx_<name>.wav cannot join the takes.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SRC="${1:?give the folder the four Kenney packs were unzipped into}"
DEST="$ROOT/Assets/EmberDeck/Resources/Audio"

install() {   # install <effect> <file> [<file> ...]
  local effect="$1"; shift
  rm -f "$DEST/sfx_${effect}.wav" "$DEST/sfx_${effect}.wav.meta" "$DEST/sfx_${effect}_"*.ogg "$DEST/sfx_${effect}_"*.ogg.meta
  local i=1
  for file in "$@"; do
    local path; path="$(find "$SRC" -name "$file.ogg" | head -1)"
    [ -n "$path" ] || { echo "missing $file.ogg" >&2; exit 1; }
    cp "$path" "$DEST/sfx_${effect}_${i}.ogg"
    i=$((i + 1))
  done
  echo "[kenney] $effect: $((i - 1)) takes"
}

install card_draw   card-slide-1 card-slide-2 card-slide-3 card-slide-4 card-slide-5 card-slide-6
install card_play   cards-pack-take-out-1 cards-pack-take-out-2
install throw       card-shove-1 card-shove-2 card-shove-3 card-shove-4
install discard     card-fan-1 card-fan-2
install turn_start  card-shuffle
install hit         impactPunch_medium_000 impactPunch_medium_001 impactPunch_medium_002 impactPunch_medium_003 impactPunch_medium_004
install heavy_hit   impactPunch_heavy_000 impactPunch_heavy_001 impactPunch_heavy_002 impactPunch_heavy_003 impactPunch_heavy_004
install blocked_hit impactMetal_medium_000 impactMetal_medium_001 impactMetal_medium_002 impactMetal_medium_003 impactMetal_medium_004
install block_gain  impactPlate_light_000 impactPlate_light_001 impactPlate_light_002 impactPlate_light_003 impactPlate_light_004
install enemy_death impactSoft_heavy_000 impactSoft_heavy_001 impactSoft_heavy_002 impactSoft_heavy_003 impactSoft_heavy_004
install player_hurt impactSoft_medium_000 impactSoft_medium_001 impactSoft_medium_002 impactSoft_medium_003 impactSoft_medium_004
install upgrade     impactMetal_heavy_000 impactMetal_heavy_001 impactMetal_heavy_002
install potion      impactGlass_light_000 impactGlass_light_001 impactGlass_light_002
install debuff      cloth1 cloth2 cloth3 cloth4
install click       click_001 click_002 click_003 click_004 click_005
install map_select  select_001 select_002 select_003
install reward      confirmation_001 confirmation_002 confirmation_003
