# EmberDeck

A roguelike deckbuilder in Unity 6 (URP 2D). This repository is the **vertical slice**: one
complete fight, playable end to end, built on the architecture the full game will use.

## Run it

1. Open the project in Unity `6000.5.1f1`.
2. Menu: **EmberDeck → Generate Content and Scene**. This writes every card, enemy, relic and
   the playable scene.
3. Open `Assets/EmberDeck/Scenes/Combat.unity` and press Play.

Balance pass: **EmberDeck → Run Balance Simulation (2000 fights)** prints a win-rate report to
the Console. It needs no scene and no Play mode.

## How to play

Click a card to select it, then click an enemy to aim it. Cards that need no target play on the
first click. `End Turn` resolves the enemy turn. Enemies announce what they will do next — the
number above an enemy is its actual damage after every modifier, not a base value.

## Architecture

The one rule: **the rules are plain C#, the view only reads them.**

```
Core/        DeterministicRng, RunRng, EventBus     — no game concepts
Combat/      Actor, Enemy, CombatState, CombatEngine — every rule, zero MonoBehaviours
Content/     CardData, CardEffect, EnemyData, relics — ScriptableObject definitions
View/        CombatView, CardView, EnemyView         — reads the model, writes only via the engine
Editor/      ContentGenerator, BalanceSimulator      — tooling, stripped from builds
```

Four decisions carry the whole design:

**Effects are composable assets, not an enum.** A card owns a *list* of `CardEffect`
ScriptableObjects. "Deal 5 damage and apply 2 Vulnerable and draw a card" needs no new code.
The alternative — an enum plus a switch in the play routine — works for thirty cards and then
every new card edits shared code, and combinations nobody anticipated become impossible.

**Enemy moves reuse `CardEffect`.** Anything a card can do, an enemy can do. Every mechanic is
written once.

**Relics hook an event bus.** Event payloads are classes, so a handler *mutates* them —
`DamageCalculation.Amount += 3` is the entire Ember Core relic. `CombatEngine` does not know
relics exist. This is what lets relic count grow without combat code growing.

**Randomness is seeded and split by purpose.** `UnityEngine.Random` is one global stream shared
with VFX and UI; a single extra call desynchronises a run. `RunRng` gives shuffles, enemies,
rewards and map their own streams from one master seed, so a run replays exactly — which is what
makes shareable seeds and reproducible bug reports possible at all.

Because no rule touches `UnityEngine`, `BalanceSimulator` plays thousands of fights headlessly.
That is the practical payoff: balance becomes measurable instead of a matter of impression.

## Deliberate shortcuts, and what replaces them

| Shortcut | Why now | Replace with |
|---|---|---|
| UI generated in code | Prefabs and scenes are YAML: unreviewable in a diff, and they merge badly while layout changes hourly | Authored prefabs once the layout settles |
| Legacy `Text`, not TextMeshPro | TMP needs its Essential Resources imported through a dialog before any text renders — that breaks a fresh clone and headless runs | TMP, after importing TMP Essentials once |
| No animation or audio | The slice answers "is one fight interesting?", which juice cannot fix if the answer is no | DOTween after the fight itself reads well |
| No `.asmdef` files | Everything compiles into Assembly-CSharp with no configuration to get wrong | Split runtime/editor assemblies when compile times bite |
| Content generated, not committed | Asset YAML carries hand-assigned GUIDs; a wrong one is a silent null at runtime | Hand-authoring, once content stops being regenerated wholesale |

## Next milestone

The slice ends at a single fight. In order:

1. **Tune this fight to an 80-90% bot win rate.** Anything higher has no decisions in it.
2. **Card rewards** after victory — pick 1 of 3. The first real run-level decision.
3. **A map of encounters** with `RunRng.Map`, and `RunState` saved as JSON between fights.
4. **More relics**, to prove the hook system carries weight beyond the one that exists.
5. **Upgrades**, using `CardInstance` — the reason cards have instances rather than being shared.

Content and balance are the work after that; the architecture above is meant not to change.
