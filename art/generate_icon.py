"""
The application icon: a few candidates, rendered with the same model and style as the card art.

    python3 -u art/generate_icon.py

An icon is read at 16 to 32 pixels on a taskbar and in a Steam library, so the prompt asks for one
bold, centred emblem on a dark ground — the opposite of a card painting, whose detail is the point.
Candidates are kept side by side because choosing an icon is a judgement about how it reads small,
which only looking at it can settle.
"""
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import generate_art as art  # noqa: E402

PROMPT = ("a single blazing ember glowing inside a blackened iron anvil, centred emblem, "
          "bold simple silhouette, strong orange firelight against a deep dark background, "
          "symmetrical composition, game icon, no text, no letters")

SEEDS = (11, 29)


def main():
    for seed in SEEDS:
        destination = art.OUT / f"app_icon_{seed}.png"
        if destination.exists():
            print(f"[icon] {destination.name}: already present, skipping")
            continue
        ok = art.render(PROMPT, seed, destination)
        print(f"[icon] {destination.name}: {'ok' if ok else 'FAILED'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
