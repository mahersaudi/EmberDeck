# EmberDeck — Card Set Design

This is the design spec for the full card set. It doubles as an implementation checklist:
every card lists the effect primitives it needs, and the "New mechanics" section is the
work required before any of it can be generated.

---

## 1. The problem with the current 15 cards

The slice's cards work, but they have no identity. `Strike`, `Guard`, `Bulwark`, `Focus` —
each one is a number with no relationship to the others. Nothing a player picks up changes
what the *next* pick should be, and that is the whole engine of the genre. A reward screen
offering three cards is only a decision if the cards point somewhere.

So the set below is not built card-by-card. It is built as **five engines that fight for the
same deck slots**, plus deliberate bridges between them.

---

## 2. The core mechanic: Heat

One resource carries the whole set.

> **Heat** accumulates on the player during a combat. Cards generate it, cards spend it.
> At the end of your turn, if Heat is above your **Overheat threshold (10)**, you lose
> `Heat − threshold` HP. Heat does **not** reset between turns.

Why this and not another status pile:

- **It makes every turn a question.** Bank more Heat for a bigger payoff, or cash out now?
  That question exists on turn 1 and still exists on turn 12.
- **It is a second currency.** Energy is per-turn and identical every turn; Heat is
  cumulative and shaped by your choices. Deckbuilding now has two axes instead of one.
- **It escalates fights naturally.** A long fight becomes dangerous on its own, without the
  designer hand-placing difficulty spikes.
- **It gives defense a second job.** "Vent" cards convert Heat into Block, so defensive
  cards are not just a worse attack — they are the pressure valve for your own engine.

Heat is also what stops the five archetypes from being five separate games: almost every
archetype touches Heat somewhere, which is where the cross-deck combos come from.

---

## 3. Statuses

Kept deliberately small. Five statuses whose interactions a player can hold in their head.

| Status | Effect | Decay |
|---|---|---|
| **Strength** | +N damage per attack | none (whole combat) |
| **Dexterity** | +N Block whenever Block is gained | none |
| **Vulnerable** | takes +50% attack damage | −1 per turn |
| **Weak** | deals −25% attack damage | −1 per turn |
| **Burn** | takes N damage at end of its turn | −1 per turn |

**Burn replaces Poison.** Poison and Burn were the same mechanic wearing different words, and
two names for one rule is a tax on the player for no gain. Burn stays because fire is the
set's identity and because it has real hooks: doubling it, spreading it, reading it as a
damage number.

---

## 4. The five archetypes

Each one is stated with its engine, its payoff, and — most importantly — **its weakness**.
An archetype with no weakness is not a strategy, it is just the correct choice.

### FORGE — turn Block into damage
**Engine:** stack Block far past what you need, then convert it.
**Payoff:** `Anvil Strike` (damage = your Block), `Molten Armor` (Block stops expiring).
**Feels like:** a slow fortress that suddenly punches.
**Weakness:** terrible opening turns; a fight that ends in four turns ends before Forge starts.

### SWARM — many small hits
**Engine:** multi-hit attacks, with Strength multiplying every individual hit.
**Payoff:** `Cinder Storm` (3 damage to all, three times), `Thousand Cuts`.
**Feels like:** a rising drumroll — each new Strength point visibly reshapes the board.
**Weakness:** enemy Block eats small hits whole. One 6-Block enemy blanks a 2-damage volley.

### PYRE — burn over time
**Engine:** apply Burn, then multiply and spread it rather than adding more.
**Payoff:** `Conflagration` (double Burn on all), `Immolate` (damage = target's Burn).
**Feels like:** setting something up two turns before it pays.
**Weakness:** damage arrives late; a burst fight is over before the Burn ticks.

### OVERDRIVE — Heat as ammunition
**Engine:** build Heat as fast as possible and spend it in single enormous hits.
**Payoff:** `Detonate` (damage = 2 × Heat), `Meltdown` (Heat damage to everything).
**Feels like:** driving with the needle in the red.
**Weakness:** you are the one taking overheat damage. Without vents or healing it kills you.

### ASHFALL — exhaust for power
**Engine:** burn cards permanently for effects far above their cost, and profit from doing so.
**Payoff:** `Pyre Rite` (Exhaust → gain Heat), `Ash Armor` (Block per card exhausted).
**Feels like:** a deck that gets smaller, sharper and more consistent as the fight goes.
**Weakness:** the deck shrinks. Run out of cards and the engine has nothing left to eat.

---

## 5. The card set

**Notation.** `1e` = energy cost. Effects list the primitive each needs, so this table is
directly implementable. ★ marks a bridge card that serves two archetypes.

### Starter deck (10 cards)

| Card | Cost | Effect | Count |
|---|---|---|---|
| Strike | 1e | Deal 6 damage. | ×4 |
| Guard | 1e | Gain 5 Block. | ×4 |
| Ember Lash | 1e | Deal 5 damage. Apply 2 Burn. | ×1 |
| Stoke | 1e | Gain 3 Heat. Draw 1. | ×1 |

The two singletons are seeds, not filler: they are the player's first taste of Pyre and
Overdrive, so the first reward screen has something to point at.

### Common (22)

| Card | Cost | Effect | Line |
|---|---|---|---|
| Bulwark | 2e | Gain 12 Block. | Forge |
| Anvil Strike | 1e | Deal damage equal to your Block. | Forge |
| Brace | 0e | Gain 4 Block. | Forge |
| Temper | 1e | **Power:** Gain 2 Dexterity. | Forge |
| Twin Fangs | 1e | Deal 4 damage twice. | Swarm |
| Whetstone | 1e | **Power:** Gain 2 Strength. | Swarm |
| Flurry | 1e | Deal 2 damage 3 times. | Swarm |
| Quick Jab | 0e | Deal 3 damage. | Swarm |
| Kindle | 1e | Apply 3 Burn. | Pyre |
| Scorch | 1e | Deal 4 damage. Apply 2 Burn. | Pyre |
| Fan the Flames | 0e | Apply 2 Burn to ALL enemies. | Pyre |
| Smoulder | 1e | Apply 5 Burn. Exhaust. | Pyre ★ Ashfall |
| Bellows | 0e | Gain 2 Heat. | Overdrive |
| Vent | 1e | Lose all Heat. Gain that much Block. | Overdrive ★ Forge |
| Flare | 1e | Deal 3 + Heat damage. | Overdrive |
| Heat Sink | 1e | Gain 3 Heat. Gain 5 Block. | Overdrive ★ Forge |
| Cremate | 0e | Exhaust the top card of your draw pile. Gain 1 Energy. | Ashfall |
| Ash Cloud | 1e | Apply 2 Weak to ALL enemies. Exhaust. | Ashfall |
| Salvage | 1e | Draw 2. Exhaust. | Ashfall |
| Focus | 1e | Draw 2. | — |
| Second Wind | 1e | Gain 6 Block. Draw 1. | — |
| Mend | 1e | Heal 6. Exhaust. | — |

### Uncommon (20)

| Card | Cost | Effect | Line |
|---|---|---|---|
| Reinforce | 2e | Double your Block. | Forge |
| Counterweight | 2e | Deal damage equal to twice your Block. Lose all Block. | Forge |
| Ironhide | 1e | **Power:** At the start of each turn, gain 3 Block. | Forge |
| Forge Rite | 2e | **Power:** Whenever you gain Block, gain 1 Heat. | Forge ★ Overdrive |
| Cinder Storm | 2e | Deal 3 damage to ALL enemies, three times. | Swarm |
| Rising Heat | 2e | **Power:** Whenever you play an Attack, gain 1 Heat. | Swarm ★ Overdrive |
| Rain of Sparks | 1e | Deal 2 damage 4 times. Each hit applies 1 Burn. | Swarm ★ Pyre |
| Frenzy | 1e | Deal 3 damage per card you played this turn. | Swarm |
| Wildfire | 2e | Apply 5 Burn to ALL enemies. | Pyre |
| Bellows Blast | 1e | Double the Burn on a target. | Pyre |
| Slow Roast | 1e | **Power:** Burn no longer decreases at end of turn. | Pyre |
| Immolate | 2e | Deal damage equal to the target's Burn. | Pyre |
| Detonate | 1e | Deal damage equal to twice your Heat. Lose all Heat. | Overdrive |
| Heat Shield | 1e | Gain Block equal to your Heat. | Overdrive ★ Forge |
| Overclock | 0e | Gain 4 Heat. Draw 2. | Overdrive |
| Coolant | 1e | Lose 6 Heat. Gain 8 Block. | Overdrive ★ Forge |
| Thermal Mass | 1e | **Power:** Your Overheat threshold increases by 5. | Overdrive |
| Pyre Rite | 1e | **Power:** Whenever you Exhaust a card, gain 2 Heat. | Ashfall ★ Overdrive |
| Burnt Offering | 1e | Exhaust a card in your hand. Deal 10 damage. | Ashfall |
| Ash Armor | 1e | Gain 4 Block per card exhausted this combat. Exhaust. | Ashfall ★ Forge |

### Rare (13)

| Card | Cost | Effect | Line |
|---|---|---|---|
| Molten Armor | 2e | **Power:** Your Block is no longer removed at the start of your turn. | Forge |
| Living Anvil | 3e | **Power:** At the end of your turn, deal damage equal to half your Block to a random enemy. | Forge |
| Thousand Cuts | 2e | **Power:** Whenever you play a card, deal 1 damage to ALL enemies. | Swarm |
| Searing Blade | 1e | **Power:** Whenever you apply Burn, gain 1 Strength. | Swarm ★ Pyre |
| Conflagration | 2e | Double the Burn on ALL enemies. | Pyre |
| Eternal Flame | 2e | **Power:** At the end of your turn, apply 2 Burn to ALL enemies. | Pyre |
| Meltdown | 3e | Deal damage equal to your Heat to ALL enemies. Lose all Heat. Take 5 damage. | Overdrive |
| Perpetual Flame | 2e | **Power:** At the start of your turn, gain 3 Heat. Overheat threshold +3. | Overdrive |
| Ember Engine | 2e | **Power:** Whenever you gain Heat, gain 1 Block. | Overdrive ★ Forge |
| Phoenix Ash | 2e | **Power:** Whenever you Exhaust a card, heal 2 HP. | Ashfall |
| Cinder Trance | 2e | **Power:** At the start of your turn, exhaust the top card of your draw pile and gain 1 Energy. | Ashfall |
| Second Forge | 3e | **Power:** Gain 1 Energy at the start of each turn. | — |
| Last Ember | 3e | Deal 22 damage. Exhaust. | — |

**Totals:** 4 starter designs, 22 common, 20 uncommon, 13 rare = **59 cards**.

---

## 6. Combo lines

These are the reason the set exists. Each is a concrete sequence a player can discover.

### "Furnace" — Forge + Overdrive
`Forge Rite` → every Block card now also makes Heat → `Vent` turns that Heat back into Block
→ `Anvil Strike` converts the pile into damage.
The loop feeds itself: Block makes Heat, Heat makes Block, and the exit is a single huge hit.
**The trap:** Forge Rite pushes Heat up whether you want it or not. Without a vent in hand,
your own defense overheats you.

### "Wildfire Swarm" — Swarm + Pyre
`Rain of Sparks` (4 hits, 1 Burn each) + `Searing Blade` (Burn → +1 Strength) → four Strength
from one card → every later multi-hit attack is now enormous.
Add `Slow Roast` and the Burn never decays, so the stack only grows.
**The trap:** it needs two specific rares to be real. Half-drawn, it is a pile of 2-damage hits.

### "Red Line" — pure Overdrive
`Overclock` and `Bellows` to 14+ Heat, eat the overheat damage for two turns, then `Detonate`
for 28+. `Thermal Mass` raises the ceiling so you can hold more before paying.
**The trap:** this deck's health total *is* its resource. One bad enemy turn and there is no
HP left to spend.

### "Ashes to Embers" — Ashfall + Overdrive
`Pyre Rite` (Exhaust → 2 Heat) + `Cinder Trance` (exhaust one card each turn, free energy) →
Heat climbs with no card investment at all → `Meltdown`.
**The trap:** `Cinder Trance` eats your draw pile. You run out of deck and the engine starves.

### "Bunker" — Forge + Ashfall
`Molten Armor` (Block never expires) + `Ash Armor` (4 Block per exhausted card) → Block
accumulates permanently across the whole fight → `Counterweight` cashes it for double.
**The trap:** almost no damage until the payoff card appears. If it is at the bottom, you lose.

### The bridge map

```
            FORGE ─── Vent, Heat Sink, Coolant, Heat Shield ─── OVERDRIVE
              │              Forge Rite, Ember Engine              │
              │                                                     │
         Ash Armor                                            Pyre Rite
              │                                                     │
           ASHFALL ──────────────────────────────────────────── OVERDRIVE
              
            SWARM ─── Rain of Sparks, Searing Blade ─── PYRE
              └────── Rising Heat ────── OVERDRIVE
```

Overdrive touches all four. That is deliberate: it is the hub, so almost any deck can pick up
a Heat card and have it mean something, while a pure Overdrive deck is the most fragile of all.

---

## 7. Rarity and reward maths

A reward screen offers 3 cards. Weights:

| Rarity | Weight | Role |
|---|---|---|
| Common | 60% | raises the floor; playable in any deck |
| Uncommon | 33% | engine pieces; the first card of an archetype |
| Rare | 7% | build-defining; usually the reason a run is memorable |

The rule the numbers follow: **commons are efficiency, uncommons are direction, rares are
identity.** A common should never redirect a deck; a rare should almost always be worth
redirecting for.

---

## 8. New mechanics required

Nothing in section 5 is implementable today. This is the work, in dependency order.

**Model — `CombatState`**
- `int Heat`
- `int OverheatThreshold` (default 10)
- `int CardsPlayedThisTurn`
- `int CardsExhaustedThisCombat`
- `bool BlockPersists` (Molten Armor)
- `bool BurnDecays` (Slow Roast)

**Engine — `CombatEngine`**
- `GainHeat(int)` / `SpendAllHeat() → int`
- Overheat resolution in the player's end-of-turn tick
- Respect `BlockPersists` when clearing Block at turn start

**New events** (powers subscribe to these; the engine stays unaware of them)
- `BlockGainedEvent`
- `HeatGainedEvent`
- `CardExhaustedEvent`
- `StatusAppliedEvent`

**New effect primitives** (each a `CardEffect` ScriptableObject)

| Primitive | Used by |
|---|---|
| `GainHeatEffect` | Stoke, Bellows, Heat Sink, Overclock |
| `SpendHeatEffect` (damage or block per Heat) | Detonate, Vent, Meltdown, Coolant |
| `ScaleWithHeatEffect` (read Heat, do not consume) | Flare, Heat Shield |
| `DamageFromBlockEffect` (multiplier, optional consume) | Anvil Strike, Counterweight |
| `MultiplyBlockEffect` | Reinforce |
| `MultiplyStatusEffect` | Bellows Blast, Conflagration |
| `DamageFromStatusEffect` | Immolate |
| `ScaleWithCounterEffect` (cards played / exhausted) | Frenzy, Ash Armor |
| `ExhaustCardEffect` (from hand or draw pile) | Cremate, Burnt Offering, Cinder Trance |
| `ModifyThresholdEffect` | Thermal Mass, Perpetual Flame |

**New power framework.** Twelve of the 59 cards are Powers with persistent triggers. Today
`CardType.Power` is cosmetic. Powers need to become what relics already are: a behaviour that
attaches to the event bus for the rest of the combat. The relic system already proves the
pattern — `PowerBehaviour` should be the same shape as `RelicBehaviour`, which means the
hardest part of this list is already built and tested.

---

## 9. Order of work

1. Heat in the model + overheat + the four new events. Nothing else can be tested first.
2. `PowerBehaviour`, mirroring `RelicBehaviour`.
3. The ten effect primitives.
4. Generate all 59 cards.
5. Re-run the balance simulator. Expect the first numbers to be wrong — the current bot has
   no concept of Heat and will not play Overdrive correctly, so the simulator's policy needs
   a Heat-aware branch before its win rate means anything.

Step 5 is the one that is easy to skip and expensive to skip: a 59-card set tuned by feel is
a 59-card set where four archetypes are wrong.
