using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Core;

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

        /// <summary>How many copies of one card a deck may hold: the rarer it is, the fewer.</summary>
        public static int MaxCopies(CardData card) => card == null ? 0 : card.Rarity switch
        {
            CardRarity.Rare     => 2,
            CardRarity.Uncommon => 3,
            CardRarity.Common   => 4,
            _                   => 10,   // the starter cards, which are filler by design
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
        /// The deck the screen opens with, and the one the Suggested button restores: the old starting
        /// deck, filled out with cheap attacks and skills. Deliberately plain — it has to be a deck a
        /// new player can win with without understanding any card, and one an experienced player will
        /// immediately want to change.
        /// </summary>
        public static List<CardData> Suggested(RunConfig config, ICollection<string> unlocked)
        {
            var deck = new List<CardData>();
            if (config == null) return deck;

            foreach (var entry in config.StarterDeck)
            {
                if (entry?.Card == null) continue;
                for (int i = 0; i < entry.Count && CanAdd(deck, entry.Card); i++) deck.Add(entry.Card);
            }

            // Two copies each of the cheapest attacks and skills, commons before anything rarer. Powers
            // are skipped: one that arrives late in a thirty-card deck has no turns left to pay off.
            var fillers = new List<CardData>(Pool(config, unlocked));
            fillers.RemoveAll(c => c.Type == CardType.Power);
            fillers.Sort((a, b) =>
            {
                int byCost = a.Cost.CompareTo(b.Cost);
                if (byCost != 0) return byCost;
                int byRarity = a.Rarity.CompareTo(b.Rarity);
                if (byRarity != 0) return byRarity;
                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal);
            });

            // Two at a time, down the list: a deck of thirty different cards draws a different hand
            // every turn and can never be relied on, which is the worst thing a default deck can be.
            foreach (var card in fillers)
            {
                if (deck.Count >= DeckSize) break;
                for (int copy = 0; copy < 2 && CanAdd(deck, card); copy++) deck.Add(card);
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
