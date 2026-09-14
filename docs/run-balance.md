# Run balance

How the full-run numbers were reached, and what they still get wrong. Measured with
**EmberDeck → Run Full-Run Simulation**: 300 runs per map policy, each run playing map choices,
fights, rests, treasure, elites and the boss with the same combat policy as the single-fight
simulator.

Three map policies are compared because "does taking elites pay off" is a comparison — one
policy's win rate cannot answer it.

| policy | takes elites | rests |
|---|---|---|
| Cautious | never, unless forced | below 60% HP |
| Balanced | above 75% HP | below 45% HP |
| Greedy | above 50% HP | below 35% HP |
| Hunter | routes toward elites, enters every one | below 35% HP |
| Avoider | routes away from elites | below 45% HP |

## Where it landed

| policy | run win | reach boss | win if reached | boss HP left at death |
|---|---|---|---|---|
| Cautious | 22.0% | 42.3% | 52.0% | 26% |
| Balanced | 21.0% | 40.3% | 52.1% | 26% |
| Greedy | 20.0% | 39.3% | 50.8% | 24% |

A won hallway costs 12 HP (19% of max), a won elite 20 HP (32%). Decks reach the boss at a
median 17 cards and about 40 HP.

The combat and map bots are deliberately mediocre — no lookahead, no combos, "take the rarest
card". Around one run in five won by that player is a reasonable place for the difficulty: hard,
winnable, and leaving room for a person who plays with intent.

## How it got there

| pass | change | reach boss | run win |
|---|---|---|---|
| 0 | fights tuned to 80-90% single-fight win rate | **0%** | 0% |
| 1 | hallway 3 enemies → 2; scaling 18% → 8% per fight | 25% | 0% |
| 2 | boss exempt from scaling, retuned; elite 2 strong enemies | 31% | 2% |
| 3 | boss cut ~40%; elite partner Rat → Emberling | 38% | 15% |
| 4 | elites also grant a relic (pool of seven) | 40% | 21% |

## What each pass taught

**Pass 0 — the single-fight simulator was measuring the wrong thing.** It reported 83% wins and
was right. But the starter deck won that fight finishing at 16 of 63 HP, and health carries
between fights, so the second fight killed 65% of runs and nothing reached the boss. A hallway
fight has to be tuned for *health cost*, not win rate. The single-fight report now says so.

**Pass 1 — scaling compounds.** 18% per fight looked small and put row-eight enemies at 2.4x
their base health.

**Pass 2 — the boss was being scaled twice.** Its numbers were authored as the end of the run,
and the per-fight multiplier was applied on top, putting a 150 HP boss near 250 by the time
anyone reached it. Every simulated run that arrived there died. The boss is now exempt.

**Pass 3 — measure how far off, not just whether.** The boss was still winning ~95% of the time
against decks that reached it. Recording *the boss's remaining health when the player died*
showed ~42% — which called for a ~40% cut rather than another trim. After the cut it reads 24%
and players win about four times in ten.

**A negative result worth keeping: bigger elite rewards did nothing.** Elites paying two cards
instead of one moved greedy boss-reach from 27.7% to 27.3%. A won elite then cost 46% of max
health, and no card offsets that. The lever was the elite's health cost, not the size of its
reward. The knob remains in `RunSimulator` (off by default) so the result can be re-checked.

**Pass 4 — relics raised every policy, not just the greedy one.** Elites now grant a relic
from a pool of seven, each built from an existing effect or Power. `EmberDeck → Audit Relics`
starts one combat per relic and confirms each changes what it claims — none is inert. On the
same seeds, run wins rose 4-6 points for all three policies. The gap between cautious and
greedy play narrowed from 2.7 to 2.0 points but did not reverse.

**Elites, answered — by making the policies actually differ.** Cautious and Greedy turned out
to take almost the same number of elites (0.49 vs 0.66 per run): both decide one step at a time,
and an elite is rarely on offer when health allows it. Hunter and Avoider plan their route
instead, and differ by 4.6x (1.16 vs 0.25 elites per run):

| policy | run win | reach boss | win if reached | relics per run |
|---|---|---|---|---|
| Hunter | 17.0% | 28.7% | **59.3%** | 0.80 |
| Avoider | **22.3%** | **44.3%** | 50.4% | 0.16 |

Elites are a real trade, and the trade currently loses. Hunting them builds a deck that beats the
boss 59% of the time instead of 50% — the payoff exists. But elites kill the run before it gets
there: boss-reach falls from 44% to 29%, a gap of about four standard errors. Net, avoiding elites
wins about five points more runs.

So the reward is not the problem any more; the risk of dying *inside* the elite is. That points
at elite lethality — or at what follows an elite on the map — rather than at bigger rewards.

## A tooling failure that cost two passes

Content regeneration crashed Unity natively twice during this work. The batch log contained no
`error CS` line, so a check for compile errors reported success, and three simulations ran
against content the edits had never reached. Their numbers — including a "deck at boss: 12"
that contradicted everything else — were artefacts of a crashed generator, not of the game.

`tools/regenerate.sh` now gates on the generator's own success line and refuses to run beside
another Unity process. The general rule: **check that the thing ran, not that nothing
complained.**

## Still wrong

- **Elites are a losing bet.** They pay off at the boss (59% vs 50%) but cost more runs on the
  way than they save there. Whether that is wrong is a design decision: elites could be a *good*
  bet for a healthy deck, or a deliberate boss-preparation gamble. If the former, reduce how often
  elites kill rather than raising their reward.
- **Mid-run attrition is the main killer.** Most deaths are hallway fights on rows 5-7, where
  8%-per-fight scaling has accumulated and the bot has rested less than once per run.
- **The reward bot is weak.** "Take the rarest card" builds seventeen-card decks with no
  archetype coherence. A smarter pick policy would change every number above, which is why it
  was left crude: a clever bot hides difficulty a real first-time player would feel.
