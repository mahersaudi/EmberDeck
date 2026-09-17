"""
Card art, authored as SVG and rasterised by Krita.

    python3 art/card_art.py            # writes art/out/cards_atlas.svg
    (then render + slice — see art/render_cards.sh)

Why SVG and not a painting program: the art is text. It diffs, it merges, a palette change
is one edit, and every symbol is reproducible from this file by anyone without needing the
same software installed. Krita rasterises it headlessly in about two seconds for the whole
sheet.

Why symbols and not illustrations: sixty painted scenes is an artist's job. Sixty symbols in
one visual language is a design job, it reads better at card size than a detailed picture
would, and it carries real information — a player learns "pointed = attack, layered = block,
ringed = a Power that stays" and can then read a new card before finishing the text.
"""

import pathlib
import subprocess
import sys

TILE = 256
COLUMNS = 10
OUT = pathlib.Path(__file__).parent / "out"

# Archetype hues. Deliberately the same five the design document and the artifact use, so a
# card's colour means the same thing everywhere the player or the designer sees it.
HUES = {
    "forge":     ("#8fc4f0", "#3f7fb8", "#1d3d5c"),
    "swarm":     ("#f2cf7a", "#c99a2e", "#5e470f"),
    "pyre":      ("#ffb27a", "#d1502a", "#5e1d0e"),
    "overdrive": ("#ff9ec4", "#c4437c", "#59162f"),
    "ashfall":   ("#c9bfd9", "#8a7fa0", "#3a3348"),
    "neutral":   ("#d8d2c8", "#9b9184", "#3d3833"),
}


# ── Symbols ──────────────────────────────────────────────────────────────────────
# Each returns SVG markup in a 100x100 box. Shapes are bold and few: a symbol has to
# survive being 60 pixels wide on a card, where detail becomes noise.

def flame():
    return """
    <path d="M50 8 C 37 34, 20 45, 20 64 a 30 30 0 0 0 60 0 C 80 45, 63 34, 50 8 Z" class="a"/>
    <path d="M50 44 C 44 58, 36 64, 36 73 a 14 14 0 0 0 28 0 C 64 64, 56 58, 50 44 Z" class="hi"/>"""


def double_flame():
    return """
    <path d="M34 22 C 25 40, 14 49, 14 63 a 21 21 0 0 0 42 0 C 56 49, 43 40, 34 22 Z" class="a"/>
    <path d="M70 34 C 63 48, 54 55, 54 66 a 17 17 0 0 0 34 0 C 88 55, 77 48, 70 34 Z" class="a"/>
    <path d="M34 50 C 30 59, 26 63, 26 69 a 9 9 0 0 0 18 0 C 44 63, 38 59, 34 50 Z" class="hi"/>"""


def wildfire():
    return """
    <path d="M22 34 C 14 50, 6 57, 6 68 a 17 17 0 0 0 34 0 C 40 57, 30 50, 22 34 Z" class="a"/>
    <path d="M78 34 C 70 50, 60 57, 60 68 a 17 17 0 0 0 34 0 C 94 57, 86 50, 78 34 Z" class="a"/>
    <path d="M50 6 C 38 30, 24 42, 24 60 a 26 26 0 0 0 52 0 C 76 42, 62 30, 50 6 Z" class="a"/>
    <path d="M50 40 C 44 54, 37 60, 37 69 a 13 13 0 0 0 26 0 C 63 60, 56 54, 50 40 Z" class="hi"/>"""


def sword():
    return """
    <path d="M50 4 L 58 20 L 58 60 L 50 70 L 42 60 L 42 20 Z" class="a"/>
    <path d="M50 12 L 53 22 L 53 58 L 50 63 Z" class="hi"/>
    <path d="M26 62 h48 v9 H26 Z" class="b"/>
    <path d="M45 71 h10 v18 H45 Z" class="b"/>
    <circle cx="50" cy="93" r="6" class="b"/>"""


def greatsword():
    return """
    <path d="M50 2 L 62 18 L 62 58 L 50 72 L 38 58 L 38 18 Z" class="a"/>
    <path d="M50 10 L 55 20 L 55 56 L 50 64 Z" class="hi"/>
    <path d="M18 60 h64 l-6 10 H24 Z" class="b"/>
    <path d="M44 70 h12 v20 H44 Z" class="b"/>
    <path d="M36 90 h28 v7 H36 Z" class="b"/>"""


def dagger():
    return """
    <path d="M50 14 L 57 30 L 57 56 L 50 64 L 43 56 L 43 30 Z" class="a"/>
    <path d="M50 22 L 53 32 L 53 54 L 50 58 Z" class="hi"/>
    <path d="M33 56 h34 v8 H33 Z" class="b"/>
    <path d="M46 64 h8 v20 H46 Z" class="b"/>"""


def twin_fangs():
    return """
    <path d="M26 10 C 34 32, 38 50, 34 78 C 26 60, 18 40, 20 14 Z" class="a"/>
    <path d="M74 10 C 66 32, 62 50, 66 78 C 74 60, 82 40, 80 14 Z" class="a"/>
    <path d="M26 20 C 31 36, 33 50, 31 66 C 27 52, 23 36, 24 22 Z" class="hi"/>"""


def slashes():
    return """
    <path d="M18 78 C 30 52, 44 32, 62 16 L 70 26 C 52 42, 38 60, 28 84 Z" class="a"/>
    <path d="M36 84 C 48 58, 62 38, 80 22 L 88 32 C 70 48, 56 66, 46 90 Z" class="a"/>
    <path d="M4 66 C 14 44, 26 28, 42 14 L 48 22 C 34 36, 22 52, 14 72 Z" class="hi"/>"""


def sparks():
    return """
    <path d="M50 6 L 58 30 L 50 40 L 42 30 Z" class="a"/>
    <path d="M22 34 L 29 54 L 22 62 L 15 54 Z" class="a"/>
    <path d="M78 34 L 85 54 L 78 62 L 71 54 Z" class="a"/>
    <path d="M36 60 L 42 78 L 36 86 L 30 78 Z" class="hi"/>
    <path d="M64 60 L 70 78 L 64 86 L 58 78 Z" class="hi"/>"""


def shield():
    return """
    <path d="M50 6 L 86 20 V 50 C 86 73, 70 88, 50 96 C 30 88, 14 73, 14 50 V 20 Z" class="a"/>
    <path d="M50 18 L 74 28 V 50 C 74 66, 63 77, 50 83 Z" class="hi"/>"""


def shield_small():
    return """
    <path d="M50 20 L 78 31 V 53 C 78 70, 66 81, 50 87 C 34 81, 22 70, 22 53 V 31 Z" class="a"/>
    <path d="M50 30 L 68 37 V 53 C 68 64, 60 72, 50 77 Z" class="hi"/>"""


def shield_layered():
    return """
    <path d="M50 4 L 90 18 V 50 C 90 74, 72 90, 50 98 C 28 90, 10 74, 10 50 V 18 Z" class="b"/>
    <path d="M50 16 L 78 26 V 50 C 78 68, 65 80, 50 87 C 35 80, 22 68, 22 50 V 26 Z" class="a"/>
    <path d="M50 28 L 66 34 V 50 C 66 61, 58 69, 50 74 Z" class="hi"/>"""


def anvil():
    return """
    <path d="M12 34 h66 l-10 16 H24 Z" class="a"/>
    <path d="M78 34 l16 8 l-16 8 Z" class="a"/>
    <path d="M40 50 h22 v16 H40 Z" class="b"/>
    <path d="M26 66 h50 v12 H26 Z" class="a"/>
    <path d="M18 78 h66 v8 H18 Z" class="b"/>
    <path d="M20 38 h40 l-5 8 H26 Z" class="hi"/>"""


def hammer():
    return """
    <path d="M18 18 h46 v26 H18 Z" class="a"/>
    <path d="M64 22 h14 v18 H64 Z" class="b"/>
    <path d="M22 22 h20 v10 H22 Z" class="hi"/>
    <path d="M36 44 h10 v48 H36 Z" class="b"/>"""


def gauntlet():
    return """
    <path d="M28 16 h44 v22 H28 Z" class="a"/>
    <path d="M24 42 h52 v18 H24 Z" class="a"/>
    <path d="M28 64 h44 v16 H28 Z" class="b"/>
    <path d="M36 84 h28 v10 H36 Z" class="b"/>
    <path d="M32 20 h22 v10 H32 Z" class="hi"/>"""


def gauge():
    return """
    <path d="M50 12 A 38 38 0 0 1 88 50 h-14 A 24 24 0 0 0 50 26 Z" class="b"/>
    <path d="M50 12 A 38 38 0 0 0 12 50 h14 A 24 24 0 0 1 50 26 Z" class="a"/>
    <path d="M50 50 L 76 30 l5 7 L 54 58 Z" class="hi"/>
    <circle cx="50" cy="52" r="9" class="a"/>
    <path d="M16 62 h68 v10 H16 Z" class="b"/>"""


def vent():
    return """
    <path d="M14 18 h72 a8 8 0 0 1 8 8 v48 a8 8 0 0 1 -8 8 H14 a8 8 0 0 1 -8 -8 V26 a8 8 0 0 1 8 -8 Z" class="b"/>
    <path d="M18 30 h64 v8 H18 Z" class="a"/>
    <path d="M18 46 h64 v8 H18 Z" class="a"/>
    <path d="M18 62 h64 v8 H18 Z" class="a"/>
    <path d="M18 30 h64 v4 H18 Z" class="hi"/>"""


def burst():
    return """
    <path d="M50 2 L 60 32 L 90 22 L 68 46 L 98 56 L 66 60 L 78 92 L 50 70
             L 22 92 L 34 60 L 2 56 L 32 46 L 10 22 L 40 32 Z" class="a"/>
    <path d="M50 30 L 57 47 L 74 52 L 57 57 L 50 74 L 43 57 L 26 52 L 43 47 Z" class="hi"/>"""


def meltdown():
    return """
    <circle cx="50" cy="52" r="34" class="a"/>
    <path d="M50 4 L 58 26 L 42 26 Z" class="a"/>
    <path d="M96 52 L 74 60 L 74 44 Z" class="a"/>
    <path d="M50 100 L 42 78 L 58 78 Z" class="a"/>
    <path d="M4 52 L 26 44 L 26 60 Z" class="a"/>
    <circle cx="50" cy="52" r="16" class="hi"/>"""


def heat_fins():
    return """
    <path d="M12 22 h76 v10 H12 Z" class="a"/>
    <path d="M12 40 h76 v10 H12 Z" class="a"/>
    <path d="M12 58 h76 v10 H12 Z" class="a"/>
    <path d="M12 76 h76 v10 H12 Z" class="b"/>
    <path d="M16 22 h30 v4 H16 Z" class="hi"/>"""


def bellows():
    return """
    <path d="M14 26 L 66 44 L 66 60 L 14 78 Z" class="a"/>
    <path d="M66 46 h26 v12 H66 Z" class="b"/>
    <path d="M20 34 L 54 46 L 54 52 L 20 44 Z" class="hi"/>"""


def poker():
    return """
    <path d="M46 6 h8 v66 h-8 Z" class="b"/>
    <path d="M50 66 C 42 76, 36 80, 36 87 a 14 14 0 0 0 28 0 C 64 80, 58 76, 50 66 Z" class="a"/>
    <path d="M38 8 h24 v8 H38 Z" class="b"/>"""


def urn():
    return """
    <path d="M28 30 h44 l-6 12 C 78 50, 80 70, 66 84 H34 C 20 70, 22 50, 34 42 Z" class="a"/>
    <path d="M22 22 h56 v10 H22 Z" class="b"/>
    <path d="M38 48 C 34 56, 34 66, 40 74 H 36 C 28 64, 30 52, 38 48 Z" class="hi"/>
    <path d="M50 2 C 45 12, 40 16, 40 21 a 10 10 0 0 0 20 0 C 60 16, 55 12, 50 2 Z" class="a"/>"""


def ash_cloud():
    return """
    <ellipse cx="34" cy="56" rx="24" ry="18" class="a"/>
    <ellipse cx="64" cy="50" rx="26" ry="20" class="a"/>
    <ellipse cx="50" cy="68" rx="30" ry="16" class="b"/>
    <ellipse cx="36" cy="48" rx="12" ry="8" class="hi"/>"""


def ember_seed():
    return """
    <path d="M50 10 C 30 26, 20 44, 26 62 C 32 80, 50 88, 50 88 C 50 88, 68 80, 74 62
             C 80 44, 70 26, 50 10 Z" class="a"/>
    <circle cx="50" cy="58" r="14" class="hi"/>"""


def eye():
    return """
    <path d="M6 50 C 24 26, 76 26, 94 50 C 76 74, 24 74, 6 50 Z" class="a"/>
    <circle cx="50" cy="50" r="18" class="b"/>
    <circle cx="50" cy="50" r="8" class="hi"/>"""


def wind():
    return """
    <path d="M8 30 h52 a12 12 0 1 0 -12 -12" class="stroke-a"/>
    <path d="M8 50 h68 a12 12 0 1 1 -12 12" class="stroke-a"/>
    <path d="M8 70 h40 a10 10 0 1 0 -10 -10" class="stroke-hi"/>"""


def heart():
    return """
    <path d="M50 88 C 20 66, 10 48, 10 34 a 20 20 0 0 1 40 -6 a 20 20 0 0 1 40 6
             C 90 48, 80 66, 50 88 Z" class="a"/>
    <path d="M36 30 a 10 10 0 0 1 10 8 C 40 42, 34 40, 30 36 Z" class="hi"/>"""


def cards_hand():
    return """
    <path d="M16 34 l22 -8 l20 56 l-22 8 Z" class="b"/>
    <path d="M38 28 h24 v60 H38 Z" class="a"/>
    <path d="M62 26 l22 8 l-20 56 l-22 -8 Z" class="b"/>
    <path d="M44 36 h12 v22 H44 Z" class="hi"/>"""


def spiral():
    return """
    <path d="M50 8 a 42 42 0 1 1 -29 72 a 32 32 0 1 0 22 -55 a 22 22 0 1 1 15 38"
          class="stroke-a"/>
    <circle cx="50" cy="50" r="7" class="hi"/>"""


def chain():
    return """
    <ellipse cx="26" cy="34" rx="16" ry="11" class="stroke-a"/>
    <ellipse cx="50" cy="52" rx="16" ry="11" class="stroke-a"/>
    <ellipse cx="74" cy="70" rx="16" ry="11" class="stroke-hi"/>"""


def ring_engine():
    return """
    <circle cx="50" cy="50" r="40" class="stroke-b"/>
    <circle cx="50" cy="50" r="26" class="stroke-a"/>
    <path d="M50 4 h0 M50 96 h0" class="a"/>
    <path d="M44 4 h12 v14 H44 Z" class="a"/>
    <path d="M44 82 h12 v14 H44 Z" class="a"/>
    <path d="M4 44 h14 v12 H4 Z" class="a"/>
    <path d="M82 44 h14 v12 H82 Z" class="a"/>
    <circle cx="50" cy="50" r="11" class="hi"/>"""


def molten_plate():
    return """
    <path d="M50 6 L 86 20 V 50 C 86 73, 70 88, 50 96 C 30 88, 14 73, 14 50 V 20 Z" class="b"/>
    <path d="M24 40 C 36 34, 44 46, 56 40 C 68 34, 76 44, 86 38 V 50
             C 86 73, 70 88, 50 96 C 30 88, 14 73, 14 50 V 34 C 18 38, 20 42, 24 40 Z" class="a"/>
    <path d="M28 54 C 38 50, 44 58, 54 54 L 54 62 C 44 66, 38 58, 28 62 Z" class="hi"/>"""


def backdraft():
    return """
    <path d="M10 74 C 26 64, 34 44, 30 22 C 46 34, 52 56, 44 78 Z" class="a"/>
    <path d="M90 74 C 74 64, 66 44, 70 22 C 54 34, 48 56, 56 78 Z" class="a"/>
    <path d="M50 42 C 44 56, 38 62, 38 71 a 12 12 0 0 0 24 0 C 62 62, 56 56, 50 42 Z" class="hi"/>"""


def offering():
    return """
    <path d="M28 36 h44 v54 H28 Z" class="b"/>
    <path d="M34 44 h32 v38 H34 Z" class="a"/>
    <path d="M50 2 C 42 16, 34 22, 34 32 a 16 16 0 0 0 32 0 C 66 22, 58 16, 50 2 Z" class="a"/>
    <path d="M50 14 C 46 22, 43 25, 43 30 a 7 7 0 0 0 14 0 C 57 25, 54 22, 50 14 Z" class="hi"/>"""


def ringed(inner_name):
    """
    A Power ring with a smaller symbol inside.

    The ring means "this stays for the rest of the combat"; the mark inside says what it
    acts on. Six Powers sharing one icon taught the player nothing — this way the shared
    part and the distinct part each carry information.
    """
    inner = SYMBOLS[inner_name]()
    return f"""
    <circle cx="50" cy="50" r="44" class="stroke-b"/>
    <circle cx="50" cy="50" r="36" class="stroke-a"/>
    <path d="M44 2 h12 v12 H44 Z" class="b"/>
    <path d="M44 86 h12 v12 H44 Z" class="b"/>
    <path d="M2 44 h12 v12 H2 Z" class="b"/>
    <path d="M86 44 h12 v12 H86 Z" class="b"/>
    <g transform="translate(26,26) scale(0.48)">{inner}</g>"""


SYMBOLS = {
    "flame": flame, "double_flame": double_flame, "wildfire": wildfire,
    "sword": sword, "greatsword": greatsword, "dagger": dagger,
    "twin_fangs": twin_fangs, "slashes": slashes, "sparks": sparks,
    "shield": shield, "shield_small": shield_small, "shield_layered": shield_layered,
    "anvil": anvil, "hammer": hammer, "gauntlet": gauntlet, "molten_plate": molten_plate,
    "gauge": gauge, "vent": vent, "burst": burst, "meltdown": meltdown,
    "heat_fins": heat_fins, "bellows": bellows, "poker": poker,
    "urn": urn, "ash_cloud": ash_cloud, "ember_seed": ember_seed, "offering": offering,
    "eye": eye, "wind": wind, "heart": heart, "cards_hand": cards_hand,
    "spiral": spiral, "chain": chain, "ring_engine": ring_engine, "backdraft": backdraft,
}


# ── Card -> symbol mapping ───────────────────────────────────────────────────────
# Cards that share a symbol are meant to: Bulwark and Reinforce are both "more shield",
# and a shared silhouette tells the player they belong together.

CARDS = [
    # id, symbol, hue
    ("strike", "sword", "neutral"),
    ("guard", "shield", "neutral"),
    ("ember_lash", "flame", "pyre"),
    ("stoke", "poker", "overdrive"),

    ("bulwark", "shield_layered", "forge"),
    ("anvil_strike", "anvil", "forge"),
    ("brace", "shield_small", "forge"),
    ("temper", "gauntlet", "forge"),

    ("twin_fangs", "twin_fangs", "swarm"),
    ("whetstone", "hammer", "swarm"),
    ("flurry", "slashes", "swarm"),
    ("quick_jab", "dagger", "swarm"),

    ("kindle", "flame", "pyre"),
    ("scorch", "double_flame", "pyre"),
    ("fan_the_flames", "bellows", "pyre"),
    ("smoulder", "ash_cloud", "pyre"),

    ("bellows", "bellows", "overdrive"),
    ("vent", "vent", "overdrive"),
    ("flare", "burst", "overdrive"),
    ("heat_sink", "heat_fins", "overdrive"),

    ("cremate", "urn", "ashfall"),
    ("ash_cloud", "ash_cloud", "ashfall"),
    ("salvage", "cards_hand", "ashfall"),

    ("focus", "eye", "neutral"),
    ("second_wind", "wind", "neutral"),
    ("mend", "heart", "neutral"),

    ("reinforce", "shield_layered", "forge"),
    ("counterweight", "hammer", "forge"),
    ("ironhide", "gauntlet", "forge"),
    ("forge_rite", "ring:shield", "forge"),

    ("cinder_storm", "sparks", "swarm"),
    ("rising_heat", "ring:sword", "swarm"),
    ("rain_of_sparks", "sparks", "swarm"),
    ("frenzy", "spiral", "swarm"),

    ("wildfire", "wildfire", "pyre"),
    ("bellows_blast", "bellows", "pyre"),
    ("slow_roast", "flame", "pyre"),
    ("immolate", "wildfire", "pyre"),
    ("backdraft", "backdraft", "pyre"),

    ("detonate", "burst", "overdrive"),
    ("heat_shield", "shield", "overdrive"),
    ("overclock", "gauge", "overdrive"),
    ("coolant", "vent", "overdrive"),
    ("thermal_mass", "gauge", "overdrive"),

    ("pyre_rite", "ring:urn", "ashfall"),
    ("burnt_offering", "offering", "ashfall"),
    ("ash_armor", "shield_small", "ashfall"),

    ("molten_armor", "molten_plate", "forge"),
    ("living_anvil", "anvil", "forge"),
    ("thousand_cuts", "chain", "swarm"),
    ("searing_blade", "sword", "swarm"),
    ("conflagration", "wildfire", "pyre"),
    ("eternal_flame", "ember_seed", "pyre"),
    ("meltdown", "meltdown", "overdrive"),
    ("perpetual_flame", "ring:flame", "overdrive"),
    ("ember_engine", "ring:gauge", "overdrive"),
    ("phoenix_ash", "ember_seed", "ashfall"),
    ("cinder_trance", "spiral", "ashfall"),
    ("second_forge", "ring:anvil", "neutral"),
    ("last_ember", "greatsword", "neutral"),
    # Found only in the Emberheart (Act 3).
    ("heartfire", "heart", "overdrive"),
    ("molten_core", "meltdown", "overdrive"),

    # Unlocked between runs
    ("crucible", "shield_layered", "forge"),
    ("brand", "poker", "pyre"),
    ("kiln_guard", "ring:gauntlet", "forge"),
    ("ember_scatter", "sparks", "swarm"),
    ("blade_dance", "slashes", "swarm"),
    ("momentum", "spiral", "neutral"),
    ("magma_heart", "ring:heart", "pyre"),
    ("supernova", "meltdown", "overdrive"),
    ("phoenix_plume", "ember_seed", "ashfall"),
]


def tile(index, card_id, symbol_name, hue):
    light, mid, dark = HUES[hue]
    col, row = index % COLUMNS, index // COLUMNS
    x, y = col * TILE, row * TILE

    body = (ringed(symbol_name.split(":", 1)[1]) if symbol_name.startswith("ring:")
            else SYMBOLS[symbol_name]())
    gid = f"g{index}"

    # Symbols are authored in a 100x100 box; scale into the tile with a margin so nothing
    # touches the slice boundary when ffmpeg cuts the atlas apart.
    scale = (TILE - 56) / 100.0
    offset = 28

    return f"""
  <g id="{card_id}" transform="translate({x + offset},{y + offset}) scale({scale:.4f})">
    <defs>
      <linearGradient id="{gid}" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0%" stop-color="{light}"/>
        <stop offset="100%" stop-color="{mid}"/>
      </linearGradient>
    </defs>
    <style>
      #{card_id} .a  {{ fill: url(#{gid}); stroke: {dark}; stroke-width: 3.2; stroke-linejoin: round; }}
      #{card_id} .b  {{ fill: {mid}; stroke: {dark}; stroke-width: 3.2; stroke-linejoin: round; }}
      #{card_id} .hi {{ fill: {light}; stroke: none; opacity: 0.85; }}
      #{card_id} .stroke-a  {{ fill: none; stroke: url(#{gid}); stroke-width: 9; stroke-linecap: round; }}
      #{card_id} .stroke-b  {{ fill: none; stroke: {dark}; stroke-width: 9; stroke-linecap: round; }}
      #{card_id} .stroke-hi {{ fill: none; stroke: {light}; stroke-width: 7; stroke-linecap: round; }}
    </style>
    {body}
  </g>"""


def build_atlas():
    rows = (len(CARDS) + COLUMNS - 1) // COLUMNS
    width, height = COLUMNS * TILE, rows * TILE

    tiles = "".join(tile(i, cid, sym, hue) for i, (cid, sym, hue) in enumerate(CARDS))
    svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" '
           f'viewBox="0 0 {width} {height}">{tiles}\n</svg>\n')

    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / "cards_atlas.svg"
    path.write_text(svg)

    manifest = OUT / "cards_atlas.txt"
    manifest.write_text("\n".join(f"{i}\t{cid}" for i, (cid, _, _) in enumerate(CARDS)) + "\n")

    print(f"[art] {len(CARDS)} cards, {COLUMNS}x{rows} atlas, {width}x{height}")
    print(f"[art] wrote {path}")
    return path


if __name__ == "__main__":
    build_atlas()
    used = {s.split(":", 1)[1] if s.startswith("ring:") else s for _, s, _ in CARDS}
    unknown = sorted(used - set(SYMBOLS))
    if unknown:
        print(f"[art] ERROR unknown symbols: {unknown}", file=sys.stderr)
        sys.exit(1)
