# Art & animation pipeline

Built on what is actually installed on this machine: **Krita 5.3**, **Blender 5.1** and
**ffmpeg 8.1**.

A correction worth keeping: the first survey of this machine reported only Blender and
ffmpeg, and concluded that card illustrations were out of reach. That was wrong. The survey
probed for lowercase binaries on `PATH`, so it never saw `/Applications/krita.app` or
SynfigStudio. Krita rasterises SVG headlessly — which is the capability the whole card-art
pipeline below depends on.

## The principle: art is a script

`art/render_enemies.py` defines every enemy as data and renders it. Nothing in `art/out/`
is authored by hand, so:

- A palette change is one edit and one command, not reopening a dozen source files.
- The diff of an art change is readable text.
- A new enemy is a dictionary entry.
- Nobody has to have the same software installed to review the change.

This is the same reason the card content is generated rather than committed as `.asset` YAML.

## Card icons

Sixty card icons, authored as SVG in `art/card_art.py` and rasterised in one pass.

```bash
./art/render_cards.sh      # SVG -> one Krita render -> 60 sliced PNGs -> Unity
```

**Why SVG rather than a painting program:** the art is text. It diffs, it merges, a palette
change is one edit, and any contributor can regenerate it without owning the same software.

**Why symbols rather than illustrations:** sixty painted scenes is an artist's job. Sixty
symbols in one visual language is a design job, it reads better at card size than a detailed
picture would, and it carries information a picture would not — pointed means attack, layered
means block, and a ring means a Power that stays for the combat. The mark inside a Power's
ring says what it feeds, so six Powers share a silhouette without sharing an icon.

**Why one atlas and not sixty files:** Krita's startup dominates its runtime. Sixty separate
invocations take about three minutes; one 2560×1536 sheet takes two seconds, and ffmpeg
slices it.

## Running the enemy renders

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

## In the game now

Card icons and enemy sprites both load through the data assets: `CardData.Art` and
`EnemyData.Art`, assigned by `ContentGenerator` from `Assets/EmberDeck/Art/`. An
`AssetPostprocessor` forces everything under `Art/` to import as a Sprite — without it the
files arrive as plain Textures, which cannot be assigned to a UI Image at all, and the
failure shows up as a null at runtime rather than anything in the import log.

## Next steps, in order

1. **Idle animation in game** — the 12-frame cycles are rendered and packed but not yet
   played; `EnemyView` shows a still.
2. **Card frames** — a rarity border and a type-coloured header, still drawn procedurally.
3. **DOTween** for card motion, damage shake, and intent changes. This is the single
   largest perceived-quality jump available and it costs no art at all.
