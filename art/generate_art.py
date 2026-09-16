"""
Generates painted card and enemy art with the local ComfyUI install.

    python3 art/generate_art.py --test          # three style probes
    python3 art/generate_art.py --cards         # all 60 card illustrations
    python3 art/generate_art.py --enemies       # enemy portraits

Requires the ComfyUI server running:
    cd ~/Desktop/ComfyUI && ./venv/bin/python main.py --port 8188

Everything runs on this machine — the models, the sampler, the output. No service is called
and nothing leaves the disk.

Style is defined once, in STYLE, so all sixty pieces read as one set. That consistency is
what separates a deck from a folder of nice pictures: the player has to believe the cards
belong to the same game.
"""

import argparse
import json
import pathlib
import sys
import time
import urllib.error
import urllib.request

SERVER = "http://127.0.0.1:8188"
OUT = pathlib.Path(__file__).parent / "out" / "painted"

# DreamShaper XL Lightning: a merged lightning checkpoint, so 8 steps at CFG 2 instead of
# 30 at CFG 7. On this machine that is the difference between a 20-minute batch and three
# hours, at a quality difference nobody will see at card size.
CHECKPOINT = "dreamshaperXL_lightningDPMSDE.safetensors"
STEPS = 8
CFG = 2.0
SAMPLER = "dpmpp_sde"
SCHEDULER = "karras"
SIZE = 1024

# One style contract for the whole set — everything except the palette, which is per
# archetype.
STYLE = ("digital painting, fantasy trading card art, dramatic rim lighting, "
         "rich saturated colour, deep shadows, volumetric glow, ornate, highly detailed, "
         "painterly brushwork, centred composition, dark background, masterpiece")

BASE_NEGATIVE = ("text, watermark, signature, logo, ui, frame, border, letters, words, "
                 "blurry, low contrast, flat lighting, washed out, pale, desaturated, "
                 "photo, photograph, 3d render, plastic, deformed, extra limbs, ugly")

# Colour carries information in this game: a card's archetype is meant to be readable before
# its text is. The first pass put "molten ember palette" in the global style, which made all
# sixty cards orange and threw that information away.
#
# Temperature is the axis that makes the split feel earned rather than arbitrary —
# blue-white steel is hotter than orange fire, magenta is past the point of control, and ash
# is what is left when the heat has gone.
PALETTES = {
    # "frost" pulled snow into the first Forge image. The blue is right — cold iron and
    # blue-white heat — but this world has no winter in it, so the word has to go and the
    # negative has to say so.
    "forge":     ("cold steel blue and cyan palette, blue-white heat, quenched dark iron, "
                  "deep blue forge glow", "orange, red, warm colours, snow, ice, winter"),
    "swarm":     ("golden amber palette, brass and bright gold highlights, warm yellow "
                  "sparks", "red, magenta, blue"),
    "pyre":      ("molten ember palette, deep orange and red fire, glowing lava", "blue, grey"),
    "overdrive": ("hot magenta and crimson palette, pink-white plasma glow, overheated "
                  "energy", "orange, green, blue"),
    "ashfall":   ("ash grey and violet palette, cold pale light, muted purple smoke, "
                  "bone white", "orange, saturated fire, yellow"),
    "neutral":   ("warm neutral palette, bone, iron and dull gold", "neon, magenta"),
    # Act 2, the Obsidian Deep: black glass and violet crystal light, with molten orange kept as an
    # accent, so its creatures read as belonging somewhere deeper than the forge without leaving
    # the fire behind.
    "deep":      ("black obsidian and deep violet crystal glow palette, cold purple light, "
                  "molten orange accents", "snow, ice, winter, green, daylight, woman, girl, human face, hair"),
    # Act 3, the Emberheart: the source of the fire. White-hot at the centre, gold and orange
    # around it — brighter and paler than the forge above, and nothing violet, so an Act 3
    # creature never reads as an Act 2 one.
    "heart":     ("white-hot and gold palette, incandescent yellow-white core, molten gold and orange, "
                  "blinding heat glow", "violet, purple, blue, cold colours, snow, ice, daylight"),
}


def archetype_of(card_id):
    """Single source of truth: the same table the SVG symbols and the design doc use."""
    import card_art
    for cid, _symbol, hue in card_art.CARDS:
        if cid == card_id:
            return hue
    return "neutral"


def workflow(prompt, seed, width=SIZE, height=SIZE, palette=None):
    """A plain SDXL text-to-image graph in ComfyUI's API format."""
    if palette is None:
        palette = PALETTES["neutral"]
    colour, avoid = palette
    positive = f"{prompt}, {colour}, {STYLE}"
    negative = f"{BASE_NEGATIVE}, {avoid}"
    return {
        "4": {"class_type": "CheckpointLoaderSimple",
              "inputs": {"ckpt_name": CHECKPOINT}},
        "6": {"class_type": "CLIPTextEncode",
              "inputs": {"text": positive, "clip": ["4", 1]}},
        "7": {"class_type": "CLIPTextEncode",
              "inputs": {"text": negative, "clip": ["4", 1]}},
        "5": {"class_type": "EmptyLatentImage",
              "inputs": {"width": width, "height": height, "batch_size": 1}},
        "3": {"class_type": "KSampler",
              "inputs": {"seed": seed, "steps": STEPS, "cfg": CFG,
                         "sampler_name": SAMPLER, "scheduler": SCHEDULER, "denoise": 1.0,
                         "model": ["4", 0], "positive": ["6", 0], "negative": ["7", 0],
                         "latent_image": ["5", 0]}},
        "8": {"class_type": "VAEDecode",
              "inputs": {"samples": ["3", 0], "vae": ["4", 2]}},
        "9": {"class_type": "SaveImage",
              "inputs": {"filename_prefix": "emberdeck", "images": ["8", 0]}},
    }


def post(path, payload):
    request = urllib.request.Request(
        f"{SERVER}{path}", data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.loads(response.read())


def get(path):
    with urllib.request.urlopen(f"{SERVER}{path}", timeout=30) as response:
        return json.loads(response.read())


def fetch_image(info):
    query = (f"/view?filename={urllib.parse.quote(info['filename'])}"
             f"&subfolder={urllib.parse.quote(info.get('subfolder', ''))}"
             f"&type={info.get('type', 'output')}")
    with urllib.request.urlopen(f"{SERVER}{query}", timeout=60) as response:
        return response.read()


def render(prompt, seed, destination, width=SIZE, height=SIZE, timeout=420, palette=None):
    """Queues one image and blocks until ComfyUI reports it done."""
    graph = workflow(prompt, seed, width, height, palette)
    prompt_id = post("/prompt", {"prompt": graph})["prompt_id"]

    deadline = time.time() + timeout
    while time.time() < deadline:
        history = get(f"/history/{prompt_id}")
        if prompt_id in history:
            outputs = history[prompt_id]["outputs"]
            for node in outputs.values():
                for image in node.get("images", []):
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    destination.write_bytes(fetch_image(image))
                    return True
            return False
        time.sleep(1.5)

    print(f"[art] timed out: {destination.name}", file=sys.stderr)
    return False


# ── Subjects ─────────────────────────────────────────────────────────────────────

ENEMIES = {
    "emberling": "a small round fire elemental creature made of glowing molten rock, "
                 "cracked obsidian skin with lava veins, two bright eyes, floating sparks",
    "cinder_rat": "a fierce rat beast with smouldering ember fur and burning red eyes, "
                  "charred whiskers, cracks of fire along its spine, crouched and snarling",
    "ash_hound": "a monstrous four-legged hound of ash and violet flame, glowing eyes, "
                 "smoke curling from its jaws, bone-like plates, imposing and predatory",
    "forge_tyrant": "a colossal armoured forge titan of blackened iron and molten seams, "
                    "enormous hammer arm, furnace burning in its chest, towering and regal, "
                    "boss monster, imposing scale",
    "ash_mite": "a small skittering insect creature of grey ash and glowing ember joints, "
                "many thin legs, cinders drifting from its shell, mandibles glowing orange",
    "slag_beetle": "a huge armoured beetle with a shell of cooled black slag and molten cracks, "
                   "heavy horned head lowered to charge, immovable and ancient",
    "kiln_imp": "a mischievous grinning fire imp perched on a clay kiln, cupped hands full of "
                "boiling orange flame, sharp ears, glowing yellow eyes",
    "ash_wraith": "a hovering spectral wraith of pale ash and cold blue smoke, hollow glowing eyes, "
                  "tattered shroud dissolving into drifting embers, reaching claws, eerie",
    "cinder_cultist": "a hooded cultist in scorched crimson robes, face hidden in shadow, "
                      "holding a ritual dagger over a burning brazier, glowing runes",
    "molten_golem": "a massive golem of cracked basalt with rivers of lava pouring through its body, "
                    "huge fists dripping magma, steam rising, elite monster",
    "salamander": "a sleek fire salamander lizard with glossy black and orange scales, "
                  "flames running along its spine and tail, coiled to strike, fierce eyes",
}

# Act 2, the Obsidian Deep. Kept as their own table so they take the "deep" palette, and merged into
# ENEMIES below so install_painted.sh files them as enemy portraits.
ACT2_ENEMIES = {
    # "A faint ghostly face" painted a woman with flaming hair. A wisp is a light, not a person.
    # The second try painted a candle inside a glass ball: an object, not a creature. It is now a living
    # flame with eyes, and glass, orbs and candles are named as what it is not.
    "ember_wisp": "a floating will-o'-the-wisp fire spirit creature, a small living flame with two glowing eyes "
                  "and wispy flame tendrils like arms, hovering in a dark cavern, violet and blue-white fire, "
                  "not a glass orb, no candle",
    "magma_leech": "a bloated segmented leech of cooling magma and black crust, round mouth ringed with "
                   "glowing teeth, dripping molten drops, clinging to black rock",
    "obsidian_sentinel": "a towering guardian statue of polished black obsidian with glowing violet cracks, "
                         "stone halberd held across its body, cold glowing eyes, immovable",
    "ashen_knight": "a hollow knight in cracked ash-grey plate armour, embers glowing inside its visor, "
                    "heavy greatsword, cold violet light, menacing",
    "cinder_shaman": "a hunched goblin shaman in bone and ash robes, staff crowned with a burning skull, "
                     "swirling runes of violet fire around its hands",
    "obsidian_colossus": "a massive colossus of stacked obsidian boulders, violet crystal core blazing in its "
                         "chest, huge stone fists, elite monster, imposing",
    "cinder_wyrm": "a colossal serpentine wyrm dragon of black obsidian scales with magma glowing between them, "
                   "coiled in a volcanic cavern, jaws open breathing fire, boss monster, imposing scale",
}
ENEMIES.update(ACT2_ENEMIES)

# Act 3, the Emberheart. Its own table for the "heart" palette, merged into ENEMIES so
# install_painted.sh files them as enemy portraits.
ACT3_ENEMIES = {
    "cinder_revenant": "a gaunt burning revenant risen from ash, cracked charcoal body with white-hot fissures, "
                       "hollow blazing eye sockets, reaching clawed hands, wreathed in rising heat",
    "molten_maw": "a hulking eyeless beast that is mostly mouth, enormous jaws of glowing cracked stone lined with "
                  "white-hot teeth, squat heavy body, drooling molten rock",
    "ash_priest": "a tall gaunt priest in pale ash-white ceremonial robes and a horned golden mask, holding a "
                  "swinging censer that pours burning embers, arms raised, solemn",
    "emberfly": "a single large glowing insect of living fire, four bright translucent wings trailing sparks, "
                "slender ember body, hovering, small creature",
    "slag_titan": "a colossal broad-shouldered titan built of cooled slag and iron scrap, white-hot seams between "
                  "its plates, tiny head sunk between huge shoulders, standing heavy and still",
    "living_flame": "a towering humanoid figure made entirely of white-gold fire, no skin and no armour, flame "
                    "streaming upward from its shoulders, two blazing eyes, elite monster, imposing",
    "emberheart": "a colossal burning heart of the world suspended in a chamber of molten gold, a vast pulsing "
                  "core of white-hot fire held in a cage of blackened iron ribs, arcs of flame reaching out, "
                  "boss monster, overwhelming scale, no face, not a person",
}
ENEMIES.update(ACT3_ENEMIES)


# One prompt per card. Each describes what the card DOES, not what it is called: a player
# should be able to guess "Detonate" from a contained blast and "Vent" from a pressure
# release without reading either name. Art that only illustrates the title teaches nothing.
CARDS = {
    # Starter
    "strike": "a burning sword slashing forward, molten cutting edge trailing sparks",
    "guard": "an iron tower shield planted in the ground, embers glancing off its face",
    "ember_lash": "a whip of living fire cracking through the air, trailing burning cinders",
    "stoke": "a blacksmith iron poker plunged deep into white-hot coals, heat surging up",

    # Forge
    "bulwark": "a massive fortress gate of blackened riveted iron, firelight behind it",
    "anvil_strike": "a glowing forge anvil struck by a molten hammer, sparks exploding outward",
    "brace": "a small round buckler raised fast, sparks glancing off the rim",
    "temper": "a sword blade quenched in oil, steam bursting upward, metal darkening",
    "reinforce": "layered steel plates locking together into a thickening shield wall",
    "counterweight": "an enormous iron counterweight swinging down on a heavy chain",
    "ironhide": "armour plates growing over skin like scales, heated iron fusing shut",
    "forge_rite": "a ritual forge circle carved into stone, molten runes glowing along its ring",
    "molten_armor": "armour of flowing molten metal, permanently liquid and glowing",
    "living_anvil": "an anvil come alive with iron limbs, forge fire burning in its core",

    # Swarm
    "twin_fangs": "two curved blades striking in unison, twin arcs of fire crossing",
    "whetstone": "a whetstone drawing a shower of sparks along a long blade edge",
    "flurry": "a blur of three rapid blade strikes, overlapping arcs of light",
    "quick_jab": "a short dagger flicking forward, precise, a small burst of sparks",
    "cinder_storm": "a whirling storm of burning cinders sweeping across a battlefield",
    "rising_heat": "heat shimmer rising from a rack of weapons, blades glowing brighter",
    "rain_of_sparks": "a rain of white-hot sparks falling like a volley of arrows",
    "frenzy": "a frenzied spinning attack, a spiral of overlapping blade trails",
    "thousand_cuts": "countless tiny blade cuts flashing in the dark, a swarm of light",
    "searing_blade": "a blade wreathed in white-hot searing fire, metal glowing through",

    # Pyre
    "kindle": "a bundle of kindling catching fire, first flames licking upward",
    "scorch": "a scorched handprint burned deep into stone, smoke curling from it",
    "fan_the_flames": "a great bellows fanning a spreading wall of flame outward",
    "smoulder": "a thick smouldering ember buried in grey ash, deep red inner glow",
    "wildfire": "a wildfire racing across a dark field, a wall of orange flame",
    "bellows_blast": "a blast from a forge bellows erupting into a rolling fireball",
    "slow_roast": "a slow steady flame under a blackened iron cauldron, unending heat",
    "immolate": "a pillar of white-hot fire consuming everything at its centre",
    "backdraft": "a backdraft explosion bursting through a doorway, rolling fire",
    "conflagration": "an entire landscape engulfed in a towering conflagration",
    "eternal_flame": "an undying flame in an ornate brazier, tendrils reaching outward",

    # Overdrive
    "bellows": "a leather forge bellows pumping air over glowing coals",
    "vent": "pressure vents releasing a roaring jet of steam and flame",
    "flare": "a signal flare erupting in blinding orange light",
    "heat_sink": "iron cooling fins glowing red, heat bleeding off in visible waves",
    "detonate": "a contained explosion bursting outward, a shockwave of fire",
    "heat_shield": "a shimmering barrier of pure heat, the air warping around it",
    "overclock": "a pressure gauge with its needle slammed into the red, glass cracking",
    "coolant": "coolant flooding over glowing metal, a violent cloud of steam",
    "thermal_mass": "a massive block of heated iron absorbing enormous heat",
    "meltdown": "a reactor core melting down, blinding white light and molten metal",
    "perpetual_flame": "an eternal flame burning in an ornate brazier, never dying",
    "ember_engine": "an intricate brass engine of gears driven by burning embers",

    # Ashfall
    "cremate": "a funeral urn with ash swirling upward into the dark",
    "ash_cloud": "a choking cloud of grey ash rolling forward, embers inside it",
    "salvage": "burnt parchment and scraps pulled from a fire, edges still glowing",
    "pyre_rite": "a ritual pyre circle, ash rising and igniting into glowing embers",
    "burnt_offering": "an offering burning on a stone altar, flames rising high",
    "ash_armor": "armour formed from compacted ash and bone, cracked with embers",
    "phoenix_ash": "a phoenix feather burning to ash with a spark of rebirth at its centre",
    "cinder_trance": "a figure meditating inside a ring of floating burning cinders",

    # Neutral
    "focus": "a single glowing eye of concentrated fire, sharp and clear",
    "second_wind": "a gust of wind reigniting dying embers back into flame",
    "mend": "a cracked iron heart being welded back together, molten seam",
    "second_forge": "a second furnace igniting beside the first, double forge glow",
    "last_ember": "a single last ember held in a dying hand, about to burst",

    # Unlocked between runs (docs/meta-progression.md)
    "crucible": "a glowing crucible of molten metal set into a heavy iron brace, heat shimmer rising",
    "brand": "a red-hot branding iron pressed against dark armour, a glowing mark seared into the metal",
    "kiln_guard": "an armoured gauntlet raised in guard, a kiln fire burning inside its iron plates",
    "ember_scatter": "a fan of burning embers flung wide across a dark battlefield, dozens of trails",
    "blade_dance": "five crossing arcs of golden blade light whirling in a circle, sparks at every cross",
    "momentum": "a spinning brass flywheel gathering speed, sparks streaming off its rim",
    "magma_heart": "a heart of molten magma glowing inside a cage of black rock, pulsing with fire",
    "supernova": "a blinding star-like explosion of pink-white plasma engulfing everything around it",
    "phoenix_plume": "a single blazing phoenix feather held up like a shield, warm light and drifting ash",
}

TEST_PROMPTS = {
    "test_emberling": ENEMIES["emberling"],
    "test_ash_hound": ENEMIES["ash_hound"],
    "test_card_anvil": "a glowing forge anvil struck by a molten hammer, sparks exploding "
                       "outward, heat haze, iron and fire",
}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--test", action="store_true")
    parser.add_argument("--enemies", action="store_true")
    parser.add_argument("--cards", action="store_true")
    parser.add_argument("--seed", type=int, default=7)
    args = parser.parse_args()

    try:
        get("/system_stats")
    except urllib.error.URLError:
        print(f"[art] ComfyUI is not answering at {SERVER}.", file=sys.stderr)
        return 1

    jobs = {}
    if args.test:
        jobs = TEST_PROMPTS
    if args.enemies:
        jobs.update(ENEMIES)
    if args.cards:
        jobs.update(CARDS)

    if not jobs:
        parser.error("pick --test, --cards or --enemies")

    total = len(jobs)
    for i, (name, prompt) in enumerate(jobs.items()):
        destination = OUT / f"{name}.png"
        # Resumable: a 60-image batch takes about ninety minutes, and losing it to one
        # interruption is not acceptable.
        if destination.exists():
            print(f"[art] {name}: already present, skipping")
            continue
        start = time.time()
        palette = (PALETTES.get(archetype_of(name)) if name in CARDS
                   else PALETTES["deep"] if name in ACT2_ENEMIES
                   else PALETTES["heart"] if name in ACT3_ENEMIES else None)
        ok = render(prompt, args.seed + i * 1000, destination, palette=palette)
        print(f"[art] [{i + 1}/{total}] {name}: {'ok' if ok else 'FAILED'} "
              f"({time.time() - start:.0f}s)")

    print(f"[art] output in {OUT}")
    return 0


if __name__ == "__main__":
    import urllib.parse  # noqa: E402  (used by fetch_image)
    sys.exit(main())
