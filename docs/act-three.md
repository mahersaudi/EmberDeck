# Act 3: the Emberheart

A run is now three acts. Beating the Cinder Wyrm opens the chamber the forge was built over: the
source of the fire, and the run's last act.

The act structure is the one Act 2 established (`docs/act-two.md`) — its own map from the same
generator, a full heal on arrival, scaling counted from the act's first fight, and only the last
boss ending the run. Nothing in the flow was added for a third act; `RunConfig.Acts` is 3 and the
rest follows, including the floor count ("floor 24 of 30") and the map's act name.

## The act's question

Acts 1 and 2 ask about a turn: can you block this, can you kill that before it hits. Act 3 asks
about the fight: **every enemy here grows, heals, or hits through Block**, so a deck that won two
acts by holding still runs out of time.

| Enemy | HP | Moves | The question |
|---|---|---|---|
| Emberfly | 12–14 | Sting 3×3 · Mote 4 + 1 Weak | Many small hits, always in numbers: Block spent on chip damage |
| Cinder Revenant | 25–28 | Kindle +2 Strength · Rend 8 · Rend 8 | Kill it in the first two turns or fight the version it becomes |
| Molten Maw | 30–33 | Gape 8 Block · Swallow 14 | One enormous hit, announced a turn ahead |
| Ash Priest | 27–30 | Censer 5 + 2 Burn · Absolution heals 8, 8 Block · Doom 10 | Burst it down, or watch the fight reset every third turn |
| Slag Titan | 50–55 | Harden 15 Block, +1 Strength · Stomp 12 · Crush 15 | The Sentinel grown up: Block, and a blow that punishes waiting |
| Living Flame (elite) | 66–72 | Flare Up 4×3, 1 Burn per hit · Consume 13, heals 6 · Gutter 12 Block, +1 Strength | Burn through Block while it eats |
| **The Emberheart (boss)** | 150–160 | Pulse Weak + Vulnerable · Eruption 4×4, 1 Burn per hit · Cataclysm 21 · Reforge 18 Block, heals 8, +1 Strength | All three of the act's things at once, on a six-turn pattern |

The boss's pattern is six turns long and repeats, so it can be learned and planned two turns ahead.
A fight that size cannot be won by reacting.

| Pool | Encounters |
|---|---|
| Early (rows 0–2) | Three Emberflies · Two Revenants · Maw and Emberfly · Priest and Emberfly |
| Late | Slag Titan · Priest and Revenant · Maw and Priest · Revenant and two Emberflies |
| Elite | Living Flame · Titan and Priest · Two Maws and an Emberfly |
| Boss | The Emberheart |

Every move is built from effects that already existed. No engine code was added for this act.

## Balance

Measuring a last act is the hard part: about one simulated run in three clears Act 1 and one in ten
reaches the Emberheart, so a pass that starts at floor 1 measures Act 3 with a dozen runs. The
simulator therefore has two extra passes that **start a run at the top of an act** — full health, the
act's own scaling counted from there, and a deck, relic shelf, upgrades and potions matching what the
runs that do arrive actually carry (`RunSimulator.StartAtAct`). It is an emulation and says so; what
it measures is one act's own difficulty, with the previous act's verdict on the player's health
removed.

With the suggested deck, 300 runs per policy:

| pass | clears the act | reaches the Emberheart | beats it |
|---|---|---|---|
| whole run from floor 1 | 31–36% (Act 1) | 9–12% | **3.7–6.0%** |
| Act 2 alone | 52–54% | 21–28% | 8–12% |
| Act 3 alone | — | 25–40% | 6–12% |

Fight by fight in Act 3: the early rows kill 0–1.4%, the late rows 10–19%, the elites 19–31%, and the
Emberheart 52% of the runs that reach it — costing 25 HP to the ones that win.

**Acts 2 and 3 were both cut by about a quarter to get there.** Act 2's numbers were authored when
the player had 63 HP and drafted their deck; at 50 HP against a built deck, its hits three-shot the
player, and the first measurement of Act 2 in isolation had 10% of players clearing it. Forty-five
numbers came down — every Act 2 and Act 3 damage value by 20–30%, every HP range by about 10%, and
the two bosses by 15% — which is what the table above is measured at. The alternative was one global
multiplier, but an enemy's announced number has to stay exactly what lands, and only the authored
values keep that promise.

## What is left

- **Act 3's early rows kill nobody** (0–1.4%), like Act 2's did before its pass. Arriving at full
  health with a deck that has won twenty fights makes the first three rows a formality; they need a
  pass of their own.
- **No new mechanic.** Act 3 recombines Strength, Burn, Block and healing. A third act is the natural
  place for one new rule, and it has none.
- **The act has no relic of its own** and no cards that belong to it: its rewards are the same pools
  as Act 2's.
