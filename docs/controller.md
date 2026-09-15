# Keyboard and gamepad

Every screen can be played without a mouse. Focus sits on one button, card, enemy or map node at a
time. A ring marks it, and its tooltip opens as if it were hovered. A bar at the bottom of the screen
names the buttons.

## Controls

| | Gamepad | Keyboard |
|---|---|---|
| Move focus | Left stick or D-pad | Arrow keys or WASD |
| Select | A | Enter or Space |
| Back | B | Escape |
| End turn (in a fight) | Y | E |
| Pause menu | Start or Back/View | Escape (when there is nothing to back out of) |

- **Holding a direction repeats** after 0.38 seconds, then every 0.12 seconds.
- **On a slider**, left and right change its value by 5%.
- **In a fight:**
  - Selecting a card that needs a target moves focus to the enemies, so the next A plays it on the
    focused one.
  - B puts the card down and returns focus to it.
  - After a play, focus moves to the card that now sits where the played one was, or to End Turn when the
    hand is empty.
- **Back** presses the screen's own Back button when it has one: Settings → Back, Pause → Resume, the
  shop's removal picker → Cancel, the rest site's upgrade picker → Back. Everywhere else it opens or
  closes the pause menu, as Escape always has.
- **A first-run tip** on screen takes focus first. A dismisses it, and so does B.

## The mouse always wins

Moving the mouse or clicking hides the ring, closes the focus tooltip and clears focus, so a mouse
player never sees any of this. The first key or button press after using the mouse only shows where
focus is; it does not also act. A player picking up a pad therefore never presses something they
could not see.

## How it works

`View/PadNavigator.cs` replaces Unity's own UI navigation. `EventSystem.sendNavigationEvents` is off;
the StandaloneInputModule still handles the mouse. Unity's navigation had three problems:

1. **It could reach buttons behind the screen in front.** It navigates every Selectable in the scene,
   so focus wandered onto the hand behind the pause menu. Here focus stays inside the screen in front:
   `CombatView.NavScope`, narrowed to an open picker (`NavHint.Modal`) or a tip on that screen.
2. **Its neighbours went stale.** They are computed from layout, and the hand re-fans on every play.
   Here the next object is found by position on every press: the nearest in the pressed direction,
   with sideways distance counting 2.5× as much as distance ahead. Up from a card therefore reaches the
   enemy above it.
3. **Focus was lost when its object disappeared.** Unity drops focus when the focused card is played.
   Here focus moves to the nearest object left on the same screen, or to the screen's default when
   the screen changed.

`View/NavHint.cs` carries the per-object flags:

| Flag | Meaning | Used on |
|---|---|---|
| `Priority` | Where focus lands when a screen opens (ties go to the top-left) | Hand cards, Continue, End-of-run New Run, a tip's Got it |
| `Modal` | Only this panel can be navigated while it is open | Shop removal picker, rest upgrade picker |
| `Cancel` | The button Back presses | Settings Back, Pause Resume, picker Cancel/Back, Got it |
| `Skip` | Never focused | Cards in the end-of-run deck summary |

## Input mapping

This project uses the legacy input manager: every Input System package version available for
Unity 6000.5 fails to compile.

- **Axes.** `ProjectSetup.ApplyInput` adds four joystick axes before every build: `Pad Stick X`,
  `Pad Stick Y` (inverted so up is positive), and `Pad DPad X` and `Pad DPad Y` (the 6th and 7th axes).
  It is idempotent and writes through `SerializedObject`. A build without them still runs; the missing
  axis logs one warning and is ignored.
- **Buttons.** They are read as `KeyCode.JoystickButtonN`, laid out as XInput:

| Button | Windows, Steam Input, Steam Deck | macOS (also accepted) |
|---|---|---|
| A / B / X / Y | 0 / 1 / 2 / 3 | 16 / 17 / 18 / 19 |
| Back, Start | 6, 7 | 10, 9 |
| D-pad | 6th and 7th axes | buttons 5–8 |

**Windows and Steam are the target.** Steam Input presents any controller (Xbox, PlayStation, Switch
Pro, the Steam Deck's own controls) as an XInput pad to a game that does not use the Steamworks input
API, so the left column covers them all. The Steam Deck runs the Windows build through Proton.

**macOS is best effort.** Unity's legacy input on macOS reports controllers differently by driver, and
this layout has **not been tested with a physical controller** on any platform. The capture harness
drives the navigator directly (`SimulateMove`, `SimulateSubmit`, `SimulateCancel`), so the focus logic
and every screen are verified, but the hardware mapping is not.

## Capture harness

`AutoCapture` sets `PadNavigator.IgnoreHardware`, so a mouse moved during a capture cannot switch focus
off mid-shot. It photographs:
- `12-pad-menu`: focus moved from Continue to New Run.
- `12a-pad-hand`, `12b-pad-next-card`: focus in the hand, then moved along it.
- `12c-pad-target`: a card aimed, with focus on the enemy.
- `12d-pad-played`: focus back in the hand after the play.
- `12e-pad-rewards`, `12f-pad-map`: the reward cards and the map.

It logs the focused object's name at each step.
