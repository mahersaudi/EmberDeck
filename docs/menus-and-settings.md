# Menus and settings

## The main menu

The game used to open straight onto the map, silently resuming whatever run was on disk. That
suits a developer and not a player, who needs to choose between the run they left and a fresh
one, and needs to know what the saved run is before choosing.

| button | does |
|---|---|
| Continue | shown only when a run is saved, with its fight number, health and deck size beside it |
| New Run | starts over; **asks for a second click when it would abandon a saved run** |
| Settings | volume and display |
| Quit | quits |

Abandoning a run is the one irreversible action on the screen, so it is the one that confirms. The
boss portrait sits beside the buttons: the run's destination, seen before it starts.

## Pausing, and what is saved

**Escape** (or B on a gamepad) backs out one layer at a time. It presses the screen's own Back button
when there is one (settings, the pause menu, the shop and rest-site pickers), puts down a card being
aimed, and otherwise opens the pause menu during a run. Start on a gamepad also opens it. See
`docs/controller.md`.

The run is written to disk each time the map is shown, and nowhere else. Leaving from a fight, a
reward or a rest site therefore returns the player to their last visit to the map. The pause menu
says so in one sentence. That is cheaper than a mid-combat save (hand, four piles, statuses, Heat
and every intent), and far better than a player discovering it by losing a fight they believed
was kept.

The defeat and run-complete screens gained a **Main Menu** button beside New Run.

## Settings

| setting | applies |
|---|---|
| Master, music, sound effects | live, while the slider moves; the effects slider clicks so the level is judged by ear |
| Window: borderless fullscreen, exclusive fullscreen, windowed | on **Apply display** |
| Resolution: every size the display offers, 1280×720 and up | on **Apply display** |
| V-Sync | on **Apply display** |

Display changes wait for Apply because a resolution applied on each arrow press resizes the window
under the pointer while it is still pressing the arrow.

Settings live in PlayerPrefs, not the run save. They belong to the machine, and abandoning a run
must never reset someone's volume. **Display settings are applied at launch only after the player
has chosen them:** Unity already restores the last window size by itself, and forcing a default on
every start would override that and resize the capture harness's window.

### Why the controls look the way they do

Unity's default slider, toggle and dropdown need its built-in sprites, which only editor code can
reach. Every control here is built from coloured rectangles, like the rest of the UI. Options are
**steppers** (a value between two arrows) rather than dropdowns: a code-built dropdown is a
template, a scroll view and a mask; a stepper is three objects that behave the same under a mouse
or the arrow keys.

## Capture harness

The harness now photographs the main menu and the settings screen, starts a run with the menu's own
New Run button (clicking twice if a saved run asks for confirmation), and photographs the pause
menu over the map before walking into the first fight.

## End of run

A run used to end on one word over the board: DEFEAT, or RUN COMPLETE. That wastes the one moment
a roguelike has the player's full attention: the run is over, nothing is at stake, and they want
to know what happened. The end-of-run screen answers three questions.

| question | shown as |
|---|---|
| What happened? | VICTORY or DEFEAT, and "Fell to Kiln Imp and Emberling on floor 5 of 9" |
| How did it go? | floor, fights won (and elites), enemies defeated, damage dealt and taken, biggest hit, cards played, turns, cards added and upgraded, relics, time, seed |
| What did the deck become? | the final deck as cards, rarest first, with ×N on duplicates, and the relics |

- **Defeat waits 1.4 seconds** before the summary covers the board, so the final blow and the
  defeat sting are seen rather than cut off.
- **Beating the boss goes straight to the summary.** It used to offer a card reward first, for a
  next fight that does not exist.
- The seed is shown so a run can be shared or replayed.

**The numbers are saved with the run** (`RunStats`, save format version 3), so a run resumed after
a restart ends with the numbers of the whole run. Older saves still load and start counting from
zero. The numbers are informational only: no rule reads them, and the simulator does not track
them, so they cannot change how a run plays.

Time counts only while a run is on screen, not while a menu, the pause screen or settings are open.
