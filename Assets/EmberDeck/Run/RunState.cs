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

        public RunMap Map;

        /// <summary>The node currently being resolved — a fight knows whether it is an elite.</summary>
        public MapNode ActiveNode;

        public bool IsElite => ActiveNode?.Type == NodeType.Elite;
        public bool IsBoss  => ActiveNode?.Type == NodeType.Boss;

        public void Heal(int amount) => Hp = System.Math.Min(MaxHp, Hp + amount);

        /// <summary>
        /// The reward roll for the current position. Derived rather than drawn from a running
        /// stream, so reloading a save produces the same offer instead of a different one.
        /// </summary>
        public DeterministicRng RewardRng() =>
            new(Seed ^ unchecked(FightNumber * 0x27D4EB2F) ^ (ActiveNode?.Row ?? 0) * 7919);

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
            run.Map = RunMap.Generate(run.Rng.Map);
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
