#!/usr/bin/env bash
# Crops the generated paintings to the shapes the game actually displays and installs them.
#
#   ./art/install_painted.sh
#
# Safe to run while generation is still going: it installs whatever exists, and a card with
# no painting yet keeps its SVG symbol. Re-run when more finish.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$ROOT/art/out/painted"
CARDS="$ROOT/Assets/EmberDeck/Art/Cards"
ENEMIES="$ROOT/Assets/EmberDeck/Art/Enemies"

command -v ffmpeg >/dev/null || { echo "ffmpeg not found" >&2; exit 1; }
mkdir -p "$CARDS" "$ENEMIES"

# The card art well is 178x128 — about 1.39:1. Generation is square, so a centred crop is
# needed or preserveAspect letterboxes the painting into a strip half the panel's height.
# The compositions are centred, so cutting equally from top and bottom keeps the subject.
CARD_CROP="crop=1024:736:0:144,scale=768:552"

# Enemy panels are near square; trim a little to fill rather than letterbox.
ENEMY_CROP="crop=1024:900:0:62,scale=640:562"

# Enemy ids come from generate_art.py's ENEMIES rather than a list repeated here: the boss
# portrait was once filed as a card because a hand-written list predated it.
ENEMY_IDS=" $(cd "$ROOT/art" && python3 -c 'import generate_art; print(" ".join(generate_art.ENEMIES))') "
[ "$ENEMY_IDS" != "  " ] || { echo "could not read enemy ids from generate_art.py" >&2; exit 1; }

cards=0
enemies=0
for file in "$SRC"/*.png; do
  [ -e "$file" ] || continue
  id="$(basename "$file" .png)"
  [[ "$id" == test_* ]] && continue
  # Backgrounds and app icons share this folder but have their own installers.
  [[ "$id" == bg_* || "$id" == app_icon_* ]] && continue
  if [[ "$ENEMY_IDS" == *" $id "* ]]; then
    ffmpeg -y -loglevel error -i "$file" -vf "$ENEMY_CROP" "$ENEMIES/$id.png"
    enemies=$((enemies + 1))
  else
    ffmpeg -y -loglevel error -i "$file" -vf "$CARD_CROP" "$CARDS/$id.png"
    cards=$((cards + 1))
  fi
done

echo "[painted] installed $cards card paintings, $enemies enemy portraits"
echo "[painted] cards still on SVG symbols: $(( 60 - cards ))"
if [ "$cards" -gt 60 ]; then
  echo "[painted] WARNING: more card images than cards — an enemy is filed as a card" >&2
fi
