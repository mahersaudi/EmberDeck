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

Measured with `./tools/simulate.sh`, 300 runs per policy, five policies. The short version: a deck
of thirty cards the player chose is about six times as strong as the drafted deck the enemies were
authored against, and the whole bestiary had to be re-tuned around it.

Three decks are simulated, and the spread between them is the point:

| starting deck | run win% | Act 1 boss% | deck at the final boss |
|---|---|---|---|
| **suggested** — what the button builds | 6.3 – 7.3 | 31 – 35 | 34 |
| **best the rules allow** — every efficient card, at its copy limit | 22 – 29 | 58 – 65 | 34 |
| **thirty at random** | 0.3 | 2 – 3 | 34 |
| the old ten-card starter deck (reference only) | 0 – 1 | 8 – 10 | 14 |

What it took to get there, in the order the measurements forced it:

1. **Card rewards moved off hallway fights** (`RewardService.OffersCards`). A card after every fight
   took a built deck from 30 to 43 by the final boss — every card added makes the thirty that were
   chosen come up less often. Hallways pay gold instead, at double the old rate, and cards come from
   elites, bosses and treasure. Deck at the boss: 43 → 34.
2. **Copy limits tightened** to 2 of a common or uncommon, 1 of a rare, 6 of a starter. At the first
   limits (4/3/2) the best legal deck was nine distinct cards stacked four deep and won **97%** of
   runs.
3. **Cremate and Overclock now exhaust.** Both cost nothing and give back energy or two cards, so
   stacked copies simply turned the energy limit off; the best deck held four Cremates and never ran
   out of energy again.
4. **The suggested deck is built from commons only, alternating attacks and skills.** Built from the
   whole pool by the same measure it won 78% of runs, and a default that good ends deck building —
   nothing the player does to it can be an improvement. Scoring attacks and skills in one list was
   just as wrong in the other direction: every efficient card in this game is a defensive one, so the
   deck came out twenty-one skills and nine attacks and taught a new player nothing. Taking two of the
   best attack and two of the best skill in turn gives a deck of fourteen attacks and sixteen skills.
5. **Player HP 63 → 50 and enemy HP × 1.15.** Player health rather than enemy damage, because an
   enemy's announced number has to stay exactly what lands; that promise is worth more than the
   convenience of one knob. Swept together: 63 HP and ×0.92 gave the suggested deck 47–53% of runs,
   50 HP and ×1.15 gives it 3.7–7.7%.

A sixth card per turn was tried twice as the thing a bigger deck seemed to want, and rejected both
times — 17.7–23.3% with the old economy. A sixth card is not a sixth more power; it is another Block
every turn, and the whole game is built on there not being one.

Where the fights sit now (pooled across policies): Act 1 hallways kill 0% early and 10–21% late,
elites 14–21%, the Act 1 boss 34%, Act 2 hallways 14–28%, Act 2 elites 22–28%, the Cinder Wyrm 62%.

These are bots. They take almost every card reward, never plan a synergy and never skip a fight they
should skip, so a person should do much better than the table says — the table is the only number
that can be compared between builds.

## What is left

- **A deck picked carelessly loses every run** — 0.3%, against 3.7–7.7% for the suggested one. That
  is what constructed play means, and the Suggested button is the whole defence against it. If
  playtesters bounce off the screen, the answer is a better default and a clearer first-run tip, not
  a wider band.
- **Act 2's early fights kill nobody** (0–0.7%). The player arrives there at full health with a
  deck that has been working for ten fights; those four encounters need their own pass.
- **No search or sort order** on the pool: it filters by type and by what is in the deck, and within
  that it is ordered attacks, then skills, then powers, each by cost. With sixty-three cards that is
  readable; at two hundred it will want a search box.
- **The copy limits are not explained anywhere but on the cards.** A player who wants four of
  something has to work out from the tile that they cannot have it.
