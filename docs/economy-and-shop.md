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
Map audit: shop 7.2%, treasure 8.4% of nodes, no dead ends, nothing unreachable.

## Saved

Gold and the number of removals travel in the run save (format version 4). Older saves continue
with the starting 75. The end-of-run screen shows cards removed and gold earned.
