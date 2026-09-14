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

**Escape** opens the pause menu during a run (map, fight, reward or rest site) and backs out one
layer at a time: settings first, then the pause menu.

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
