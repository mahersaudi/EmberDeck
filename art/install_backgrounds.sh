#!/usr/bin/env bash
# Installs the painted backgrounds: SDXL's 1344x768 renders, scaled to the game's 1920x1080.
#
#   ./art/install_backgrounds.sh
#
# They go under Resources/Backgrounds, where CombatView and MapView load them by name; a background
# that is not installed falls back to the flat colour, so this is safe to run while some are missing.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$ROOT/art/out/painted"
DEST="$ROOT/Assets/EmberDeck/Resources/Backgrounds"

command -v ffmpeg >/dev/null || { echo "ffmpeg not found" >&2; exit 1; }
mkdir -p "$DEST"

count=0
NAMES=(bg_hallway bg_elite bg_boss bg_map bg_hallway_2 bg_elite_2 bg_boss_2 bg_map_2
       bg_hallway_3 bg_elite_3 bg_boss_3 bg_map_3)
for name in "${NAMES[@]}"; do
  if [ ! -f "$SRC/$name.png" ]; then
    echo "[bg] $name.png not rendered yet" >&2
    continue
  fi
  # Trim the bottom 4% and matching sides before scaling. The model sometimes signs a painting in its
  # bottom corners — bg_elite came back with lettering in both — and 1310x737 keeps the 16:9 shape.
  ffmpeg -y -loglevel error -i "$SRC/$name.png" -vf "crop=1310:737:17:0,scale=1920:1080:flags=lanczos" "$DEST/$name.png"
  count=$((count + 1))
done

echo "[bg] installed $count of ${#NAMES[@]} backgrounds -> $DEST"
