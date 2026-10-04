#!/usr/bin/env bash
# Installs the music and the effects Kenney's packs don't cover, from CC0 recordings on OpenGameArt and
# Kenney's Music Jingles. Downloads into a cache folder first, so a re-run is offline.
#
#   ./art/audio/install_cc0.sh [cache folder]     (needs curl, unzip, ffmpeg)
#
# Sources (all CC0 — public domain, no attribution required, commercial use allowed):
#   music_combat  "Cynic Battle Loop" by Ferk                 https://opengameart.org/content/cynic-battle-loop
#   music_boss    "Epic Boss Battle" by Juhani Junkala         https://opengameart.org/content/boss-battle-music
#   music_map     "Dark Shrine Loop" by qubodup and yd         https://opengameart.org/content/dark-shrine-loop
#   burn          a fire crackle by AntumDeluge                https://opengameart.org/content/fire-crackling
#   overheat, relic, buff
#                 "80 CC0 RPG SFX" by rubberduck              https://opengameart.org/content/80-cc0-rpg-sfx
#   victory, defeat
#                 Kenney "Music Jingles", pizzicato set        https://kenney.nl/assets/music-jingles
#
# Music is levelled to a common loudness (the three files arrive 15 LU apart) by plain gain, which keeps
# each loop seamless; a limiter or dynamic normaliser would bend the seam. Everything is written as WAV
# and Unity compresses it on import (AudioImportSettings), so no lossy file is encoded twice.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CACHE="${1:-${TMPDIR:-/tmp}/emberdeck-cc0}"
DEST="$ROOT/Assets/EmberDeck/Resources/Audio"
mkdir -p "$CACHE"

fetch() {   # fetch <file> <url>
  [ -s "$CACHE/$1" ] || curl -sSL --fail -o "$CACHE/$1" "$2"
}
fetch combat.ogg  "https://opengameart.org/sites/default/files/cynicbattleloop_0.ogg"
fetch boss.wav    "https://opengameart.org/sites/default/files/Juhani%20Junkala%20-%20Epic%20Boss%20Battle%20%5BSeamlessly%20Looping%5D.wav"
fetch map.ogg     "https://opengameart.org/sites/default/files/qubodup-yd-DarkShrineLoop-OpenGameArt.ogg"
fetch fire.ogg    "https://opengameart.org/sites/default/files/fire-1_0.ogg"
fetch rpg80.zip   "https://opengameart.org/sites/default/files/80-CC0-RPG-SFX_0.zip"
fetch jingles.zip "https://kenney.nl/media/pages/assets/music-jingles/f37e530b9e-1677590399/kenney_music-jingles.zip"
[ -d "$CACHE/rpg80" ]   || unzip -q -o "$CACHE/rpg80.zip" -d "$CACHE/rpg80"
[ -d "$CACHE/jingles" ] || unzip -q -o "$CACHE/jingles.zip" -d "$CACHE/jingles"

find_one() { find "$CACHE" -name "$1" | head -1; }

loudness() {   # integrated loudness of a file, LUFS
  ffmpeg -hide_banner -nostats -i "$1" -af ebur128 -f null - 2>&1 | awk '/^ *I:/ {v=$2} END {print v}'
}

music() {   # music <track> <source> <target LUFS>
  local gain; gain="$(awk -v t="$3" -v i="$(loudness "$2")" 'BEGIN {printf "%.2f", t - i}')"
  rm -f "$DEST/music_$1.ogg" "$DEST/music_$1.ogg.meta"
  ffmpeg -y -loglevel error -i "$2" -af "volume=${gain}dB" -ar 44100 -c:a pcm_s16le "$DEST/music_$1.wav"
  echo "[cc0] music_$1: ${gain} dB"
}

clear_sfx() {   # every file an effect may have had: a single take, or numbered takes
  rm -f "$DEST/sfx_$1".wav "$DEST/sfx_$1".wav.meta "$DEST/sfx_$1"_*.ogg "$DEST/sfx_$1"_*.ogg.meta \
        "$DEST/sfx_$1"_*.wav "$DEST/sfx_$1"_*.wav.meta
}

# A take: optional trim, short fades so a cut never clicks, then peak-normalised to -1 dBFS.
take() {   # take <source> <output> [start seconds] [length seconds]
  local trim="" fades=""
  if [ -n "${4:-}" ]; then
    trim="-ss $3 -t $4"
    fades="afade=t=in:d=0.02,afade=t=out:st=$(awk -v l="$4" 'BEGIN {print l - 0.25}'):d=0.25,"
  fi
  local peak; peak="$(ffmpeg -hide_banner $trim -i "$1" -af "${fades}volumedetect" -f null - 2>&1 | awk '/max_volume/ {print $5}')"
  # shellcheck disable=SC2086
  ffmpeg -y -loglevel error $trim -i "$1" -af "${fades}volume=$(awk -v p="$peak" 'BEGIN {print -1 - p}')dB" \
         -ar 44100 -c:a pcm_s16le "$2"
}

sfx() {   # sfx <effect> <file> [<file> ...]   — each file from the packs becomes one take
  local effect="$1"; shift
  clear_sfx "$effect"
  local i=1
  for file in "$@"; do
    local path; path="$(find_one "$file")"
    [ -n "$path" ] || { echo "missing $file" >&2; exit 1; }
    take "$path" "$DEST/sfx_${effect}_${i}.wav"
    i=$((i + 1))
  done
  echo "[cc0] $effect: $((i - 1)) takes"
}

music combat "$CACHE/combat.ogg" -16
music boss   "$CACHE/boss.wav"   -15
music map    "$CACHE/map.ogg"    -18

# Burn plays on every burn applied and every tick, so it is four short pieces of one crackle.
clear_sfx burn
i=1
for start in 0.0 0.9 1.8 2.7; do
  take "$CACHE/fire.ogg" "$DEST/sfx_burn_${i}.wav" "$start" 0.8
  i=$((i + 1))
done
echo "[cc0] burn: 4 takes"

sfx overheat spell_fire_06.ogg spell_fire_03.ogg spell_fire_04.ogg
sfx relic    item_gem_01.ogg item_gem_03.ogg item_gem_04.ogg
sfx buff     spell_01.ogg spell_02.ogg
sfx victory  jingles_PIZZI02.ogg   # a rising run
sfx defeat   jingles_PIZZI07.ogg   # a falling, slowing one
clear_sfx heat                     # nothing plays it
