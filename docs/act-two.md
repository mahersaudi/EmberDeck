# Act 2: the Obsidian Deep

A run is now two acts. Beating the Forge Tyrant no longer ends it. It opens a second map below the
forge, with its own enemies, backgrounds and boss: the Cinder Wyrm.

## How a run crosses between acts

- **The Tyrant pays out** a relic, 55–70 gold and a card reward at elite odds. Bosses never drop a
  potion.
  Picking or skipping that card starts Act 2.
- **Full heal.** The act starts at full HP. The first boss fight is hard, and arriving in Act 2 at
  8 HP would decide the second act before it begins.
- **A new map** from the same generator. Its seed comes from the run seed and the act number, so a
  save only needs the act to rebuild it.
- **Scaling restarts.** Enemy HP growth counts fights within the act (`RunState.ActStartFight`), and
  Act 2 grows at 5% per fight against Act 1's 8% (`RunConfig.ActScalingPerFight`). Act 2's enemies
  are authored at their own strength instead of arriving multiplied by every fight of Act 1.
- **Only the last boss wins the run** (`RunState.IsFinalBoss`). Floors count through both acts, so
  the end-of-run screen reads "floor 14 of 20".

Act 1 is unchanged. Its scaling, encounter order, shops and events come out exactly as before: every
new seed term is zero in Act 1, and the simulator's Act 1 boss rate is identical to the single-act
numbers (26–30%).

Shops and events used to be seeded only by map position. Act 2 reuses the same rows, so without the
act term it would have offered the same shop stock again.

## The enemies

Each asks one of Act 1's questions again, of a deck that has had a whole act to grow. They are built
only from effects that already existed; no engine code was added.

| Enemy | HP | Moves | The question |
|---|---|---|---|
| Ember Wisp | 9–11 | Flicker 3 + 1 Burn · Flare 6 | Area damage, while Burn stacks on you |
| Magma Leech | 22–24 | Siphon 6 and heals 3 · Latch 4 + 1 Weak | A race: it heals as it bites |
| Obsidian Sentinel | 40–43 | Fortify 12 Block · Halberd 9 · Halberd 9 | Block big enough that only burst or Burn gets through |
| Ashen Knight | 36–39 | Sunder 6 + 1 Vulnerable · Cleave 9 · Steel 8 Block, +1 Strength | Vulnerable, then the hit that punishes it |
| Cinder Shaman | 22–24 | Hex 1 Weak, 1 Vulnerable · Firebolt 6 + 2 Burn · Ritual +1 Strength | Debuffs, and stronger every cycle it is left alone |
| Obsidian Colossus (elite) | 60–64 | Shatter 3×3 · Quake 11 · Ward 14 Block, +1 Strength | The Sentinel's question at elite scale |
| **Cinder Wyrm (boss)** | 120–130 | Roar 1 Weak, 1 Vulnerable · Inferno Breath 3×4, 1 Burn per hit · Tail Sweep 14 · Coil 16 Block, +1 Strength | Everything above at once |

> These numbers are about a quarter below the ones this act was first tuned at. They were authored
> for a player with 63 HP who drafted their deck from card rewards; a run now starts from thirty
> chosen cards, and the player has 50 HP. Measured in isolation at the old numbers, one simulated
> player in ten cleared this act. See `docs/act-three.md`, "Balance".

| Pool | Encounters |
|---|---|
| Early (rows 0–2) | Wisp trio · Leech pair · Obsidian Sentinel · Shaman and Wisp |
| Late | Knight and Wisp · Shaman and Leech · Sentinel and Shaman · Wisps and Leech |
| Elite | Obsidian Colossus · Knight and Wisps · Shaman, Leech and Wisp |
| Boss | Cinder Wyrm |

## Balance

The first draft of Act 2 was far too hard. It took four passes, all measured with
`./tools/simulate.sh` (5 bot policies × 300 runs, potions on):

| Pass | Run win % | Reached the Wyrm | Wyrm deaths | What changed |
|---|---|---|---|---|
| Draft | 0.0 | 0–1% | 100% | — |
| 1 | 0.3–1.3 | 6–9% | — | About −25% HP and −25% damage across the act; four Wisps became two Wisps and a Leech |
| 2 | 0.7–3.7 | 8–12% | 77% | Late enemies lighter; two Knights (who Cleave on the same turn) became a Knight and two Wisps |
| 3 | 5.0–6.7 | 11–15% | 60% | Shaman's Ritual +2 → +1; Colossus and Wyrm lighter |
| **4 (shipped)** | **5.0–7.3** | **13–17%** | **58.5%** | Act 2 scaling 8% → 5%; two Leeches beside the Shaman became a Leech and a Wisp |

Where the final numbers sit, next to Act 1:

| | Act 1 | Act 2 |
|---|---|---|
| Hallway deaths, early rows | 0–0.1% | 0–2% |
| Hallway deaths, late rows | 10–16% | 14–23% |
| Elite deaths | 11–20% | 20–23% |
| Boss deaths | 54% (Tyrant) | 58.5% (Wyrm) |

About half of the runs that beat the Tyrant reach the Wyrm, and 35–56% of those win. The bot plays
like a first-time player on purpose, so a whole-run win rate of 5–7% is its number, not a person's.
What the table shows is that Act 2 is a step up from Act 1 of about the same size at every tier.

## Art

- **Seven portraits** in `art/generate_art.py` (`ACT2_ENEMIES`) with a `deep` palette: black obsidian,
  violet crystal light and orange accents, so the act reads as somewhere deeper than the forge.
- **Four backgrounds** in `art/generate_backgrounds.py`: `bg_hallway_2`, `bg_elite_2`, `bg_boss_2`
  and `bg_map_2`. A later act without its own painting borrows Act 1's.
- The Ember Wisp took three renders. "A faint ghostly face" painted a woman with flaming hair; "a floating
  orb of flame" painted a candle in a glass ball. The shipped prompt asks for a living flame creature
  with eyes, names orbs and candles as what it is not, and the deep palette's negatives exclude human
  faces.
- `install_painted.sh` now skips backgrounds and app icons, which share the render folder and were being
  copied in as card art. Its last line also exited 1 whenever the card count was correct.

## In the interface

- The map title names the act ("ACT 2 · THE OBSIDIAN DEEP"). The boss node's tooltip names that act's
  boss, and says whether beating it ends the run or goes deeper.
- A first-run tip on the Act 2 map explains the full heal and the stronger enemies.
- The run line, main menu Continue detail and end-of-run floor count all show the act.
- The save format is version 7 (`act`, `actStartFight`). Older saves load into Act 1.
