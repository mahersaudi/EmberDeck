# Keywords and icons

## The vocabulary

A player meeting "Vulnerable" for the first time cannot know it means +50% damage, and a
deckbuilder whose words need a wiki is one people bounce off. `View/Keywords.cs` holds every word
that means something specific. **Each definition was checked against `CombatEngine`**, not written
from memory of the design intent:

| word | definition | where the rule lives |
|---|---|---|
| Block | Absorbs damage before HP is lost. Removed at the start of its owner's next turn. | `DealDamage`, `BeginPlayerTurn`, `RunEnemyTurn` |
| Energy | Spent to play cards. Refills at the start of each turn. | `BeginPlayerTurn` |
| Heat | Built up by cards and kept between turns. Some cards spend it. Too much causes Overheat. | `GainHeat`, `SpendHeat` |
| Overheat | At the end of your turn, lose 1 HP per point of Heat above the threshold (10 unless raised). | `ResolveOverheat` |
| Burn | At the end of its turn, loses HP equal to its Burn, ignoring Block. Then Burn goes down by 1. | `TickEndOfTurn` |
| Strength | Each attack deals that much more damage. | `DealDamage` (flat, before multipliers) |
| Dexterity | Whenever Block is gained, gain that much more. | `GainBlock` |
| Vulnerable | Takes 50% more damage from attacks. Lasts that many turns. | `DealDamage`, `TickEndOfTurn` |
| Weak | Deals 25% less damage with attacks. Lasts that many turns. | `DealDamage`, `TickEndOfTurn` |
| Exhaust | Removed from your deck until the end of this combat. | `ExhaustCard` |
| Power | Played once. Its effect lasts for the rest of the combat. | `ActivatePower` |

Change a rule, change its sentence here.

**In card text** every keyword is coloured, so the words that have a definition look like they
do. Matching is whole-word, which is what keeps "Overheat" from lighting up "Heat" inside it.

## Tooltips

One panel (`View/Tooltip.cs`) on its own canvas above everything, so hand cards, reward cards and
the upgrade picker are all explained the same way. It has no raycaster: a tooltip that can catch
the pointer flickers, because covering the thing it explains fires that thing's pointer-exit.

| pointed at | shows |
|---|---|
| a card | every keyword on it, defined |
| an enemy | its intent spelled out with resolved numbers ("Deals 4 damage. Applies 3 Burn to you."), its Block, every status it has with its value, then any status the move is about to apply |
| the player panel | health, Block, every status |
| the Heat panel | current Heat, and either the threshold or exactly what overheating will cost this turn |
| the Energy orb | energy now and per turn |

Triggers take a function rather than a snapshot, because an enemy's tooltip has to describe the
intent and Burn it has *now*. **Never put a trigger inside another trigger:** pointer-enter
reaches every ancestor, so nested triggers fight over the one panel. Status chips deliberately
catch no pointer; the panel they sit on explains them all.

## Intents

An intent used to be a number, or a word for moves without one. Scald read "4", and the 3 Burn it
also applied was invisible until it had landed. Now it is **an icon for the kind of move, the
number, and a small icon for everything else the move does**: the Burn on Scald and Firebite,
the Vulnerable and Weak on Wail, the Strength and Block on the Tyrant's Forge. The number is the
engine's resolved value, after Strength, Weak and Vulnerable, and the tooltip uses the same one.

## Statuses

`StatusStrip` replaces "Burn 3   Strength 2   Vulnerable 1" with chips: icon and number, in
vocabulary order. A chip is recognised by shape and colour before it is read, and a value that goes
up punches, so a change is noticed.

## Icons

Sixteen icons, authored as SVG in `art/icons.py` and rasterised by Krita:

```bash
./art/render_icons.sh
```

An icon is read at 24 pixels out of the corner of an eye, so each is one bold silhouette in one
colour. **The dark outline is generated, not drawn:** every shape is copied behind itself in the
outline colour with a thick stroke, so all sixteen have exactly the same weight and each stays
legible over a bright portrait. The script refuses to slice an atlas without an alpha channel,
because an opaque icon would sit in a black square and the failure would only show in the game.

Icons load by name from `Resources/Icons`, which `ArtImportSettings` imports as sprites. A
missing icon renders as nothing rather than a white square; the number beside it still reads.
