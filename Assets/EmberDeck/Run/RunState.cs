using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Core;

namespace EmberDeck.Run
{
    /// <summary>
    /// What survives between fights. This is the object that turns a combat into a run: the
    /// deck grows, the health does not come back, and both of those carry forward into the
    /// next encounter.
    ///
    /// Plain C# like everything else in the model, so a run can be simulated end to end
    /// without a scene — which is how reward balance will eventually be measured.
    /// </summary>
    public sealed class RunState
    {
        public readonly List<CardData> Deck = new();
        public readonly RunRng Rng;
        public readonly int Seed;

        public int MaxHp;
        public int Hp;

        /// <summary>1 for the first fight. Enemy scaling reads this.</summary>
        public int FightNumber = 1;

        public RunState(int seed, int maxHp)
        {
            Seed = seed;
            Rng = new RunRng(seed);
            MaxHp = maxHp;
            Hp = maxHp;
        }

        public static RunState Start(RunConfig config, int seed)
        {
            var run = new RunState(seed, config.MaxHp);
            foreach (var entry in config.StarterDeck)
            {
                if (entry?.Card == null) continue;
                for (int i = 0; i < entry.Count; i++)
                    run.Deck.Add(entry.Card);
            }
            return run;
        }

        public void AddCard(CardData card)
        {
            if (card != null) Deck.Add(card);
        }
    }
}
