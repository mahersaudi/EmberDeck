using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Core;

namespace EmberDeck.Run
{
    /// <summary>
    /// Rolls the cards offered after a victory.
    ///
    /// The rule the weights follow: commons are efficiency, uncommons are direction, rares
    /// are identity. A common should never redirect a deck; a rare should almost always be
    /// worth redirecting for. If commons were rare enough to feel like a prize, every choice
    /// would be "take the shiny one" and the screen would stop being a decision.
    /// </summary>
    public static class RewardService
    {
        public const int OfferCount = 3;

        static int Weight(CardRarity rarity, bool elite) => rarity switch
        {
            CardRarity.Common   => elite ? 25 : 60,
            CardRarity.Uncommon => elite ? 50 : 33,
            CardRarity.Rare     => elite ? 25 : 7,
            _                   => 0,   // Starter cards are never offered as rewards.
        };

        /// <summary>
        /// Three distinct cards. Distinct matters: an offer of the same card twice is a
        /// two-card choice wearing a three-card screen, and players notice immediately.
        /// </summary>
        public static List<CardData> Roll(IReadOnlyList<CardData> pool, DeterministicRng rng,
                                          int count = OfferCount, bool eliteOdds = false)
        {
            var offered = new List<CardData>(count);
            var remaining = new List<CardData>(pool);

            while (offered.Count < count && remaining.Count > 0)
            {
                int total = 0;
                foreach (var card in remaining) total += Weight(card.Rarity, eliteOdds);
                if (total <= 0) break;

                int roll = rng.Range(0, total);
                for (int i = 0; i < remaining.Count; i++)
                {
                    roll -= Weight(remaining[i].Rarity, eliteOdds);
                    if (roll >= 0) continue;

                    offered.Add(remaining[i]);
                    remaining.RemoveAt(i);
                    break;
                }
            }
            return offered;
        }
    }
}
