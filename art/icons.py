"""
UI icons — statuses, intents, resources, keywords — authored as SVG and rasterised by Krita.

    ./art/render_icons.sh      (runs this, renders, slices into Assets/EmberDeck/Resources/Icons)

The card paintings are illustrations; these are the opposite. An icon is read at 26 pixels out of
the corner of an eye, so each one is a single bold silhouette in a single colour, drawn over a
dark outline that keeps it legible on top of a bright portrait. SVG for the same reasons the old
card symbols were: it is text that diffs, a restyle is one palette edit, and anyone can reproduce
it without the software that drew it.

The outline is generated, not drawn: every shape is copied behind itself in the outline colour
with a thick stroke. Hand-drawing outlines for sixteen icons would mean sixteen chances for one to
be a different weight from the rest.
"""

import pathlib
import re

TILE = 128
COLUMNS = 8
OUT = pathlib.Path(__file__).parent / "out"

OUTLINE = "#120d0a"
OUTLINE_WIDTH = 9

SHIELD = "M50 6 L86 18 L86 46 C86 70 70 86 50 95 C30 86 14 70 14 46 L14 18 Z"
FLAME_OUTER = "M50 6 C 36 32, 18 44, 18 64 a 32 32 0 0 0 64 0 C 82 44, 64 32, 50 6 Z"
FLAME_INNER = "M50 42 C 43 57, 34 63, 34 73 a 16 16 0 0 0 32 0 C 66 63, 57 57, 50 42 Z"


def sword(blade, guard, grip="#5a3a24"):
    return f"""
    <path d="M50 3 L58 15 L58 64 L42 64 L42 15 Z" fill="{blade}"/>
    <rect x="27" y="63" width="46" height="10" rx="3" fill="{guard}"/>
    <rect x="45" y="72" width="10" height="16" fill="{grip}"/>
    <circle cx="50" cy="92" r="6" fill="{guard}"/>"""


def flame(outer="#f07a2a", inner="#ffd56a"):
    return f'<path d="{FLAME_OUTER}" fill="{outer}"/><path d="{FLAME_INNER}" fill="{inner}"/>'


def thermometer(fill):
    return f"""
    <rect x="37" y="5" width="26" height="64" rx="13" fill="#f2e6da"/>
    <circle cx="50" cy="75" r="20" fill="#f2e6da"/>
    <rect x="44" y="24" width="12" height="50" rx="6" fill="{fill}"/>
    <circle cx="50" cy="75" r="13" fill="{fill}"/>"""


def bottle(liquid, highlight):
    """A round-bellied flask. One silhouette for every potion, told apart by colour alone: a player
    learns "red heals, blue guards" once and reads the belt at a glance after that."""
    return f"""
    <rect x="40" y="5" width="20" height="10" rx="3" fill="#8a6a4a"/>
    <rect x="43" y="13" width="14" height="17" fill="#dbe6ec"/>
    <path d="M42 28 L58 28 L58 34 C75 40 85 54 85 68 C85 86 70 96 50 96 C30 96 15 86 15 68 C15 54 25 40 42 34 Z" fill="#dbe6ec"/>
    <path d="M21 66 C21 60 25 54 31 50 L69 50 C75 54 79 60 79 66 C79 82 67 90 50 90 C33 90 21 82 21 66 Z" fill="{liquid}"/>
    <ellipse cx="37" cy="64" rx="6" ry="9" fill="{highlight}"/>"""


ICONS = {
    # Intents: what an enemy will do next.
    "intent_attack": f'<g transform="rotate(45 50 50)">{sword("#f4efe6", "#e0573f")}</g>',
    "intent_block": f'<path d="{SHIELD}" fill="#5f9fdc"/>'
                    '<path d="M50 18 L74 26 L74 46 C74 63 63 75 50 82 Z" fill="#a4cdf2"/>',
    "intent_buff": '<path d="M50 8 L88 46 L70 46 L50 26 L30 46 L12 46 Z" fill="#e8b94a"/>'
                   '<path d="M50 48 L88 86 L70 86 L50 66 L30 86 L12 86 Z" fill="#e8b94a"/>',
    "intent_debuff": '<path d="M50 92 L88 54 L70 54 L50 74 L30 54 L12 54 Z" fill="#b48cf0"/>'
                     '<path d="M50 52 L88 14 L70 14 L50 34 L30 14 L12 14 Z" fill="#b48cf0"/>',
    "intent_unknown": '<circle cx="50" cy="50" r="38" fill="#8a8494"/>'
                      '<circle cx="30" cy="50" r="7" fill="#231e28"/>'
                      '<circle cx="50" cy="50" r="7" fill="#231e28"/>'
                      '<circle cx="70" cy="50" r="7" fill="#231e28"/>',

    # Statuses.
    "status_burn": flame(),
    "status_strength": '<rect x="44" y="34" width="12" height="62" rx="4" fill="#7a4a2a"/>'
                       '<rect x="14" y="10" width="72" height="32" rx="6" fill="#d9573c"/>'
                       '<rect x="14" y="10" width="72" height="11" rx="5" fill="#f3906f"/>',
    "status_dexterity": f'<path d="{SHIELD}" fill="#3fb3a2"/>'
                        '<rect x="44" y="28" width="12" height="44" rx="2" fill="#e8fbf7"/>'
                        '<rect x="28" y="44" width="44" height="12" rx="2" fill="#e8fbf7"/>',
    "status_vulnerable": f'<path d="{SHIELD}" fill="#c268d8"/>'
                         '<path d="M54 12 L42 40 L57 50 L41 84" fill="none" stroke="#1b1020" '
                         'stroke-width="7" stroke-linejoin="round" stroke-linecap="round"/>',
    # Weak: a broken sword. Drawn wide and in two big pieces with a clear gap, and laid on the
    # diagonal like the attack sword — upright, it filled a narrow sliver of the square and still
    # read as a grey mark at intent size even after the blade was widened.
    "status_weak": '<g transform="rotate(45 50 50)"><g transform="rotate(-24 50 40)"><path d="M50 2 L64 18 L64 40 L36 46 L36 18 Z" fill="#c9c2d6"/></g>'
                   '<path d="M36 54 L64 48 L64 66 L36 66 Z" fill="#c9c2d6"/>'
                   '<rect x="20" y="64" width="60" height="13" rx="4" fill="#8e86a3"/>'
                   '<rect x="43" y="76" width="14" height="14" fill="#4b3a2e"/>'
                   '<circle cx="50" cy="93" r="7" fill="#8e86a3"/></g>',

    # Resources.
    "res_block": f'<path d="{SHIELD}" fill="#5f9fdc"/>'
                 '<path d="M50 18 L74 26 L74 46 C74 63 63 75 50 82 Z" fill="#a4cdf2"/>',
    "res_energy": '<path d="M60 4 L20 56 L46 56 L38 96 L82 40 L55 40 Z" fill="#f2c14e"/>',
    "res_heat": thermometer("#ef6a2e"),
    "res_overheat": thermometer("#e2302b"),

    # Card keywords.
    "kw_exhaust": '<rect x="16" y="18" width="48" height="66" rx="7" fill="#9a93a6" transform="rotate(-12 40 51)"/>'
                  f'<g transform="translate(38 30) scale(0.62)">{flame()}</g>',
    "kw_power": '<circle cx="50" cy="50" r="34" fill="none" stroke="#e86aa8" stroke-width="13"/>'
                '<path d="M50 24 L57 43 L77 43 L61 55 L67 75 L50 63 L33 75 L39 55 L23 43 L43 43 Z" fill="#f9bcd9"/>',

    # Potions: the same flask in six colours — heal, guard, fire, strength, draw, weaken.
    "potion_red": bottle("#d9433b", "#f59a8f"),
    "potion_blue": bottle("#4f86d6", "#a9c9f2"),
    "potion_orange": bottle("#ef7a2a", "#ffc07a"),
    "potion_gold": bottle("#e2b33e", "#f7df90"),
    "potion_green": bottle("#4bb46a", "#a8e6b8"),
    "potion_purple": bottle("#9a62d8", "#d3b5f5"),

    # Map nodes: what a step on the map holds, read before the player commits to a path.
    "node_fight": f'<g transform="rotate(45 50 50)">{sword("#e9e4dc", "#9a9aa6")}</g>',
    "node_elite": '<path d="M26 44 C14 30 16 10 30 8 C26 20 30 30 38 36 Z" fill="#d9b25c"/>'
                  '<path d="M74 44 C86 30 84 10 70 8 C74 20 70 30 62 36 Z" fill="#d9b25c"/>'
                  '<path d="M26 46 C26 22 74 22 74 46 L74 72 L63 72 L63 92 L37 92 L37 72 L26 72 Z" fill="#b8473a"/>'
                  '<rect x="34" y="50" width="32" height="8" rx="3" fill="#2a1410"/>'
                  '<rect x="46" y="58" width="8" height="24" rx="2" fill="#2a1410"/>',
    "node_rest": '<rect x="18" y="74" width="64" height="12" rx="6" fill="#7a4a2a" transform="rotate(-14 50 80)"/>'
                 '<rect x="18" y="74" width="64" height="12" rx="6" fill="#8d5a34" transform="rotate(14 50 80)"/>'
                 f'<g transform="translate(22 4) scale(0.56)">{flame()}</g>',
    "node_treasure": '<path d="M14 44 C14 22 86 22 86 44 Z" fill="#b8792e"/>'
                     '<rect x="14" y="44" width="72" height="44" rx="4" fill="#8f5a24"/>'
                     '<rect x="14" y="40" width="72" height="9" fill="#e2b33e"/>'
                     '<rect x="44" y="46" width="12" height="18" rx="2" fill="#f2d27a"/>',
    "node_shop": '<ellipse cx="50" cy="80" rx="32" ry="10" fill="#b8862c"/>'
                 '<ellipse cx="50" cy="72" rx="32" ry="10" fill="#d9a53b"/>'
                 '<ellipse cx="50" cy="58" rx="28" ry="9" fill="#b8862c"/>'
                 '<ellipse cx="50" cy="50" rx="28" ry="9" fill="#e8bd4f"/>'
                 '<ellipse cx="50" cy="34" rx="24" ry="8" fill="#b8862c"/>'
                 '<ellipse cx="50" cy="26" rx="24" ry="8" fill="#f2d27a"/>',
    "node_event": '<circle cx="50" cy="50" r="40" fill="#8a63c9"/>'
                  '<path d="M37 38 C37 22 63 22 63 38 C63 50 50 50 50 62" fill="none" stroke="#f4eefc" '
                  'stroke-width="10" stroke-linecap="round" stroke-linejoin="round"/>'
                  '<circle cx="50" cy="76" r="6" fill="#f4eefc"/>',
    # Frames: 9-sliced sprites for cards, panels and buttons. Drawn in greys and white so the UI's own
    # colour carries through (a card frame takes its rarity colour, a panel its palette colour). They
    # fill their tile edge to edge with no outline, and only the corners are decorated, because
    # everything between the corners is stretched when the frame is sliced.
    "frame_card": '<path fill-rule="evenodd" fill="#d6d6dc" d="M9 0 H91 Q100 0 100 9 V91 Q100 100 91 100 H9 Q0 100 0 91 V9 Q0 0 9 0 Z '
                  'M15 12 H85 Q88 12 88 15 V85 Q88 88 85 88 H15 Q12 88 12 85 V15 Q12 12 15 12 Z"/>'
                  '<path fill="#f2f2f6" d="M9 1.5 H91 Q98.5 1.5 98.5 9 V11 H1.5 V9 Q1.5 1.5 9 1.5 Z"/>'
                  '<path fill-rule="evenodd" fill="#8f8f99" d="M13 10.5 H87 Q89.5 10.5 89.5 13 V87 Q89.5 89.5 87 89.5 H13 Q10.5 89.5 10.5 87 V13 Q10.5 10.5 13 10.5 Z '
                  'M15 12 H85 Q88 12 88 15 V85 Q88 88 85 88 H15 Q12 88 12 85 V15 Q12 12 15 12 Z"/>'
                  '<circle cx="6" cy="6" r="3.4" fill="#7a7a84"/><circle cx="94" cy="6" r="3.4" fill="#7a7a84"/>'
                  '<circle cx="6" cy="94" r="3.4" fill="#7a7a84"/><circle cx="94" cy="94" r="3.4" fill="#7a7a84"/>'
                  '<circle cx="6" cy="6" r="2.2" fill="#ffffff"/><circle cx="94" cy="6" r="2.2" fill="#ffffff"/>'
                  '<circle cx="6" cy="94" r="2.2" fill="#ffffff"/><circle cx="94" cy="94" r="2.2" fill="#ffffff"/>',
    "frame_panel": '<rect x="0" y="0" width="100" height="100" rx="7" fill="#ffffff"/>'
                   '<rect x="3" y="3" width="94" height="94" rx="5" fill="#d2d2d8"/>'
                   '<rect x="3" y="3" width="94" height="7" rx="3" fill="#e4e4ea"/>',
    "frame_button": '<rect x="0" y="0" width="100" height="100" rx="9" fill="#ffffff"/>'
                    '<rect x="3" y="3" width="94" height="94" rx="7" fill="#cfcfd6"/>'
                    '<rect x="3" y="3" width="94" height="44" rx="7" fill="#e3e3e9"/>'
                    '<rect x="3" y="86" width="94" height="11" rx="5" fill="#a6a6b0"/>',

    "node_boss": '<path d="M24 30 L32 10 L42 24 L50 6 L58 24 L68 10 L76 30 Z" fill="#e2b33e"/>'
                 '<path d="M22 54 C22 30 78 30 78 54 C78 68 70 74 66 76 L66 90 L34 90 L34 76 C30 74 22 68 22 54 Z" fill="#e6e0d4"/>'
                 '<circle cx="38" cy="54" r="8" fill="#2a1410"/><circle cx="62" cy="54" r="8" fill="#2a1410"/>'
                 '<path d="M46 66 L50 60 L54 66 Z" fill="#2a1410"/>'
                 '<rect x="40" y="80" width="4" height="10" fill="#2a1410"/><rect x="48" y="80" width="4" height="10" fill="#2a1410"/>'
                 '<rect x="56" y="80" width="4" height="10" fill="#2a1410"/>',
}

TAG = re.compile(r"<(path|rect|circle|ellipse|polygon)\b([^>]*?)/>")
ATTR = re.compile(r'([a-zA-Z-]+)="([^"]*)"')


def outlined(markup):
    """The same shapes, all in the outline colour and stroked thick — drawn first, behind."""
    def fix(match):
        attrs = dict(ATTR.findall(match.group(2)))
        if attrs.get("fill", "#000") == "none":
            attrs["stroke"] = OUTLINE
            attrs["stroke-width"] = str(float(attrs.get("stroke-width", "0")) + OUTLINE_WIDTH)
        else:
            attrs["fill"] = OUTLINE
            attrs["stroke"] = OUTLINE
            attrs["stroke-width"] = str(OUTLINE_WIDTH)
        attrs["stroke-linejoin"] = "round"
        attrs["stroke-linecap"] = "round"
        return "<%s %s/>" % (match.group(1), " ".join(f'{k}="{v}"' for k, v in attrs.items()))
    return TAG.sub(fix, markup)


def main():
    OUT.mkdir(exist_ok=True)
    rows = (len(ICONS) + COLUMNS - 1) // COLUMNS
    # A margin inside each tile, so the outline never touches the tile edge and bleeds into a
    # neighbour when the atlas is sliced.
    inner = 0.86
    offset = TILE * (1 - inner) / 2
    scale = TILE * inner / 100

    parts, index = [], []
    for i, (name, markup) in enumerate(ICONS.items()):
        if name.startswith("frame_"):
            # Frames must reach the tile's edges to slice cleanly: no margin, no outline.
            x, y = (i % COLUMNS) * TILE, (i // COLUMNS) * TILE
            parts.append(f'<g transform="translate({x} {y}) scale({TILE / 100:.4f})">{markup}</g>')
        else:
            x = (i % COLUMNS) * TILE + offset
            y = (i // COLUMNS) * TILE + offset
            parts.append(f'<g transform="translate({x:.2f} {y:.2f}) scale({scale:.4f})">{outlined(markup)}{markup}</g>')
        index.append(f"{i}\t{name}")

    width, height = COLUMNS * TILE, rows * TILE
    svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" '
           f'viewBox="0 0 {width} {height}">' + "".join(parts) + "</svg>\n")
    (OUT / "icons_atlas.svg").write_text(svg)
    (OUT / "icons_atlas.txt").write_text("\n".join(index) + "\n")
    print(f"[icons] {len(ICONS)} icons -> {OUT / 'icons_atlas.svg'}")


if __name__ == "__main__":
    main()
