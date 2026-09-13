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
}


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
        palette = PALETTES.get(archetype_of(name)) if name in CARDS else None
        ok = render(prompt, args.seed + i * 1000, destination, palette=palette)
        print(f"[art] [{i + 1}/{total}] {name}: {'ok' if ok else 'FAILED'} "
              f"({time.time() - start:.0f}s)")

    print(f"[art] output in {OUT}")
    return 0


if __name__ == "__main__":
    import urllib.parse  # noqa: E402  (used by fetch_image)
    sys.exit(main())
