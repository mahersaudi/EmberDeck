using System.Collections.Generic;
using System.Text.RegularExpressions;
using EmberDeck.Combat;
using EmberDeck.Content;
using UnityEngine;

namespace EmberDeck.View
{
    /// <summary>
    /// The game's vocabulary: every word a card or an intent uses that means something specific.
    ///
    /// A player meeting "Vulnerable" for the first time has no way to know it means +50% damage, and
    /// a deckbuilder whose words need a wiki is one people bounce off. Each definition was written
    /// against the rule as CombatEngine implements it, not from memory of what it was meant to do:
    /// the numbers in these sentences are the numbers the engine uses.
    /// </summary>
    public static class Keywords
    {
        public sealed class Keyword
        {
            public readonly string Word;
            public readonly string Icon;
            public readonly Color Color;
            public readonly string Body;

            public Keyword(string word, string icon, Color color, string body)
            {
                Word = word;
                Icon = icon;
                Color = color;
                Body = body;
            }
        }

        public static readonly Color BurnColor = new(1f, 0.56f, 0.2f);
        public static readonly Color StrengthColor = new(0.93f, 0.43f, 0.32f);
        public static readonly Color DexterityColor = new(0.36f, 0.78f, 0.7f);
        public static readonly Color VulnerableColor = new(0.83f, 0.47f, 0.9f);
        public static readonly Color WeakColor = new(0.74f, 0.71f, 0.82f);
        public static readonly Color HeatColor = new(0.96f, 0.5f, 0.24f);
        public static readonly Color ExhaustColor = new(0.7f, 0.67f, 0.76f);
        public static readonly Color PowerColor = new(0.94f, 0.5f, 0.72f);

        // Vocabulary order is tooltip order: resources first, then statuses, then card keywords.
        public static readonly IReadOnlyList<Keyword> All = new[]
        {
            new Keyword("Block", "res_block", Palette.Block,
                "Absorbs damage before HP is lost. Removed at the start of its owner's next turn."),
            new Keyword("Energy", "res_energy", Palette.Energy,
                "Spent to play cards. Refills at the start of each turn."),
            new Keyword("Heat", "res_heat", HeatColor,
                "Built up by cards and kept between turns. Some cards spend it. Too much causes Overheat."),
            new Keyword("Overheat", "res_overheat", Palette.Overheat,
                "At the end of your turn, lose 1 HP for each point of Heat above your Overheat threshold (10, unless a card raises it)."),
            new Keyword("Burn", "status_burn", BurnColor,
                "At the end of its turn, loses HP equal to its Burn, ignoring Block. Then Burn goes down by 1."),
            new Keyword("Strength", "status_strength", StrengthColor,
                "Each attack deals that much more damage."),
            new Keyword("Dexterity", "status_dexterity", DexterityColor,
                "Whenever Block is gained, gain that much more."),
            new Keyword("Vulnerable", "status_vulnerable", VulnerableColor,
                "Takes 50% more damage from attacks. Lasts that many turns."),
            new Keyword("Weak", "status_weak", WeakColor,
                "Deals 25% less damage with attacks. Lasts that many turns."),
            new Keyword("Exhaust", "kw_exhaust", ExhaustColor,
                "Removed from your deck until the end of this combat."),
            new Keyword("Power", "kw_power", PowerColor,
                "Played once. Its effect lasts for the rest of the combat."),
        };

        static readonly Dictionary<string, Keyword> ByWord = new();

        // Whole words only: "Overheat" contains "heat" but is not Heat, and the \b boundaries are
        // what keep one from lighting up inside the other.
        static readonly Regex Words;

        static Keywords()
        {
            var words = new List<string>();
            foreach (var keyword in All)
            {
                ByWord[keyword.Word] = keyword;
                words.Add(Regex.Escape(keyword.Word));
            }
            Words = new Regex(@"\b(" + string.Join("|", words) + @")\b", RegexOptions.Compiled);
        }

        public static Keyword Find(string word) =>
            word != null && ByWord.TryGetValue(word, out var keyword) ? keyword : null;

        public static Keyword For(StatusType status) => Find(status.DisplayName());

        /// <summary>The keywords a card uses, in vocabulary order.</summary>
        public static List<Keyword> ForCard(CardData card)
        {
            var found = new HashSet<string>();
            foreach (Match match in Words.Matches(card.BuildDescription()))
                found.Add(match.Value);
            if (card.Type == CardType.Power) found.Add("Power");
            if (card.Exhaust) found.Add("Exhaust");

            var result = new List<Keyword>();
            foreach (var keyword in All)
                if (found.Contains(keyword.Word)) result.Add(keyword);
            return result;
        }

        /// <summary>Colours every keyword in a sentence, so the words that have a definition look like it.</summary>
        public static string Highlight(string text) =>
            string.IsNullOrEmpty(text)
                ? text
                : Words.Replace(text, match =>
                    $"<color=#{ColorUtility.ToHtmlStringRGB(ByWord[match.Value].Color)}>{match.Value}</color>");
    }
}
