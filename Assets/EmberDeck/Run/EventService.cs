using System;
using System.Collections.Generic;
using System.Linq;
using EmberDeck.Content;
using EmberDeck.Core;

namespace EmberDeck.Run
{
    /// <summary>
    /// The events, and the rules for which one a "?" node holds.
    ///
    /// Every event trades one thing the run has for another: health for upgrades, maximum health for
    /// a rare card, gold for a gamble, a plain card for a better one. None adds a new mechanic; each is
    /// built from health, gold, the deck and upgrades, which the player already understands. That is
    /// what keeps an event a decision rather than a reading exercise.
    ///
    /// An event is never seen twice in one run. The seen list travels in the save, and the event at a
    /// position is derived from the seed, so a resumed run meets the event it would have met.
    /// </summary>
    public static class EventService
    {
        public static readonly IReadOnlyList<RunEvent> All = Build();

        public static RunEvent Pick(RunState run)
        {
            var fresh = All.Where(e => !run.SeenEvents.Contains(e.Id)).ToList();
            if (fresh.Count == 0) fresh = All.ToList();
            return fresh[PositionRng(run, 0x2545F491).Range(0, fresh.Count)];
        }

        public static RunEvent Find(string id) => All.FirstOrDefault(e => e.Id == id);

        public static EventContext Context(RunState run, RunConfig config) =>
            new() { Run = run, Config = config, Rng = PositionRng(run, 0x6C8E9CF5) };

        /// <summary>Takes a choice. Returns the outcome sentence, or null if the choice is blocked.</summary>
        public static string Take(RunEvent evt, EventChoice choice, EventContext context)
        {
            if (choice.Blocked(context) != null) return null;
            if (!context.Run.SeenEvents.Contains(evt.Id)) context.Run.SeenEvents.Add(evt.Id);
            return choice.Apply(context);
        }

        static DeterministicRng PositionRng(RunState run, int salt)
        {
            var node = run.ActiveNode;
            return new DeterministicRng(run.Seed ^ unchecked((node?.Row ?? 0) * 0x27D4EB2F + (node?.Column ?? 0) * 0x165667B1) ^ salt
                                       ^ unchecked((run.Act - 1) * 0x61C88647));
        }

        // ── Building blocks ──────────────────────────────────────────────────────────

        static EventChoice Leave() => new()
        {
            Label = "Leave",
            Effect = "Nothing happens.",
            Apply = _ => "You move on.",
            BotValue = _ => 0,
        };

        /// <summary>Health lost to an event never kills: dying to a menu choice is not a decision.</summary>
        static int LoseHp(RunState run, int amount)
        {
            int lost = Math.Min(amount, run.Hp - 1);
            run.Hp -= lost;
            return lost;
        }

        /// <summary>From the run's own pool, so an event can hand out an unlocked card but never a locked one.</summary>
        static CardData RandomCard(RunState run, RunConfig config, CardRarity rarity, DeterministicRng rng)
        {
            var pool = run.RewardPool(config).Where(c => c != null && c.Rarity == rarity).ToList();
            return pool.Count == 0 ? null : pool[rng.Range(0, pool.Count)];
        }

        static List<string> UpgradeRandom(EventContext context, int count)
        {
            var names = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var candidates = context.Run.UpgradableCards();
                if (candidates.Count == 0) break;
                var card = candidates[context.Rng.Range(0, candidates.Count)];
                if (!context.Run.UpgradeCard(card)) break;
                context.Run.Stats.CardsUpgraded++;
                names.Add(card.DisplayName);
            }
            return names;
        }

        static bool IsStarter(RunConfig config, CardData card) =>
            config.StarterDeck.Any(entry => entry != null && entry.Card == card);

        // ── The events ───────────────────────────────────────────────────────────────

        static IReadOnlyList<RunEvent> Build()
        {
            var events = new List<RunEvent>();

            var shrine = new RunEvent
            {
                Id = "cinder_shrine",
                Title = "The Cinder Shrine",
                Body = "A shrine of cooled slag still breathes heat. Blood is scorched into its steps, " +
                       "and the cards laid on its altar come back sharper.",
            };
            shrine.Choices.Add(new EventChoice
            {
                Label = "Offer blood",
                Effect = "Lose 10 HP. Upgrade 2 random cards.",
                Blocked = c => c.Run.Hp <= 10 ? "not enough HP"
                             : c.Run.UpgradableCards().Count == 0 ? "no card can be upgraded" : null,
                Apply = c =>
                {
                    LoseHp(c.Run, 10);
                    var names = UpgradeRandom(c, 2);
                    return $"The shrine drinks. {string.Join(" and ", names)} come back upgraded.";
                },
                BotValue = c => c.Run.Hp > c.Run.MaxHp * 0.6f ? 3 : -2,
            });
            shrine.Choices.Add(Leave());
            events.Add(shrine);

            var merchant = new RunEvent
            {
                Id = "ember_merchant",
                Title = "The Ember Merchant",
                Body = "A hooded trader warms his hands over a brazier of other people's cards. " +
                       "He will take something plain and give you something he swears is better.",
            };
            merchant.Choices.Add(new EventChoice
            {
                Label = "Trade",
                Effect = "Lose a random starter card. Gain a random uncommon card.",
                Blocked = c => c.Run.Deck.Count <= ShopService.MinimumDeck || !c.Run.Deck.Any(card => IsStarter(c.Config, card))
                    ? "nothing plain enough to trade" : null,
                Apply = c =>
                {
                    var starters = c.Run.Deck.Where(card => IsStarter(c.Config, card)).ToList();
                    var given = starters[c.Rng.Range(0, starters.Count)];
                    var received = RandomCard(c.Run, c.Config, CardRarity.Uncommon, c.Rng);
                    c.Run.Deck.Remove(given);
                    c.Run.Stats.CardsRemoved++;
                    if (received != null)
                    {
                        c.Run.AddCard(received);
                        c.Run.Stats.CardsAdded++;
                    }
                    return $"He takes your {given.DisplayName} and hands you {received?.DisplayName ?? "nothing at all"}.";
                },
                BotValue = _ => 2,
            });
            merchant.Choices.Add(Leave());
            events.Add(merchant);

            var forge = new RunEvent
            {
                Id = "abandoned_forge",
                Title = "The Abandoned Forge",
                Body = "The fire here went out long ago, but the anvil is still warm and a hammer lies across it. " +
                       "Something glitters in the ash.",
            };
            forge.Choices.Add(new EventChoice
            {
                Label = "Lift the hammer",
                Effect = "Gain a random rare card. Lose 5 max HP.",
                Blocked = c => c.Run.MaxHp <= 30 ? "too weak to lift it" : null,
                Apply = c =>
                {
                    c.Run.MaxHp -= 5;
                    c.Run.Hp = Math.Min(c.Run.Hp, c.Run.MaxHp);
                    var card = RandomCard(c.Run, c.Config, CardRarity.Rare, c.Rng);
                    if (card != null)
                    {
                        c.Run.AddCard(card);
                        c.Run.Stats.CardsAdded++;
                    }
                    return $"The hammer is heavier than it looks. You carry away {card?.DisplayName ?? "nothing"}, and something of yourself stays behind.";
                },
                BotValue = c => c.Run.MaxHp >= 55 ? 2 : -1,
            });
            forge.Choices.Add(new EventChoice
            {
                Label = "Sift the ash",
                Effect = "Gain 35 gold.",
                Apply = c =>
                {
                    GoldService.Earn(c.Run, 35);
                    return "Coins, blackened but sound. +35 gold.";
                },
                BotValue = _ => 1,
            });
            forge.Choices.Add(Leave());
            events.Add(forge);

            var spring = new RunEvent
            {
                Id = "hot_spring",
                Title = "The Hot Spring",
                Body = "Water boils up between black stones. It smells of iron and scalds for a moment, then soothes.",
            };
            spring.Choices.Add(new EventChoice
            {
                Label = "Bathe",
                Effect = "Heal 15 HP.",
                Apply = c =>
                {
                    int before = c.Run.Hp;
                    c.Run.Heal(15);
                    return $"The heat draws the ache out. +{c.Run.Hp - before} HP.";
                },
                BotValue = c => c.Run.MaxHp - c.Run.Hp >= 15 ? 3 : -1,
            });
            spring.Choices.Add(new EventChoice
            {
                Label = "Drink",
                Effect = "Gain 3 max HP.",
                Apply = c =>
                {
                    c.Run.MaxHp += 3;
                    c.Run.Hp += 3;
                    return "It burns going down and settles into your bones. +3 max HP.";
                },
                BotValue = _ => 2,
            });
            spring.Choices.Add(Leave());
            events.Add(spring);

            var coals = new RunEvent
            {
                Id = "gamblers_coals",
                Title = "The Gambler's Coals",
                Body = "Three cups over three coals. A grinning imp offers double or nothing, and swears only one cup is cursed.",
            };
            coals.Choices.Add(new EventChoice
            {
                Label = "Bet 50 gold",
                Effect = "Half the time, win 110 gold. Otherwise, lose the bet.",
                Blocked = c => c.Run.Gold < 50 ? "not enough gold" : null,
                Apply = c =>
                {
                    GoldService.Spend(c.Run, 50);
                    if (c.Rng.Value01() < 0.5f)
                    {
                        GoldService.Earn(c.Run, 110);
                        return "The cup lifts on a pile of coins. +110 gold.";
                    }
                    return "The cup is empty. The imp pockets your 50 gold and laughs.";
                },
                // A small edge is not worth a shop visit's gold to a bot that needs that gold.
                BotValue = c => c.Run.Gold >= 150 ? 1 : -1,
            });
            coals.Choices.Add(Leave());
            events.Add(coals);

            var tithe = new RunEvent
            {
                Id = "ash_tithe",
                Title = "The Ash Tithe",
                Body = "A robed keeper sweeps ash from the road and asks for a toll: not gold, but a card, and a little blood to seal it.",
            };
            tithe.Choices.Add(new EventChoice
            {
                Label = "Pay the tithe",
                Effect = "Lose 5 HP. Remove a Strike from your deck.",
                Blocked = c => c.Run.Hp <= 5 ? "not enough HP"
                             : c.Run.Deck.Count <= ShopService.MinimumDeck || !c.Run.Deck.Any(card => card != null && card.Id == "strike")
                                 ? "no Strike to give" : null,
                Apply = c =>
                {
                    LoseHp(c.Run, 5);
                    var strike = c.Run.Deck.First(card => card != null && card.Id == "strike");
                    c.Run.Deck.Remove(strike);
                    c.Run.Stats.CardsRemoved++;
                    return "The keeper burns your Strike on the road. Your deck is lighter for it.";
                },
                BotValue = c => c.Run.Hp > 15 ? 3 : -1,
            });
            tithe.Choices.Add(Leave());
            events.Add(tithe);

            var smith = new RunEvent
            {
                Id = "wandering_smith",
                Title = "The Wandering Smith",
                Body = "A smith with no forge sets an anvil on a rock and offers to rework one of your cards, for a price.",
            };
            smith.Choices.Add(new EventChoice
            {
                Label = "Pay 40 gold",
                Effect = "Upgrade a random card.",
                Blocked = c => c.Run.Gold < 40 ? "not enough gold"
                             : c.Run.UpgradableCards().Count == 0 ? "no card can be upgraded" : null,
                Apply = c =>
                {
                    GoldService.Spend(c.Run, 40);
                    var names = UpgradeRandom(c, 1);
                    return names.Count > 0 ? $"A few sure strokes, and {names[0]} is upgraded." : "The smith shrugs; there was nothing to improve.";
                },
                BotValue = c => c.Run.Gold >= 90 ? 2 : 0,
            });
            smith.Choices.Add(Leave());
            events.Add(smith);

            return events;
        }
    }
}
