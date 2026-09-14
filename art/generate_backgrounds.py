"""
Painted backgrounds: the battlefield for hallway fights, elites and the boss, and the map.

    python3 -u art/generate_backgrounds.py

A background sits behind text, cards and numbers the player must read, so every prompt asks for an
empty scene — no figures, nothing in the middle — dark enough that the interface stays the brightest
thing on screen. Rendered wide at SDXL's native 1344x768 and scaled to the game's 1920x1080 on install.
"""
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import generate_art as art  # noqa: E402

EMPTY = ("empty scene, wide establishing shot, dark vignette, dark floor, "
         "low contrast in the centre, atmospheric depth")

# What every background must avoid, on top of the generator's own negatives. The first elite render
# signed itself in both bottom corners and put a bright white floor exactly where the cards sit.
AVOID = "people, person, figure, creature, character, text, signature, lettering, bright floor, white ground"

BACKGROUNDS = {
    "bg_hallway": "interior of a vast ancient forge cavern, cooled lava channels glowing faintly along the floor, "
                  "blackened stone arches fading into smoke, drifting embers, " + EMPTY,
    # "Falling ash like snow" painted a winter: snow on the floor, bare trees, a white glare. Ash is
    # now described as smoke and cinders, and snow is in the negatives.
    "bg_elite": "ruined temple hall of black obsidian pillars, dim violet firelight between the columns, "
                "drifting grey smoke and faint cinders, cracked dark stone floor, ominous, " + EMPTY,
    "bg_boss": "colossal throne room inside a volcano, a great forge of molten iron behind an empty throne, "
               "rivers of lava far below, red and orange glow, overwhelming scale, " + EMPTY,
    "bg_map": "top-down view of a dark volcanic wasteland at night, faint glowing trails of cooling lava, "
              "ash plains and black rock, like an old map seen from above, muted and dark, " + EMPTY,

    # Act 2, the Obsidian Deep: below the forge, black glass and violet crystal light.
    "bg_hallway_2": "deep underground cavern of black volcanic glass, violet crystal clusters glowing faintly "
                    "in the walls, thin rivers of magma far below, drifting cinders, " + EMPTY,
    "bg_elite_2": "vast cathedral-like cave of towering obsidian spires, cold violet crystal light, "
                  "dark smoke, cracked black glass floor, ominous, " + EMPTY,
    "bg_boss_2": "enormous volcanic lair deep underground, a lake of molten lava ringed by black obsidian cliffs, "
                 "violet crystals high above, red glow and heat haze, overwhelming scale, " + EMPTY,
    "bg_map_2": "top-down view of deep dark caverns of black glass, faint violet crystal veins and thin glowing "
                "magma channels, like an old map seen from above, muted and dark, " + EMPTY,
}

# (colours wanted, things to avoid), in the generator's palette format.
PALETTES = {
    "bg_hallway": ("deep orange and red firelight, dark stone", AVOID),
    "bg_elite": ("ash grey and violet palette, cold purple firelight, dark", AVOID + ", snow, ice, winter, frost, bare trees"),
    "bg_boss": ("molten red and orange glow, black iron", AVOID),
    "bg_map": ("muted dark earth tones, faint orange glow", AVOID + ", snow, ice"),
    "bg_hallway_2": ("black obsidian and deep violet palette, faint orange magma glow", AVOID + ", snow, ice"),
    "bg_elite_2": ("black and cold violet palette, dim crystal light", AVOID + ", snow, ice, winter"),
    "bg_boss_2": ("molten red lava and black obsidian, violet highlights", AVOID),
    "bg_map_2": ("very dark violet and black tones, faint orange glow", AVOID + ", snow, ice"),
}

WIDTH, HEIGHT = 1344, 768


def main():
    for i, (name, prompt) in enumerate(BACKGROUNDS.items()):
        destination = art.OUT / f"{name}.png"
        if destination.exists():
            print(f"[bg] {name}: already present, skipping")
            continue
        ok = art.render(prompt, 101 + i * 1000, destination, width=WIDTH, height=HEIGHT, palette=PALETTES[name])
        print(f"[bg] {name}: {'ok' if ok else 'FAILED'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
