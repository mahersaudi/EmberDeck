# Art & animation pipeline

Built on what is actually installed on this machine: **Blender 5.1** and **ffmpeg 8.1**.
No ImageMagick, no Inkscape, no Python imaging libraries — and none are needed.

## The principle: art is a script

`art/render_enemies.py` defines every enemy as data and renders it. Nothing in `art/out/`
is authored by hand, so:

- A palette change is one edit and one command, not reopening a dozen source files.
- The diff of an art change is readable text.
- A new enemy is a dictionary entry.
- Nobody has to have the same software installed to review the change.

This is the same reason the card content is generated rather than committed as `.asset` YAML.

## Running it

```bash
blender --background --python art/render_enemies.py            # static sprites
blender --background --python art/render_enemies.py -- --idle  # + 12-frame idle cycles
```

Then pack a cycle into a sheet Unity can slice:

```bash
ffmpeg -y -i art/out/emberling_idle/%02d.png -filter_complex "tile=12x1" \
       art/out/emberling_idle_sheet.png
```

Output is 512×512 PNG with alpha, orthographic, front-facing.

## What this pipeline is and is not

**It is:** consistent, reproducible placeholder art with real lighting and readable
silhouettes — good enough to design, balance and demo against, and good enough that the
game stops looking like coloured rectangles.

**It is not:** character art. Primitives assembled by script give you form and silhouette;
they do not give you appeal, personality, or a style anyone will remember. When the game is
worth art, this pipeline hands an artist a clear silhouette brief and gets replaced.

Being explicit about that matters, because procedural art is seductive: it is easy to keep
polishing a script that will never produce what a person would produce in an afternoon.

## Two traps already hit, worth writing down

**Blender's default view transform is AgX**, which desaturates bright colour hard. It is
built for photographic realism and it turned a saturated orange ember into cream. Sprites
want `view_transform = "Standard"`.

**Render engine identifiers move between Blender versions** (`BLENDER_EEVEE` vs
`BLENDER_EEVEE_NEXT`). The script asks the running build what it supports rather than
hard-coding a name, so it does not break on the next upgrade.

## What does NOT belong in this pipeline

**Card frames, borders and icons** stay procedural in Unity (`UiFactory`, `Palette`). They
have to match the UI's colours exactly and scale to any resolution; baking them to PNG means
two sources of truth for one look.

**Card and UI animation** belongs at runtime, not pre-rendered. Cards sliding, lifting,
flipping and shaking are responses to game state, and no baked clip can respond. DOTween
when the time comes.

Pre-rendered frames are only correct for things with no interactive state — an enemy's idle
bob, a death puff, a spell flourish.

## Next steps, in order

1. **Import the sprites into Unity** and replace the flat colour rectangles in `EnemyView`.
2. **Sprite sheet slicing** — 12 frames horizontally; the `sprite-editor` workflow handles
   the slicing, driven by a script rather than the Sprite Editor window.
3. **Card frames** — extend `CardView` with a rarity border and a type-coloured header,
   still drawn procedurally.
4. **DOTween** for card motion, damage shake, and intent changes. This is the single
   largest perceived-quality jump available and it costs no art at all.
