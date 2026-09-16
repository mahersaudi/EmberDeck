"""
Generates the Steam capsule images: painted art from the local ComfyUI, with the title set as type.

    python3 art/generate_capsules.py              # every capsule
    python3 art/generate_capsules.py --art-only   # render the paintings, skip the type
    python3 art/generate_capsules.py --type-only  # re-set the type over paintings already rendered

Requires the ComfyUI server (see generate_art.py) and Pillow for the type. Pillow lives in
ComfyUI's own virtual environment, so this script re-executes itself there if it is missing:
nothing has to be installed for it.

Why the title is drawn here rather than prompted: a diffusion model cannot spell. Every capsule
Steam asks for has to carry the game's name legibly at sizes down to 462x174, and the only way to
get that is to draw the letters. The face is DejaVu Sans, which is already in the repository as the
game's Latin fallback font — it is a user-interface face, not a display one, and it is the first
thing a designer should replace.

Everything runs on this machine. No service is called.
"""

import argparse
import json
import os
import pathlib
import subprocess
import sys
import time
import urllib.error
import urllib.request

ROOT = pathlib.Path(__file__).parent.parent
OUT = ROOT / "steam" / "store" / "capsules"
FONT = ROOT / "Assets" / "EmberDeck" / "Resources" / "Fonts" / "DejaVuSans.ttf"
VENV_PYTHON = pathlib.Path.home() / "Desktop" / "ComfyUI" / "venv" / "bin" / "python"

SERVER = "http://127.0.0.1:8188"
CHECKPOINT = "dreamshaperXL_lightningDPMSDE.safetensors"
STEPS = 8
CFG = 2.0
SAMPLER = "dpmpp_sde"
SCHEDULER = "karras"

# The same style contract as the rest of the art, so a capsule looks like the game it sells.
STYLE = ("digital painting, fantasy trading card art, dramatic rim lighting, rich saturated colour, "
         "deep shadows, volumetric glow, ornate, highly detailed, painterly brushwork, masterpiece")
NEGATIVE = ("text, watermark, signature, logo, ui, frame, border, letters, words, numbers, "
            "blurry, low contrast, flat lighting, washed out, pale, desaturated, photo, 3d render, "
            "plastic, deformed, extra limbs, ugly, people, person, face, hands")

SUBJECT = ("a great blacksmith's forge burning in the dark, an anvil of blackened iron at the centre, "
           "playing cards of glowing ember and ash hovering in an arc above it, sparks rising, "
           "molten orange and gold light, deep shadows")

# (final size Steam wants, size to render at). Render sizes are multiples of 64 near the target's
# aspect: SDXL is trained on those, and anything else arrives soft or doubled.
CAPSULES = {
    "main_capsule":     ((1232, 706), (1216, 704)),
    "header_capsule":   ((920, 430),  (1216, 576)),
    "small_capsule":    ((462, 174),  (1344, 512)),
    "vertical_capsule": ((748, 896),  (768, 896)),
    "library_capsule":  ((600, 900),  (640, 960)),
    "library_hero":     ((3840, 1240), (1920, 640)),
}

# Where the title sits, as a fraction of the capsule, and how tall the letters are. A capsule is
# read at thumbnail size, so the name is large on the small ones and merely present on the wide
# ones, where the painting has room to carry it.
TITLE = {
    "main_capsule":     (0.5, 0.80, 0.16),
    "header_capsule":   (0.5, 0.80, 0.19),
    "small_capsule":    (0.5, 0.50, 0.34),
    "vertical_capsule": (0.5, 0.86, 0.11),
    "library_capsule":  (0.5, 0.86, 0.11),
    "library_hero":     (0.5, 0.88, 0.09),
}

NAME = "EMBERDECK"
TAGLINE = "A roguelike deckbuilder of fire and forge"


# ── Painting ─────────────────────────────────────────────────────────────────────

def workflow(prompt, seed, width, height):
    return {
        "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": CHECKPOINT}},
        "2": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["1", 1], "text": prompt}},
        "3": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["1", 1], "text": NEGATIVE}},
        "4": {"class_type": "EmptyLatentImage", "inputs": {"width": width, "height": height, "batch_size": 1}},
        "5": {"class_type": "KSampler", "inputs": {
            "model": ["1", 0], "positive": ["2", 0], "negative": ["3", 0], "latent_image": ["4", 0],
            "seed": seed, "steps": STEPS, "cfg": CFG, "sampler_name": SAMPLER,
            "scheduler": SCHEDULER, "denoise": 1.0}},
        "6": {"class_type": "VAEDecode", "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
        "7": {"class_type": "SaveImage", "inputs": {"images": ["6", 0], "filename_prefix": "capsule"}},
    }


def post(path, payload):
    request = urllib.request.Request(f"{SERVER}{path}", data=json.dumps(payload).encode(),
                                     headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.loads(response.read())


def render(prompt, seed, width, height, destination):
    """Queues one image and waits for it. Returns True when the file is written."""
    try:
        prompt_id = post("/prompt", {"prompt": workflow(prompt, seed, width, height)})["prompt_id"]
    except urllib.error.URLError as error:
        print(f"[capsules] ComfyUI is not answering on {SERVER}: {error}", file=sys.stderr)
        return False

    for _ in range(600):
        time.sleep(2)
        with urllib.request.urlopen(f"{SERVER}/history/{prompt_id}", timeout=30) as response:
            history = json.loads(response.read())
        if prompt_id not in history:
            continue

        outputs = history[prompt_id].get("outputs", {})
        for node in outputs.values():
            for image in node.get("images", []):
                query = urllib.parse.urlencode({"filename": image["filename"],
                                                "subfolder": image.get("subfolder", ""),
                                                "type": image.get("type", "output")})
                with urllib.request.urlopen(f"{SERVER}/view?{query}", timeout=60) as source:
                    destination.write_bytes(source.read())
                return True
        return False

    print("[capsules] timed out waiting for ComfyUI", file=sys.stderr)
    return False


# ── Type ─────────────────────────────────────────────────────────────────────────

def set_type(name, painting, final_size, destination):
    """Draws the title over a painting and writes the capsule at the size Steam asks for."""
    from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

    image = Image.open(painting).convert("RGB").resize(final_size, Image.LANCZOS)
    width, height = image.size

    # A dark gradient under the title, so the letters never have to compete with a bright ember.
    veil = Image.new("L", (1, height), 0)
    for y in range(height):
        t = y / max(1, height - 1)
        veil.putpixel((0, y), int(200 * max(0.0, (t - 0.35) / 0.65) ** 1.4))
    image = Image.composite(Image.new("RGB", image.size, (6, 4, 8)), image,
                            veil.resize(image.size, Image.BILINEAR))

    x_fraction, y_fraction, cap_height = TITLE[name]
    font = ImageFont.truetype(str(FONT), max(12, int(height * cap_height)))
    draw = ImageDraw.Draw(image)
    anchor = (int(width * x_fraction), int(height * y_fraction))

    # The glow is the title drawn once in orange, blurred, and laid under the letters: the same
    # light the art is full of, so the type belongs to the picture.
    glow = Image.new("RGB", image.size, (0, 0, 0))
    ImageDraw.Draw(glow).text(anchor, NAME, font=font, fill=(255, 130, 40), anchor="mm")
    glow = glow.filter(ImageFilter.GaussianBlur(max(2, int(height * 0.02))))
    image = ImageChops.add(image, glow)

    draw = ImageDraw.Draw(image)
    draw.text(anchor, NAME, font=font, fill=(255, 244, 230), anchor="mm",
              stroke_width=max(1, int(height * cap_height * 0.06)), stroke_fill=(20, 8, 4))

    # The tagline only where there is room for it to be read.
    if name in ("main_capsule", "header_capsule", "library_hero"):
        small = ImageFont.truetype(str(FONT), max(10, int(height * cap_height * 0.30)))
        draw.text((anchor[0], anchor[1] + int(height * cap_height * 0.78)), TAGLINE, font=small,
                  fill=(226, 200, 180), anchor="mm", stroke_width=1, stroke_fill=(16, 8, 4))

    image.save(destination)
    print(f"[capsules] {destination.name}: {final_size[0]}x{final_size[1]}")


def set_logo(destination):
    """The library logo: the title on transparency, nothing else."""
    from PIL import Image, ImageDraw, ImageFont

    image = Image.new("RGBA", (1280, 720), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype(str(FONT), 150)
    draw.text((640, 330), NAME, font=font, fill=(255, 246, 232, 255), anchor="mm",
              stroke_width=8, stroke_fill=(150, 60, 16, 255))
    small = ImageFont.truetype(str(FONT), 44)
    draw.text((640, 460), TAGLINE, font=small, fill=(240, 196, 150, 255), anchor="mm")
    image.save(destination)
    print(f"[capsules] {destination.name}: 1280x720 transparent")


# ── Driving ──────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--art-only", action="store_true")
    parser.add_argument("--type-only", action="store_true")
    parser.add_argument("--seed", type=int, default=98765)
    args = parser.parse_args()

    OUT.mkdir(parents=True, exist_ok=True)
    paintings = OUT / "paintings"
    paintings.mkdir(exist_ok=True)

    if not args.type_only:
        for i, (name, (_, render_size)) in enumerate(CAPSULES.items()):
            destination = paintings / f"{name}.png"
            if destination.exists():
                print(f"[capsules] {name}: painting already present, skipping")
                continue
            start = time.time()
            ok = render(SUBJECT + ", " + STYLE, args.seed + i * 1000, *render_size, destination)
            print(f"[capsules] {name}: {'painted' if ok else 'FAILED'} ({time.time() - start:.0f}s)")

    if args.art_only:
        return 0

    # Pillow is in ComfyUI's environment, not this one.
    try:
        import PIL  # noqa: F401
    except ImportError:
        if os.environ.get("CAPSULES_REEXEC") or not VENV_PYTHON.exists():
            print("[capsules] Pillow is needed for the type and was not found", file=sys.stderr)
            return 1
        print(f"[capsules] re-running under {VENV_PYTHON} for Pillow")
        return subprocess.call([str(VENV_PYTHON), __file__, "--type-only"],
                               env={**os.environ, "CAPSULES_REEXEC": "1"})

    for name, (final_size, _) in CAPSULES.items():
        painting = paintings / f"{name}.png"
        if not painting.exists():
            print(f"[capsules] {name}: no painting yet, skipped")
            continue
        set_type(name, painting, final_size, OUT / f"{name}.png")

    set_logo(OUT / "library_logo.png")
    print(f"[capsules] output in {OUT}")
    return 0


if __name__ == "__main__":
    import urllib.parse  # noqa: E402  (used by render)
    sys.exit(main())
