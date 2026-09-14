# Gold and the shop

Gold turns fights into a currency and the map into a plan: a route through two shops is a
different run from a route through two elites.

## Earning

| source | gold |
|---|---|
| start of a run | 75 |
| hallway fight | 10–16 |
| elite fight | 25–35, on top of its relic |
| treasure | 20–30, on top of its card |
| boss | none: the run ends there |

Every amount is derived from the run's position (`GoldService`), like card rewards, so a
resumed save pays exactly what the interrupted run would have.

## The shop

A map node from the third row onward (`S` on the map).

| on sale | price | why |
|---|---|---|
| 5 cards | common 45–55, uncommon 68–82, rare 135–165 | a common is efficiency, a rare is identity and costs three commons |
| one card at half price | — | a reason to look at every shelf, not only the rares |
| one relic the run does not own | 140–170 | a permanent effect on every card at once |
| removing a card | 75, then +25 each time | the most valuable service in the genre and the least obvious |

Removal never takes a deck below 5 cards, and each shop removes at most one. Prices are coloured
by whether they can be paid now, and an unaffordable click shakes the price instead of opening a
dialog. Stock is derived from the shop's position, so a shop revisited after a restart shows the
same shelves.

`ShopService` holds every rule. `ShopView` and `RunSimulator` both buy through it, so a price change
is measured the moment it is made.

## Balance: the map was the lever, not the prices

The first version put shops at 13% of nodes, taking five points from fights and eight from
treasure. Run wins in the simulator rose from **24–29% to 31–35%**. Cutting gold income by about a
fifth changed nothing measurable (32–36%).

So the simulator gained a switch that stops the bot buying anything while leaving maps, gold and
shop visits unchanged:

| map | shops on | shops off (nothing bought) |
|---|---|---|
| shops carved from fights | 32–36% | **35–39%** |
| shops carved from treasure | **24–29%** | 25–33% |

Two things fall out of that table.

- **The easier runs came from the map.** A shop is a step with no damage and no enemy scaling. Taking
  shops out of the fight share meant fewer fights per run. Restoring fights and elites to 70% of
  nodes, with shops carved from treasure, brought run wins back to 24–29%, where they were before
  shops existed.
- **Buying lowers the bot's win rate slightly** (2–4 points). Its rule is to remove a Strike, then buy
  the rarest card it can afford, and a rare it plays poorly is worse than no card. That is a limit of
  the bot, not a verdict on the shop: a person buys for the deck they are building.

The bot never bought a relic: after a removal and a card, it never had 140 gold left. A person who
skips cards to save for a relic can; whether relic prices are right needs real play to judge.

Measured shape of a run with shops on: about 0.65 shop visits, 0.52 cards bought and 0.52 removals.
Map audit before events existed: shop 7.2%, treasure 8.4% of nodes, no dead ends, nothing unreachable.

## Saved

Gold and the number of removals travel in the run save (format version 4). Older saves continue
with the starting 75. The end-of-run screen shows cards removed and gold earned.

## Events

A "?" node holds a short scene and a choice. Every event trades one thing the run has for another,
using only what the player already understands: health, maximum health, gold, the deck and upgrades.

| event | choice | trade |
|---|---|---|
| The Cinder Shrine | Offer blood | lose 10 HP, upgrade 2 random cards |
| The Ember Merchant | Trade | a random starter card for a random uncommon |
| The Abandoned Forge | Lift the hammer / Sift the ash | a random rare for 5 max HP, or 35 gold |
| The Hot Spring | Bathe / Drink | heal 15 HP, or gain 3 max HP |
| The Gambler's Coals | Bet 50 gold | half the time win 110 gold |
| The Ash Tithe | Pay the tithe | lose 5 HP, remove a Strike |
| The Wandering Smith | Pay 40 gold | upgrade a random card |

- Every event can be left, and every choice states its effect before it is taken.
- **A choice that cannot be taken stays on screen with the reason** ("not enough gold"). Hiding it
  would hide that it existed.
- Health lost to an event never kills.
- No event repeats within a run; the seen list travels in the save (format version 5). The event at a
  position, and any gamble, is derived from the seed, so a resumed run meets the same event with the
  same outcome.
- `EventService` holds the events and the rules; `EventView` and `RunSimulator` both take choices
  through it. Each choice carries a `BotValue`, so a new event cannot ship without telling the
  simulator how a plain player would treat it.

Events take their map share from treasure and shops, never from fights: shop 5.0%, treasure 5.5%,
event 5.2% of nodes, with no dead ends and nothing unreachable.

### Balance, and where it was left

Adding events raised run wins by about 3.5 points for every policy (24–29% to 28–32%). Making the
events less generous — the shrine costs 10 HP instead of 8, the spring heals 15 instead of 20 and
grants 3 max HP instead of 4, the forge's gold is 35 instead of 40 — moved it by less than a point
(27–32%).

So, as with shops, the lever is routing more than rewards: the bot takes an event over a fight
whenever both are offered, which means slightly fewer fights per run. Tuning further would tune the
game to that bot. It was left here — about one run in three or four won by a deliberately plain
player — for real play to judge.

## Potions

A potion is a single-use effect carried between fights and drunk during the player's turn for no
energy. Each is an existing card effect used without a card, so no potion needs a rule of its own.
Colour carries the kind, so the belt reads at a glance.

| potion | effect | colour |
|---|---|---|
| Healing Draught | heal 15 HP | red |
| Iron Tonic | gain 14 Block | blue |
| Fire Flask | deal 8 damage to ALL enemies | orange |
| Burning Oil | apply 7 Burn to an enemy | orange |
| Strength Brew | gain 2 Strength | gold |
| Energy Draught | gain 2 Energy | gold |
| Swift Elixir | draw 3 cards | green |
| Weakening Ash | apply 1 Weak and 2 Vulnerable to an enemy | purple |

- **Two slots.** A full belt refuses a new potion rather than replacing one; the reward screen says so.
- Fights drop a potion 20% of the time, elites 40%, the boss never. Shops sell two for 40–60 gold.
- Aimed potions are selected and then aimed at an enemy, exactly like a card.
- Drops and shelves are position-derived; carried potions travel in the save (format version 6).
- The simulator's bot drinks attack and buff potions on the first turn of an elite or the boss, and
  healing or Block below 40% health.

### Balance: the boss fight was the lever

| change | run wins |
|---|---|
| before potions | 27–32% |
| potions: 3 slots, drops 35% / 60% | **44–50%** |
| drops 20% / 40% | 37–44% |
| 2 slots, Fire Flask 10 → 8, Weakening Ash Weak 2 → 1 | 37–43% |

Cutting drops removed about half the potions and a third of the effect; a smaller belt and weaker
potions then changed nothing measurable. So the simulator gained a switch that lets potions drop
and be bought as usual but never be drunk. With it off, run wins returned exactly to 27–32%:
potions themselves were worth about ten points, from about **one potion drunk per run**.

One potion is worth that much because the boss fight is close. When the bot lost to the Forge
Tyrant, the boss had about 20% of its health left; a single Fire Flask or Strength Brew turns many
of those losses into wins. Weakening potions further would have made them not worth finding.
Shops, events and potions have all added player power, so the challenge was retuned instead: the
Forge Tyrant's health went from 90–100 to 100–110.

| Forge Tyrant health | potions on | potions off |
|---|---|---|
| 90–100 | 37–43% | 27–32% |
| 100–110 | 33–37% | 22–26% |
| **110–120** | **26–30%** | 20–22% |

At 110–120 the game is back where it was before gold, shops, events and potions — about one run in
three or four won by the deliberately plain bot — while potions are still worth about eight points.
That is where it was left for real play to judge.
