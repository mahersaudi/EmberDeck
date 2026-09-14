#!/usr/bin/env bash
# Builds the UI icons: SVG -> one Krita render -> sliced PNGs -> Resources/Icons.
#
#   ./art/render_icons.sh
#
# Same pipeline as the old card symbols (render_cards.sh): one Krita pass over an atlas, because
# Krita's startup costs far more than the drawing.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/art/out"
DEST="$ROOT/Assets/EmberDeck/Resources/Icons"
KRITA=/Applications/krita.app/Contents/MacOS/krita
TILE=128
COLUMNS=8

command -v ffmpeg >/dev/null || { echo "ffmpeg not found" >&2; exit 1; }
[ -x "$KRITA" ] || { echo "Krita not found at $KRITA" >&2; exit 1; }

echo "[icons] composing atlas"
python3 "$ROOT/art/icons.py"

echo "[icons] rasterising with Krita"
rm -f "$OUT/icons_atlas.png"
# Krita does not exit on its own reliably in this mode, so bound it and check the output.
"$KRITA" "$OUT/icons_atlas.svg" --export --export-filename "$OUT/icons_atlas.png" >/dev/null 2>&1 &
pid=$!
for _ in $(seq 1 90); do kill -0 "$pid" 2>/dev/null || break; sleep 1; done
kill -9 "$pid" 2>/dev/null || true
[ -f "$OUT/icons_atlas.png" ] || { echo "Krita produced no atlas" >&2; exit 1; }

# An icon without transparency would sit in a black square over every portrait, and the
# failure would only show up in the game.
fmt=$(ffprobe -v error -select_streams v:0 -show_entries stream=pix_fmt -of csv=p=0 "$OUT/icons_atlas.png")
case "$fmt" in
  rgba|rgba64be|rgba64le|ya8|ya16be|ya16le|pal8) ;;
  *) echo "[icons] atlas has no alpha channel (pix_fmt=$fmt)" >&2; exit 1 ;;
esac

echo "[icons] slicing"
mkdir -p "$DEST"
# PNGs only: keeping the .meta files keeps every icon's GUID stable across re-renders.
rm -f "$DEST"/*.png
count=0
while IFS=$'\t' read -r index id; do
  [ -n "$id" ] || continue
  x=$(( (index % COLUMNS) * TILE ))
  y=$(( (index / COLUMNS) * TILE ))
  ffmpeg -y -loglevel error -i "$OUT/icons_atlas.png" \
         -vf "crop=${TILE}:${TILE}:${x}:${y}" -pix_fmt rgba "$DEST/${id}.png"
  count=$((count + 1))
done < "$OUT/icons_atlas.txt"

echo "[icons] $count icons -> $DEST"
