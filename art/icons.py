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
