#!/usr/bin/env bash
# Builds every card icon: SVG -> one Krita render -> sliced PNGs -> Unity.
#
#   ./art/render_cards.sh
#
# Rendering the whole sheet in one pass rather than sixty times is the difference between
# two seconds and three minutes: Krita's startup dominates, not the drawing.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/art/out"
DEST="$ROOT/Assets/EmberDeck/Art/Cards"
KRITA=/Applications/krita.app/Contents/MacOS/krita
TILE=256
COLUMNS=10

command -v ffmpeg >/dev/null || { echo "ffmpeg not found" >&2; exit 1; }
[ -x "$KRITA" ] || { echo "Krita not found at $KRITA" >&2; exit 1; }

echo "[cards] composing atlas"
python3 "$ROOT/art/card_art.py"

echo "[cards] rasterising with Krita"
# Krita does not exit on its own reliably in this mode, so bound it and check the output.
"$KRITA" "$OUT/cards_atlas.svg" --export --export-filename "$OUT/cards_atlas.png" >/dev/null 2>&1 &
pid=$!
for _ in $(seq 1 90); do kill -0 "$pid" 2>/dev/null || break; sleep 1; done
kill -9 "$pid" 2>/dev/null || true
[ -f "$OUT/cards_atlas.png" ] || { echo "Krita produced no atlas" >&2; exit 1; }

echo "[cards] slicing"
mkdir -p "$DEST"
rm -f "$DEST"/*.png
while IFS=$'\t' read -r index id; do
  x=$(( (index % COLUMNS) * TILE ))
  y=$(( (index / COLUMNS) * TILE ))
  ffmpeg -y -loglevel error -i "$OUT/cards_atlas.png" \
         -vf "crop=${TILE}:${TILE}:${x}:${y}" "$DEST/${id}.png"
done < "$OUT/cards_atlas.txt"

echo "[cards] $(ls "$DEST"/*.png | wc -l | tr -d ' ') icons in Assets/EmberDeck/Art/Cards"
