# First-run tips

EmberDeck teaches itself with short tips instead of a tutorial fight. Each tip appears the first time
the thing it explains matters, points at that thing with a pulsing ring, and never comes back.

## Why tips and not a tutorial fight

- A scripted fight teaches a deck the player will never hold. Players who already know the genre have
  to sit through it or hunt for the skip.
- A tip that appears when Heat first rises explains Heat to the player who needs it, in their own run.
  A veteran spends one click on it.
- A tip also counts as done when the player does what it describes. Someone who plays a card before
  reading "click a card" is not told to do it anyway.

## The tips

| Id | When it appears | Points at | Done early when |
|---|---|---|---|
| `map` | The map is shown | The nodes you can enter now | You choose a node |
| `hand` | A fight starts | The hand | You play a card |
| `intent` | A fight starts, after `hand` | Each living enemy's intent | You dismiss it |
| `potion` | A fight, with a potion in the belt | The potion slots | You drink a potion |
| `heat` | Heat first rises above 0 | The Heat panel | You dismiss it |
| `endturn` | Nothing in hand is playable, after at least one card was played this fight | End Turn | You end a turn |
| `reward` | A card reward or treasure | The offered cards | You pick or skip |

Rest sites, shops and events explain themselves on screen, so they have no tip.

## How it works

`View/Coach.cs` owns everything:

- **One tip at a time.** Tips queue in the order they were raised. A fight queues `hand` and `intent`
  before anything else, so a potion tip raised while the opening hand is dealt still comes third.
- **Its own canvas.** Sorted at 400: above the game, below the tooltip (500). A tooltip opened while a
  tip is up still draws on top.
- **Hidden while menus are up.** `Coach.Suspended` is set by `CombatView`. While the main menu, pause,
  settings or end-of-run screen is open, the tip hides without being dismissed.
- **Leaving a screen.** `Coach.EndScreen()` counts the tip on screen as read and drops the queue. Queued
  tips were never seen, so they can still appear the next time their moment comes.
- **Targets are read every frame.** The ring follows cards as they deal in and move. If nothing is
  there to point at, the tip is centred with no ring.
- **Saved per player.** Seen tips and the on/off switch are stored in PlayerPrefs (`emberdeck.tutorial.*`),
  like Settings. Abandoning a run does not replay them.

**Skip tutorial** turns tips off. **Settings → Gameplay → Tutorial tips** turns them back on, and
turning them on starts every tip over.

## Capture harness

`AutoCapture` calls `Coach.UseMemoryOnly()`: every tip starts unseen, and nothing is written to
PlayerPrefs, so a capture run neither depends on nor changes what the person at the machine has seen.
It photographs each tip (`01-map`, `02-combat-start`, `02t-intent-tip`, `02c-potions`, `03d-tip-<id>`
for the late tips, `04-rewards`), logging which tip is up, and dismisses each one through its **Got it** button.

The harness presses End Turn early, which completes the end-turn tip before it can appear, so it
calls the development-only `Coach.Forget("endturn")` before raising the late tips.
