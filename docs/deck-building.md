# Building the deck

A run no longer starts from a fixed ten-card deck. It starts from thirty cards the player chose,
on a screen that opens when New Run is pressed.

## The rules

- **Thirty cards, exactly.** Start is refused — and says so by dimming — at twenty-nine or
  thirty-one.
- **A copy limit that falls with rarity**: 10 of a starter card, 4 of a common, 3 of an uncommon,
  2 of a rare. Without a limit every build is thirty copies of the best rare and the other fifty
  cards may as well not exist. The limit is printed on every tile, next to how many are in the deck.
- **The pool** is the starter cards plus the reward pool plus whatever the profile's unlocks have
  added — sixty-three cards with nothing unlocked. Cards that were locked again (a profile reset)
  drop out of a remembered deck when it is read back.
- **The deck is remembered** in `profile.json`, so the second run opens on the deck that played the
  first one and starting it again is one tap.

All of this lives in `DeckBuilder`, not in the screen, because the balance simulator builds decks
too — under the same rules, or the measurement is of a game nobody plays.

## The screen

The pool is drawn as tiles, not as full cards. Sixty cards at card size is four screens of
scrolling, and building a deck means comparing cards against each other, which needs them in view
at once. A tile carries what the choice turns on: the art, the name, the cost, and the copies held
out of the copies allowed. The full rules text is in the tooltip, where it is for every card in the
game.

`Suggested` restores the default deck, `Clear` empties it, a tile adds a copy and a row of the deck
list takes one out.

Two Unity details worth keeping:

- The viewports use a stencil `Mask`, not `RectMask2D`. `RectMask2D` computes its clip rectangle in
  canvas space, and the Arabic stage is mirrored with a negative scale — which inverts that
  rectangle and clips the whole contents away. The first Arabic capture of this screen was an empty
  frame for exactly that reason.
- The tiles are buttons inside a `ScrollRect`, which is what makes a drag scroll and a tap add.

## What it did to the balance

Measured with `./tools/simulate.sh`, 300 runs per policy, five policies. A built deck is **harder**
for the simulated player, not easier — it is diluted by every reward taken on top of it, and thirty
cards means each good card comes up half as often as it did in a ten-card deck:

| starting deck | run win% | Act 1 boss% | deck at the final boss |
|---|---|---|---|
| suggested thirty | 3.3 – 5.0 | 24 – 25 | 43 |
| thirty at random | 1.0 – 2.3 | 10 – 13 | 43 |
| the old ten-card starter (reference) | 5.7 – 8.3 | 30 – 34 | 23 |

Enemy HP scaling per fight went from 8% to 6% (Act 2: 5% to 4%) to put the suggested deck back near
the band the rest of the game was tuned at. The random-deck row is the point of keeping it: a deck
thrown together without reading it loses, which is what makes the screen a decision.

**A sixth card per turn was tried and rejected.** A thirty-card deck cycles slowly, so drawing one
more card each turn looked like the obvious compensation. It took the simulated win rate from
3.3–5.0% to 17.7–23.3%. A sixth card is not a sixth more power — it is another Block every turn,
and the whole game is built on there not being one.

These are bots. They take almost every card reward, never plan a synergy and never skip a fight
they should skip, so a person who chose their own thirty cards should do much better than the table
says — but the table is the only number that can be compared between builds.

## What is left

- **Rewards still add to a built deck**, which takes it from 30 to about 43 by the final boss.
  Skipping a reward is now often right, and the game does not say so anywhere. Either the reward
  screen should make dilution visible, or hallway fights should pay gold instead of cards.
- **No filters or sort order** on the pool: it is ordered attacks, then skills, then powers, each by
  cost. With sixty-three cards that is still readable; it will not be at a hundred.
- **The pad cannot scroll the pool.** Focus moves to a tile that is off screen without bringing it
  into view.
