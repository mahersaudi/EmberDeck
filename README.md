# EmberDeck

A roguelike deckbuilder in Unity 6 (URP 2D). This repository is the **vertical slice**: one
complete fight, playable end to end, built on the architecture the full game will use.

## Run it

1. Open the project in Unity `6000.5.1f1`.
2. Menu: **EmberDeck → Generate Content and Scene**. This writes every card, enemy, relic and
   the playable scene.
3. Open `Assets/EmberDeck/Scenes/Combat.unity` and press Play.

Balance pass: **EmberDeck → Run Balance Simulation (2000 fights)** prints a win-rate report to
the Console. It needs no scene and no Play mode. The opening fight currently measures:

```
Win rate  : 86.7%   (1734/2000)
Turns     : median 8, range 4-15
HP left   : median 21 of 65, 10th pct 6
```

It took three passes to get there. The first build of this encounter measured **100%** — the
bot won 2000 of 2000 and finished with 60 HP of 72, meaning the fight contained no decision at
all. Ten hands played by hand would not have shown that.

## Build and ship

```bash
./tools/build.sh                 # release builds for macOS and Windows
./tools/build.sh BuildMac        # development build for macOS, with the capture harness
```

| method | output |
|---|---|
| `BuildMac` / `BuildWindows` | `Build/macOS/`, `Build/Windows/`: development builds, with the capture harness and debug hooks |
| `BuildMacRelease` / `BuildWindowsRelease` | `Build/Release/macOS/`, `Build/Release/Windows/` |
| `BuildAllRelease` | both release builds; the default |

- The version is `BuildScript.Version`. Only the generated scene is built.
- Windows builds need Unity's `windows-mono` module. Both platforms use Mono: IL2CPP for Windows can
  only be built on Windows.
- Release builds exclude the capture harness entirely (`AutoCapture.cs` is compiled only into
  development builds), because it drives debug hooks that do not exist in a release.
- Uploading to Steam, and everything that needs a Steamworks account: [`steam/README.md`](steam/README.md).
- `tools/build.sh` and `tools/regenerate.sh` share `tools/unity-guards.sh`, which refuses to run beside
  another Unity process and stops a compiler server from a different .NET SDK — the cause of a build
  failing with CS1504 "Method not found ... EncodingExtensions" on a file nobody changed.

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
| Legacy Input Manager, not the Input System package | Every Input System version available for this Editor fails to compile against it (deprecated APIs are errors in 6000.5) | The Input System package, once a version compiles. Gamepad play works on the legacy manager (`docs/controller.md`); rebinding and per-controller layouts still need the package |
| ~~No animation or audio~~ | Done: code tweens and synthesised audio | See `docs/audio-and-motion.md` |
| No `.asmdef` files | Everything compiles into Assembly-CSharp with no configuration to get wrong | Split runtime/editor assemblies when compile times bite |
| Content generated, not committed | Asset YAML carries hand-assigned GUIDs; a wrong one is a silent null at runtime | Hand-authoring, once content stops being regenerated wholesale |

## Next milestone

The slice ends at a single fight. In order:

1. ~~Tune this fight to an 80-90% bot win rate.~~ Done — 86.7%.
2. **Card rewards** after victory — pick 1 of 3. The first real run-level decision.
3. **A map of encounters** with `RunRng.Map`, and `RunState` saved as JSON between fights.
4. **More relics**, to prove the hook system carries weight beyond the one that exists.
5. ~~**Upgrades**~~ — done: the rest site offers heal *or* upgrade. See `docs/card-design.md`, "Upgrades".
6. ~~**First-run tutorial**~~ — done: tips that appear when what they explain first matters. See `docs/first-run-tips.md`.
7. ~~**Act 2**~~ — done: a second map, seven enemies and the Cinder Wyrm. See `docs/act-two.md`.
8. ~~**Meta unlocks**~~ — done: Embers, an unlock track of cards and relics, and five difficulty levels. See `docs/meta-progression.md`.
9. ~~**Controller support**~~ — done: keyboard and gamepad focus on every screen, with button hints. See `docs/controller.md`.
10. ~~**Arabic**~~ — done: the whole game in Arabic, right to left, chosen in Settings. See `docs/localization.md`.

Content and balance are the work after that; the architecture above is meant not to change.

## Project notes

**Packages are pinned deliberately.** The `com.unity.template.2d` template ships package
versions older than this Editor, and Unity 6000.5 turns several deprecated APIs into hard
errors — so the template's own manifest does not compile. `Packages/manifest.json` was trimmed
to what the game actually uses and pinned to versions that build. `manifest.json.bak` holds the
template's original list.

**Active Input Handling is set to the legacy Input Manager**, and `CombatView.EnsureEventSystem`
creates a `StandaloneInputModule` to match. These two must always agree: a mismatch produces a
game that runs perfectly and ignores every click, with no error anywhere.
