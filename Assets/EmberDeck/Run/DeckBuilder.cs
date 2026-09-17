using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Content.Effects;
using EmberDeck.Core;
using UnityEngine;

namespace EmberDeck.Run
{
    /// <summary>
    /// The rules for building the deck a run starts with: thirty cards, chosen from everything the
    /// player has, with a copy limit that falls as a card gets rarer.
    ///
    /// The rules live here rather than in the screen that draws them, because the balance simulator
    /// builds decks too — a run that starts from thirty chosen cards is a different game from one
    /// that starts from four Strikes, and the only way to know how different is to measure it with
    /// decks built by the same rules a player builds under.
    ///
    /// The copy limit is what keeps a deck a deck. Without it every build is thirty copies of the
    /// best rare, and the rest of the cards may as well not exist.
    /// </summary>
    public static class DeckBuilder
    {
        public const int DeckSize = 30;

        /// <summary>A deck someone designed, offered whole: a way into the builder that is not a blank page.</summary>
        public sealed class Preset
        {
            public string Id;
            public string Name;
            public string Description;
            public (string Card, int Count)[] Cards;
        }

        /// <summary>
        /// Four ready-made decks, one per way this game can be played, built only from cards every new
        /// profile has — nothing here waits on an unlock.
        ///
        /// They exist because a deck thrown together without reading the cards loses every simulated
        /// run, and the Suggested deck, which wins, teaches nothing about why. A preset has a name and a
        /// sentence that says what it does, so a new player learns an archetype by playing one.
        /// Each adds up to exactly thirty and respects the copy limits; if a card is ever renamed or
        /// removed, Build fills the gap from the Suggested deck rather than leaving it short.
        /// </summary>
        public static readonly Preset[] Presets =
        {
            new()
            {
                Id = "pyre", Name = "Pyre",
                Description = "Set everything on fire and outlast it. Burn ignores Block and ticks every turn.",
                Cards = WithBackbone(("ember_lash", 4), ("scorch", 2), ("kindle", 2), ("fan_the_flames", 2),
                                     ("smoulder", 2), ("immolate", 2), ("backdraft", 2)),
            },
            new()
            {
                Id = "anvil", Name = "Anvil",
                Description = "Block everything, then hit back hard. Slow, and very hard to kill.",
                Cards = WithBackbone(("strike", 2), ("anvil_strike", 2), ("reinforce", 2), ("counterweight", 2),
                                     ("ash_armor", 2), ("heat_sink", 2), ("twin_fangs", 2), ("temper", 1),
                                     ("ironhide", 1)),
            },
            new()
            {
                Id = "sparks", Name = "Sparks",
                Description = "Many cheap attacks a turn. Strength makes every one of them count.",
                // Rain of Sparks is out: with it this deck won 16-23% of simulated runs, three times the
                // Suggested deck, and a starting preset that good is the only deck anyone would pick.
                Cards = WithBackbone(("quick_jab", 2), ("twin_fangs", 2), ("flurry", 2), ("salvage", 2),
                                     ("frenzy", 2), ("whetstone", 1), ("ember_lash", 2), ("scorch", 2),
                                     ("cinder_storm", 1)),
            },
            new()
            {
                Id = "overdrive", Name = "Overdrive",
                Description = "Build Heat on purpose, then spend it all at once. The hardest of the four to play.",
                // The simulated player cannot play this deck and the numbers say so (under 5% of runs clear Act
                // 1): its policy only makes Heat when nothing else is worth playing, so every card here that
                // spends Heat finds none to spend. A person plays it the other way round, which is the whole
                // archetype. It is kept, and its description warns that it is the hardest of the four.
                // Heat made and Heat spent in balance, and two Thermal Mass to raise the ceiling. Tried both
                // ways first: with too many cards that add Heat it cleared Act 1 in 10% of simulated runs,
                // burning itself down; with too few, every card that spends Heat spent nothing, and it cleared
                // Act 1 in none.
                Cards = WithBackbone(("stoke", 3), ("bellows", 2), ("heat_sink", 2), ("flare", 2), ("detonate", 2),
                                     ("vent", 2), ("thermal_mass", 2), ("strike", 1)),
            },
        };

        /// <summary>
        /// Fourteen cards every preset shares: two each of Strike, Guard, Second Wind, Bulwark, Brace, Focus and
        /// Cremate. The first presets were pure archetypes and every one of them lost to the Suggested deck —
        /// Overdrive cleared Act 1 in 6% of simulated runs against Suggested's 33% — because a deck built only
        /// around its idea has no Block and nothing to draw with. The backbone is what the archetype stands on.
        /// </summary>
        static (string, int)[] WithBackbone(params (string, int)[] core)
        {
            var cards = new List<(string, int)>
            {
                ("strike", 2), ("guard", 2), ("second_wind", 2), ("bulwark", 2), ("brace", 2), ("focus", 2), ("cremate", 2),
            };
            foreach (var (id, count) in core)
            {
                int existing = cards.FindIndex(c => c.Item1 == id);
                if (existing >= 0) cards[existing] = (id, cards[existing].Item2 + count);
                else cards.Add((id, count));
            }
            return cards.ToArray();
        }

        /// <summary>A preset as cards. Always legal: anything missing from the pool is made up from Suggested.</summary>
        public static List<CardData> Build(Preset preset, RunConfig config, ICollection<string> unlocked)
        {
            var deck = new List<CardData>();
            if (preset == null || config == null) return deck;

            var pool = Pool(config, unlocked);
            foreach (var (id, count) in preset.Cards)
            {
                var card = pool.Find(c => c.Id == id);
                for (int i = 0; i < count && card != null && CanAdd(deck, card); i++) deck.Add(card);
            }

            foreach (var card in Suggested(config, unlocked))
            {
                if (deck.Count >= DeckSize) break;
                if (CanAdd(deck, card)) deck.Add(card);
            }
            return deck;
        }

        /// <summary>The copy limits as one line, for the screen that enforces them.</summary>
        public const string LimitsText = "Copies allowed: starter cards 6, commons and uncommons 2, rares 1.";

        /// <summary>
        /// How many copies of one card a deck may hold: the rarer it is, the fewer.
        ///
        /// Two of anything real, one of a rare, and starter cards as filler. The first version allowed
        /// four commons, three uncommons and two rares, and the best deck the rules allowed then was nine
        /// distinct cards stacked four deep — it won 97% of simulated runs. A deck of thirty now needs
        /// fifteen cards that work, which is the difference between building a deck and finding the two
        /// best cards in the game.
        /// </summary>
        public static int MaxCopies(CardData card) => card == null ? 0 : card.Rarity switch
        {
            CardRarity.Rare     => 1,
            CardRarity.Uncommon => 2,
            CardRarity.Common   => 2,
            _                   => 6,   // the starter cards, which are filler by design
        };

        /// <summary>
        /// Every card that can go in a deck: the starter cards, the reward pool, and whatever the
        /// profile's unlocks add. Ordered the way the screen shows them — attacks, then skills, then
        /// powers, each by cost — so the same card is always in the same place.
        /// </summary>
        public static List<CardData> Pool(RunConfig config, ICollection<string> unlocked)
        {
            var pool = new List<CardData>();
            if (config == null) return pool;

            foreach (var entry in config.StarterDeck)
                if (entry?.Card != null && !pool.Contains(entry.Card)) pool.Add(entry.Card);

            foreach (var card in config.RewardPoolFor(unlocked))
                if (card != null && !pool.Contains(card)) pool.Add(card);

            pool.Sort((a, b) =>
            {
                int byType = a.Type.CompareTo(b.Type);
                if (byType != 0) return byType;
                int byCost = a.Cost.CompareTo(b.Cost);
                if (byCost != 0) return byCost;
                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal);
            });
            return pool;
        }

        public static int CountOf(IEnumerable<CardData> deck, CardData card)
        {
            if (deck == null || card == null) return 0;
            int count = 0;
            foreach (var entry in deck)
                if (entry == card) count++;
            return count;
        }

        public static bool CanAdd(IReadOnlyList<CardData> deck, CardData card) =>
            deck != null && card != null && deck.Count < DeckSize && CountOf(deck, card) < MaxCopies(card);

        /// <summary>A deck that can start a run: exactly thirty cards, and no card over its limit.</summary>
        public static bool IsLegal(IReadOnlyList<CardData> deck)
        {
            if (deck == null || deck.Count != DeckSize) return false;
            foreach (var card in deck)
            {
                if (card == null) return false;
                if (CountOf(deck, card) > MaxCopies(card)) return false;
            }
            return true;
        }

        /// <summary>
        /// Roughly what one play of a card is worth, in points of damage.
        ///
        /// Read off the effects themselves, so a new card is scored the moment it exists and no table
        /// has to be kept in step. Deliberately crude: it values damage, block, a drawn card and a point
        /// of energy, discounts a card that exhausts, and understands nothing about synergy. It exists so
        /// the Suggested deck is built out of cards that do something, rather than out of the cheapest
        /// cards in the pool — the first version sorted by cost, and a deck of nought-cost utility cards
        /// lost to a deck picked at random.
        /// </summary>
        public static float Value(CardData card)
        {
            if (card == null) return 0f;

            float value = 0f;
            foreach (var effect in card.Effects)
            {
                switch (effect)
                {
                    case DealDamageEffect damage:
                        value += damage.Amount * Mathf.Max(1, damage.Hits);
                        value += damage.PerHitStatusAmount * Mathf.Max(1, damage.Hits) * 1.5f;
                        break;
                    case GainBlockEffect block:
                        value += block.Amount * 0.9f;
                        break;
                    case DrawCardsEffect draw:
                        value += draw.Amount * 4f;
                        break;
                    case GainEnergyEffect energy:
                        value += energy.Amount * 5f;
                        break;
                    case HealEffect heal:
                        value += heal.Amount * 0.7f;
                        break;
                    case ApplyStatusEffect status:
                        value += status.Amount * 1.6f;
                        break;
                    // Everything else — Heat, block multipliers, scaling with a counter, rule changes —
                    // is worth something no arithmetic here can state. A small credit keeps such a card
                    // from scoring zero and being treated as blank.
                    case null:
                        break;
                    default:
                        value += 3f;
                        break;
                }
            }

            if (card.Exhaust) value *= 0.6f;
            return value;
        }

        /// <summary>Value per point of energy. The 0.7 keeps a nought-cost card from scoring infinitely well.</summary>
        public static float Efficiency(CardData card) => card == null ? 0f : Value(card) / (card.Cost + 0.7f);

        /// <summary>
        /// The deck the screen opens with, and the one the Suggested button restores: a couple of starter
        /// cards and then the most efficient attacks and skills in the pool, two copies each. It has to be
        /// a deck a new player can win with without understanding any card, and one an experienced player
        /// will immediately want to change.
        /// </summary>
        public static List<CardData> Suggested(RunConfig config, ICollection<string> unlocked)
        {
            var deck = new List<CardData>();
            if (config == null) return deck;

            // Two of each starter card, not the four the old opening deck held. Strike and Guard are
            // filler by design, and a suggested deck that is twelve of them loses to a deck thrown
            // together at random — measured, before this cap.
            foreach (var entry in config.StarterDeck)
            {
                if (entry?.Card == null) continue;
                int copies = Mathf.Min(entry.Count, 2);
                for (int i = 0; i < copies && CanAdd(deck, entry.Card); i++) deck.Add(entry.Card);
            }

            // Two copies each of the most efficient common attacks and skills.
            //
            // Commons only, on purpose. Built from the whole pool by the same measure, this deck won 78%
            // of simulated runs — a default that good is the end of deck building, because nothing the
            // player does to it can be an improvement. The uncommons and rares are what they are for.
            // Powers are skipped too: one that arrives late in a thirty-card deck has no turns left to
            // pay off.
            var fillers = new List<CardData>(Pool(config, unlocked));
            fillers.RemoveAll(c => c.Type == CardType.Power || c.Rarity > CardRarity.Common);
            fillers.Sort((a, b) =>
            {
                int byValue = Efficiency(b).CompareTo(Efficiency(a));
                if (byValue != 0) return byValue;
                int byCost = a.Cost.CompareTo(b.Cost);
                if (byCost != 0) return byCost;
                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal);
            });

            // Two at a time, alternating attacks and skills, down the two lists. Two at a time because a
            // deck of thirty different cards draws a different hand every turn and can never be relied
            // on; alternating because scoring the whole pool together built a deck of twenty-one skills
            // and nine attacks — every efficient card in this game is a defensive one, and a deck that
            // cannot kill anything teaches a new player nothing about the game.
            var attacks = fillers.FindAll(c => c.Type == CardType.Attack);
            var skills = fillers.FindAll(c => c.Type != CardType.Attack);
            for (int i = 0; deck.Count < DeckSize && (i < attacks.Count || i < skills.Count); i++)
            {
                if (i < attacks.Count)
                    for (int copy = 0; copy < 2 && CanAdd(deck, attacks[i]); copy++) deck.Add(attacks[i]);
                if (deck.Count >= DeckSize) break;
                if (i < skills.Count)
                    for (int copy = 0; copy < 2 && CanAdd(deck, skills[i]); copy++) deck.Add(skills[i]);
            }

            // Nothing but starter cards left to give: fill with the first card that still fits.
            while (deck.Count < DeckSize)
            {
                bool added = false;
                foreach (var card in fillers)
                {
                    if (!CanAdd(deck, card)) continue;
                    deck.Add(card);
                    added = true;
                    break;
                }
                if (!added) break;
            }
            return deck;
        }

        /// <summary>
        /// The strongest deck the rules allow, by Value: two of everything efficient, rares first.
        ///
        /// Not offered to the player anywhere — it is the other end of the measurement. The suggested
        /// deck says what a beginner meets; this says what someone who reads every card and works out the
        /// arithmetic can build, which is what the enemies eventually have to hold up against.
        /// </summary>
        public static List<CardData> Best(RunConfig config, ICollection<string> unlocked)
        {
            var deck = new List<CardData>();
            var pool = new List<CardData>(Pool(config, unlocked));
            pool.RemoveAll(c => c.Type == CardType.Power);
            pool.Sort((a, b) =>
            {
                int byValue = Efficiency(b).CompareTo(Efficiency(a));
                if (byValue != 0) return byValue;
                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal);
            });

            foreach (var card in pool)
            {
                if (deck.Count >= DeckSize) break;
                for (int copy = 0; copy < MaxCopies(card) && CanAdd(deck, card); copy++) deck.Add(card);
            }
            return deck;
        }

        /// <summary>Two lines describing a deck, for the simulator's report.</summary>
        public static string Describe(IReadOnlyList<CardData> deck)
        {
            if (deck == null || deck.Count == 0) return "(empty)";

            var seen = new List<CardData>();
            var parts = new List<string>();
            foreach (var card in deck)
            {
                if (seen.Contains(card)) continue;
                seen.Add(card);
                parts.Add($"{CountOf(deck, card)}x {card.DisplayName}");
            }
            return string.Join(", ", parts);
        }

        /// <summary>
        /// A legal deck drawn at random. Not offered to the player — it is how the simulator plays the
        /// person who builds a deck without reading it, which is the low end of what balance has to hold for.
        /// </summary>
        public static List<CardData> Randomised(RunConfig config, ICollection<string> unlocked, DeterministicRng rng)
        {
            var deck = new List<CardData>();
            var pool = Pool(config, unlocked);
            if (pool.Count == 0 || rng == null) return deck;

            for (int guard = 0; deck.Count < DeckSize && guard < DeckSize * 40; guard++)
            {
                var card = pool[rng.Range(0, pool.Count)];
                if (CanAdd(deck, card)) deck.Add(card);
            }
            return deck;
        }

        /// <summary>Card ids back into cards, dropping anything the pool no longer holds — a card can be locked again.</summary>
        public static List<CardData> Resolve(RunConfig config, IEnumerable<string> ids, ICollection<string> unlocked)
        {
            var deck = new List<CardData>();
            if (config == null || ids == null) return deck;

            var pool = Pool(config, unlocked);
            foreach (string id in ids)
            {
                foreach (var card in pool)
                {
                    if (card.Id != id) continue;
                    if (CanAdd(deck, card)) deck.Add(card);
                    break;
                }
            }
            return deck;
        }

        public static List<string> Ids(IEnumerable<CardData> deck)
        {
            var ids = new List<string>();
            if (deck == null) return ids;
            foreach (var card in deck)
                if (card != null) ids.Add(card.Id);
            return ids;
        }
    }
}
