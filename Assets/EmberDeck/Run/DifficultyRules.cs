using System.Collections.Generic;
using EmberDeck.Content;

namespace EmberDeck.Run
{
    /// <summary>
    /// Difficulty levels, opened one at a time by winning at the highest level reached.
    ///
    /// Each level adds one rule and keeps the ones below it, so level 3 is levels 1 and 2 plus its own.
    /// Every rule is a number the player can read before choosing: more HP on a kind of enemy, less of
    /// their own, a smaller heal. Nothing that changes how an enemy behaves, which would make a harder
    /// level a different game rather than the same game with less room for mistakes.
    /// </summary>
    public static class DifficultyRules
    {
        public const int Max = 5;

        static readonly string[] Rules =
        {
            "Elites have 10% more HP.",
            "Start with 5 less max HP.",
            "Rest sites heal 20% of max HP instead of 30%.",
            "Hallway enemies have 10% more HP.",
            "Bosses have 10% more HP.",
        };

        public static string Name(int level) => level <= 0 ? "Normal" : $"Difficulty {level}";

        /// <summary>Every rule in force at a level, lowest first.</summary>
        public static List<string> RulesAt(int level)
        {
            var rules = new List<string>();
            for (int i = 0; i < Rules.Length && i < level; i++) rules.Add(Rules[i]);
            return rules;
        }

        public static int StartingMaxHp(RunConfig config, int level) => config.MaxHp - (level >= 2 ? 5 : 0);

        public static float RestHealFraction(RunConfig config, int level) => level >= 3 ? 0.2f : config.RestHealFraction;

        /// <summary>Multiplies the enemy HP the fight would otherwise have.</summary>
        public static float EnemyHpMultiplier(RunState run)
        {
            if (run == null) return 1f;
            int level = run.Difficulty;
            if (run.IsBoss) return level >= 5 ? 1.1f : 1f;
            if (run.IsElite) return level >= 1 ? 1.1f : 1f;
            return level >= 4 ? 1.1f : 1f;
        }
    }
}
