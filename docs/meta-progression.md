# Meta progression: Embers, unlocks and difficulty

Every finished run earns **Embers**, which are kept across runs. The lifetime total opens an unlock
track of new cards and relics. Winning opens difficulty levels, one at a time.

## Embers

| For | Embers |
|---|---|
| Each floor reached (both acts, bosses included, 20 in all) | 1 |
| Each elite won | 3 |
| Each boss beaten | 10 |
| Winning the run | 10 more |

A first run that dies on floor 6 earns about 8. A loss at the Tyrant earns about 15, and a win about 60.
Getting further is rewarded, not only winning: a new player's first unlock comes within a run or two,
not after a win that is hours away.

Embers are a lifetime total and are never spent, so an unlock can never be lost or regretted. Only a
run that reaches its end-of-run screen counts. A run abandoned from the main menu earns nothing, since
there is no floor to count from a run that did not end.

## The unlock track

| Embers | Unlock | Adds |
|---|---|---|
| 10 | Kiln cards | **Crucible** (1: 7 Block, 3 Heat) · **Brand** (1: 6 damage, 1 Vulnerable) · **Kiln Guard** (2, Power: whenever you play an Attack, gain 2 Block) |
| 25 | Obsidian Shard | Relic: at the start of each combat, apply 1 Weak to ALL enemies |
| 45 | Tempest cards | **Ember Scatter** (1: 3 damage to ALL) · **Blade Dance** (2: 3 damage 4 times) · **Momentum** (0: gain 1 Energy, Exhaust) |
| 70 | Forge Apron | Relic: at the end of your turn, gain 2 Block |
| 100 | Inferno cards | **Magma Heart** (1, Power: whenever you apply Burn, gain 1 Block) · **Supernova** (2: spend all Heat, deal that much damage to ALL, Exhaust) · **Phoenix Plume** (1: 9 Block, heal 3, Exhaust) |
| 140 | Anvil Crown | Relic: whenever you gain Heat, gain 1 Block |

- **Unlocked cards** join the reward pool: fight rewards, treasure, shops and events. They get upgrades
  by the same rule as every other card.
- **Unlocked relics** join the relic pool: elites, bosses and shops.
- **Built from existing effects.** Nothing on the track needed engine code, and it is kept out of the
  base pools in `ContentGenerator`, so the base game is exactly the game that was balanced.
- **A run's pools are fixed when it starts** (`RunState.Unlocked`, saved with the run). An unlock earned
  elsewhere never changes a run already in progress.

## Difficulty

After the first win, the main menu shows a difficulty control. Winning at the highest level reached
opens the next, up to 5. Each level keeps every rule below it (`DifficultyRules`).

| Level | Adds |
|---|---|
| 1 | Elites have 10% more HP |
| 2 | Start with 5 less max HP |
| 3 | Rest sites heal 20% of max HP instead of 30% |
| 4 | Hallway enemies have 10% more HP |
| 5 | Bosses have 10% more HP |

Every rule is a number the player can read before choosing. None changes how an enemy behaves, so a
higher level is the same game with less room for mistakes, not a different one.

## Balance

`./tools/simulate.sh` plays five passes (5 bot policies × 300 runs each). The two passes that split the
track show whether a change in the full pass comes from cards or relics.

The first track doubled run wins: 11–18% against 5–7% for the base game. That broke the promise
that unlocks add choices rather than power. Supernova (1.5× Heat to every enemy for 2 Energy),
Blade Dance at 3×5, Momentum's free Energy and draw, a Vulnerable-on-everything relic, and a
Strength-and-Dexterity crown were all toned down or replaced. The shipped track:

| Pass | Run win % | Beat the Act 1 boss |
|---|---|---|
| Base game | 5.0–7.3 | 26–30% |
| Unlocked cards only | 7.3–10.3 | 29–35% |
| Unlocked relics only | 4.7–7.3 | 28–31% |
| **Everything unlocked** | **5.3–8.3** | **29–35%** |
| Everything unlocked, difficulty 5 | 1.0–3.0 | 11–13% |

The full track adds one or two points: about the noise of 300 runs, and small next to what a player
learns in the runs it takes to earn it. Difficulty 5 cuts wins by about three quarters. It is meant
for players who already win, not for the bot, which plays like a first-time player on purpose.

## Files

- `Run/Profile.cs`: `profile.json`, separate from the run save, because the run save is deleted
  whenever a run ends and the profile must survive that. An unreadable profile is kept as
  `profile.bad.json` rather than overwritten.
- `Run/UnlockService.cs`: the Embers formula, the track, and what one run crossed.
- `Run/DifficultyRules.cs`: the five rules and where they apply.
- `Content/UnlockData.cs` and `RunConfig.Unlocks`: the track as content. `RewardPoolFor` and
  `RelicPoolFor` add unlocked content to the base pools.
- `View/MainMenuView.cs`: the progress panel (Embers, a bar to the next unlock, the track with a tooltip
  per unlock) and the difficulty control.
- `View/EndOfRunView.cs`: "+26 Embers (51 total) · Unlocked: Tempest cards".
- The save format is version 8 (`difficulty`, `unlocked`). Older saves continue at normal difficulty with
  base pools.
- The capture harness calls `Profile.UseMemoryOnly(embers: 30, maxDifficulty: 2)`, so a capture run never
  reads or writes the real profile.
