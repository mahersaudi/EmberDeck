# Launch checklist

What Steam asks for, what exists, and what only the owner of the game can do. Anything marked
**you** needs a Steamworks account or a decision that is not a programming one.

## Before the page can go up

| | item | state |
|---|---|---|
| **you** | Steamworks account, Steam Direct fee (US$100), tax and banking details | not started |
| **you** | Create the app, note the App ID | not started |
| **you** | Create the Windows and macOS depots, note both IDs | not started |
| ✓ | Store copy, English | `steam/store/page-en.md` |
| ✓ | Store copy, Arabic | `steam/store/page-ar.md` |
| ✓ | Screenshots, 8 at 1920×1080 | `steam/store/screenshots/` |
| ✓ | Content survey answers, including the AI disclosure | in `page-en.md` |
| ✓ | Capsule art — 6 sizes and the library logo | `steam/store/capsules/`, first pass (see below) |
| | Trailer | needs footage of a real run |
| **you** | Age rating questionnaires (only needed for some territories) | not started |

Steam requires the page to be public as "Coming Soon" for at least two weeks before release, and a
first release cannot happen sooner than 30 days after the fee is paid. The page is the long pole,
not the build.

## Before a build can go live

| | item | state |
|---|---|---|
| ✓ | Windows and macOS release builds | `./tools/build.sh` |
| ✓ | Depot scripts | `steam/app_build.vdf` and the two depot files, with `__PLACEHOLDERS__` |
| ✓ | Launch options for both platforms | written in `steam/README.md` |
| | Run the Windows build on Windows | never done — it is built on a Mac |
| | Steam Cloud for `run.json` and `profile.json` | not implemented; leave the feature unchecked |
| | Steamworks SDK integration (achievements, rich presence) | none, and none is needed to ship |
| | Code signing and notarisation for macOS | unsigned; Gatekeeper will warn |
| | A second person's playthrough on each platform | the playtest builds are out; no results back yet |

## Things worth deciding before launch, not after

- **Price.** Nothing in the repository assumes one.
- **Steam Cloud.** Two small JSON files; the only reason it is not done is that nobody has asked.
- **Achievements.** The game already counts everything an achievement would need (`RunStats`), so
  this is a day's work whenever the App ID exists.
- **The Android build.** It is not a Steam platform. If it ships anywhere it is Google Play, which
  is a separate account, a separate fee, an app-bundle build and a privacy declaration.
- **The name.** "EmberDeck" is not checked against the trademark register or against Steam's
  existing catalogue.

## About the capsules

`art/generate_capsules.py` paints each size from the game's own checkpoint and style, then sets the
title as type, because a diffusion model cannot spell. They are a first pass, and two things about
them should not survive to launch:

- **The face is DejaVu Sans**, the game's Latin fallback — a user-interface face, not a display one.
  A designer choosing a title face is the single largest improvement available to the store page.
- **The main capsule's cards are ordinary playing cards**, spades and all. The prompt asked for cards
  of ember and ash; the model painted the cards it knows. Re-rolling the seed or naming what the cards
  are not is a five-minute fix.
