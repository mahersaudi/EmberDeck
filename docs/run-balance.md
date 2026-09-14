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

## Where it landed

| policy | run win | reach boss | win if reached | boss HP left at death |
|---|---|---|---|---|
| Cautious | 17.7% | 40.0% | 44.2% | 24% |
| Balanced | 15.3% | 37.7% | 40.7% | 24% |
| Greedy | 15.0% | 36.0% | 41.7% | 24% |

A won hallway costs 12 HP (19% of max), a won elite 20 HP (32%). Decks reach the boss at a
median 17 cards and about 40 HP.

The combat and map bots are deliberately mediocre — no lookahead, no combos, "take the rarest
card". Around one run in six won by that player is a reasonable place for the difficulty: hard,
winnable, and leaving room for a person who plays with intent.

## How it got there

| pass | change | reach boss | run win |
|---|---|---|---|
| 0 | fights tuned to 80-90% single-fight win rate | **0%** | 0% |
| 1 | hallway 3 enemies → 2; scaling 18% → 8% per fight | 25% | 0% |
| 2 | boss exempt from scaling, retuned; elite 2 strong enemies | 31% | 2% |
| 3 | boss cut ~40%; elite partner Rat → Emberling | 38% | 15% |

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

## A tooling failure that cost two passes

Content regeneration crashed Unity natively twice during this work. The batch log contained no
`error CS` line, so a check for compile errors reported success, and three simulations ran
against content the edits had never reached. Their numbers — including a "deck at boss: 12"
that contradicted everything else — were artefacts of a crashed generator, not of the game.

`tools/regenerate.sh` now gates on the generator's own success line and refuses to run beside
another Unity process. The general rule: **check that the thing ran, not that nothing
complained.**

## Still wrong

- **Elites are neutral, not rewarding.** Cautious play wins 17.7% against greedy 15.0%. With 300
  runs the standard error on these rates is about 2 points, so the gap is within noise — but the
  design intends elites to be a *good* bet for a healthy deck, and they are not yet. The natural
  fix is a different reward, most likely a relic, rather than more cards.
- **Mid-run attrition is the main killer.** Most deaths are hallway fights on rows 5-7, where
  8%-per-fight scaling has accumulated and the bot has rested less than once per run.
- **The reward bot is weak.** "Take the rarest card" builds seventeen-card decks with no
  archetype coherence. A smarter pick policy would change every number above, which is why it
  was left crude: a clever bot hides difficulty a real first-time player would feel.
