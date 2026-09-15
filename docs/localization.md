# Arabic

The whole game can be played in Arabic: every screen, card, enemy, event and tip, right to left, in an
Arabic font. The language is chosen in **Settings → Gameplay → Language**. It is saved per machine,
like the other settings, and applies when you leave the Settings screen.

## Why it needed its own text pipeline

The interface uses Unity's legacy `Text`, which does no text shaping at all. It draws code points one
after another, left to right. Arabic written that way comes out wrong three times over:

1. Letters appear in their isolated form instead of joining.
2. Words read backwards.
3. Wrapping happens on the reversed string, so the last words of a sentence land on its first line.

TextMeshPro has the same limits without an RTL plug-in. So the game does the work itself before
`Text` ever sees the string.

## How a label becomes Arabic

Every label in the game is created by `UiFactory.Label`, which adds a `UiText`: a `Text` subclass that
does three things when text is assigned.

1. **Translate** (`View/Loc.cs`). The English string is the key.
   - The table lives in `Resources/Localization/ar.txt`: an English line, then its Arabic on the next
     line after `= `.
   - A key with `{0}`, `{1}`… is a template. "Deal {0} damage." matches "Deal 6 damage.", and the 6 goes
     into the Arabic. Words a placeholder captured are translated in turn: "Apply 2 Burn to the target."
     fills in "حرق" as well.
   - A template only counts if every placeholder translates. Otherwise it was the wrong template:
     "Heat {0}" must not claim the card "Heat Shield+".
   - A string with no key is tried one sentence at a time. That is how card descriptions translate,
     since they are built from each effect's sentence.
   - Rich-text tags are kept, and the text between them is translated piece by piece.
2. **Shape** (`View/ArabicText.cs`). Every letter is replaced with its Presentation Forms-B code point
   in the form its neighbours call for (isolated, final, initial or medial), with lam-alef as a single
   ligature. Harakat are dropped, because `Text` cannot place a mark over a letter.
3. **Lay out**.
   - Lines are broken at spaces to fit the label's width, measured on the shaped words, 6% short of the
     full width so `Text` never re-wraps.
   - Each line is then put in visual order. Runs of Latin letters and numbers keep their own order;
     everything else reads right to left.
   - Neutrals between two numbers take the paragraph's direction unless they are one separator inside a
     number ("46/63").
   - Tags are lifted off the characters before reordering and put back after, so a coloured keyword
     stays coloured.

Reading `text` back returns the English that was assigned, so code that rebuilds a label keeps working.
`Text` draws from the laid-out string: `UiText` returns it only while the mesh is being built. Returning
the English there was the first bug found in the Arabic captures, where every label not translated
before assignment drew in English.

## The mirrored layout

In Arabic the whole screen is mirrored: the run line sits at the top right, the hand deals from the
right, health bars fill leftwards, and table labels sit on the right of their values.

- **One mirror for everything.** `CombatView` hangs every screen off a **Stage** under the canvas, and
  gives the Stage a horizontal scale of −1. The canvas's own scale belongs to its CanvasScaler.
- **Text still reads forwards.** `UiText` mirrors its glyph quads back inside its own rectangle, and its
  alignment is flipped (`UiFactory.Mirror`), so a label that was flush left is flush right.
- **Things placed by world position** needed no change: tooltips, tips, the focus ring, and floating
  numbers. The tooltip now takes its target's left and right edges from the minimum and maximum corners,
  because under the mirror the "top-right" corner is on the left.
- **Controller input.** Left and right on a slider are reversed under the mirror, so they move the
  handle the way it looks. Focus moves by screen position, so arrows need nothing special.
- **Separate canvases** (tooltip, tips, controls bar) are not mirrored. Their rows reverse their order
  and flush right instead.

## Fonts

| Font | For | License |
|---|---|---|
| Noto Naskh Arabic UI (Regular) | Arabic | SIL Open Font License 1.1 |
| DejaVu Sans | Latin and symbols in Arabic mode: key names, "×", seeds | Bitstream Vera license (free, redistributable) |

Their license texts ship in `StreamingAssets/Licenses`.

**Why Noto Naskh Arabic UI.** The Arabic font must map the Presentation Forms-B code points, because
that is what the shaper draws. Many modern Arabic fonts shape through OpenType instead and do not map
them. The Cairo and Tajawal fonts on this machine lack 36 of the 125 forms, including most isolated and
final ones. A small cmap check (kept in the session notes, not the repo) confirmed that Noto Naskh Arabic
UI, Amiri and DejaVu Sans map all 125. Noto Naskh Arabic UI was chosen because it is designed for
interface line heights.

**The Latin fallback.** Noto Naskh Arabic UI has almost no Latin, so `ProjectSetup.ApplyFonts` sets
DejaVu Sans as its fallback font before every build. Setting `TrueTypeFontImporter.fontReferences` alone
did not reach the `.meta` in a batch build. The serialized field is written too, and the build log
confirms "Arabic font fallback set to DejaVu Sans".

## Changing language

The interface is built once, in one language. Leaving Settings with a different language reloads the
scene. The tip canvas, which survives scene loads, is dropped and rebuilt (`Coach.Rebuild`). During a
run the language row is disabled, with a note to change it from the main menu, because rebuilding would
lose the fight in progress. The save and profile are untouched either way.

## Writing translations

- **No harakat.** They are stripped; write "طبق", not "طبّق".
- **Keywords as standalone words** ("اكسب 5 درع"), so they are found and coloured. A keyword with a
  prefix attached ("الحرق", "درعك") is plain text. That is fine where grammar wants it, but it loses
  the colour.
- **Western digits.** The numbers come from the game.
- **The mirrored layout moves things.** A tip that points "bottom left" in English points "أسفل
  اليمين" in Arabic.

## Finding what is untranslated

`Loc.Missing` collects every English string shown in Arabic mode without a translation. Run the capture
harness in Arabic:

```bash
Build/macOS/EmberDeck.app/Contents/MacOS/EmberDeck -emberdeck-capture OUT -emberdeck-lang ar -logFile OUT/player.log
```

It photographs every screen in Arabic. Before quitting, it also passes every card, relic, potion,
enemy, move, event, keyword, unlock and difficulty rule through translation
(`CombatView.DebugSweepTranslations`). It logs `[AutoCapture] untranslated: N` and one
`[Untranslated]` line per string. The language is set in memory only, so a capture never changes the
machine's setting.
