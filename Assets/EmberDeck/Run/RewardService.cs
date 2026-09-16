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

        /// <summary>
        /// Whether a won fight offers cards at all.
        ///
        /// Hallway fights pay gold instead. A run now starts from thirty cards the player chose, and a
        /// card after every fight took that deck to forty-three by the final boss — every card added
        /// makes the thirty that were chosen come up less often. Gold has no such cost: it buys a card
        /// the player picked, or removes one, or pays for a relic.
        ///
        /// Cards still come from the places that are a choice to enter or a detour to reach: elites,
        /// bosses and treasure. That keeps a card a prize rather than a tax.
        /// </summary>
        public static bool OffersCards(RunState run) =>
            run != null && (run.IsElite || run.IsBoss || run.ActiveNode?.Type == NodeType.Treasure);

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
