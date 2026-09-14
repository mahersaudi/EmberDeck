using System;
using System.Collections.Generic;
using EmberDeck.Content.Relics;

namespace EmberDeck.Content
{
    /// <summary>
    /// One step on the unlock track: cards or a relic that join a run's pools once the player has earned
    /// enough Embers across all their runs.
    ///
    /// Unlocks add content, not stats. A new player and a veteran fight the same enemies with the same
    /// starter deck; what the veteran has earned is more choices on the reward screen. The whole track was
    /// tuned until it added only one or two points of simulated run wins, so the balance measured for the
    /// base game stays true for everyone (docs/meta-progression.md).
    /// </summary>
    [Serializable]
    public sealed class UnlockData
    {
        public string Id;
        public string DisplayName;

        /// <summary>Lifetime Embers needed. Never spent, so an unlock can never be lost.</summary>
        public int Threshold;

        public List<CardData> Cards = new();
        public List<RelicData> Relics = new();

        /// <summary>What the unlock adds, in one line: card names, or what a relic does. Shown under the unlock's name.</summary>
        public string Describe()
        {
            var parts = new List<string>();
            foreach (var card in Cards)
                if (card != null) parts.Add(card.DisplayName);
            foreach (var relic in Relics)
                if (relic != null) parts.Add(relic.DisplayName == DisplayName ? relic.Description : $"{relic.DisplayName}: {relic.Description}");
            return string.Join(", ", parts);
        }
    }
}
