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

cards=0
enemies=0
for file in "$SRC"/*.png; do
  [ -e "$file" ] || continue
  id="$(basename "$file" .png)"
  case "$id" in
    test_*) continue ;;
    emberling|cinder_rat|ash_hound)
      ffmpeg -y -loglevel error -i "$file" -vf "$ENEMY_CROP" "$ENEMIES/$id.png"
      enemies=$((enemies + 1)) ;;
    *)
      ffmpeg -y -loglevel error -i "$file" -vf "$CARD_CROP" "$CARDS/$id.png"
      cards=$((cards + 1)) ;;
  esac
done

echo "[painted] installed $cards card paintings, $enemies enemy portraits"
echo "[painted] cards still on SVG symbols: $(( 60 - cards ))"
