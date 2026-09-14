using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Content.Relics;
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

        /// <summary>Relics owned this run. Starts with the configured ones; elites add more.</summary>
        public readonly List<RelicData> Relics = new();
        public readonly RunRng Rng;
        public readonly int Seed;

        public int MaxHp;
        public int Hp;

        /// <summary>Spent at shops. Earned only through GoldService.</summary>
        public int Gold;

        /// <summary>Cards removed at shops this run; each removal costs more than the last.</summary>
        public int CardsRemoved;

        /// <summary>Events already met this run. An event is never offered twice in one run.</summary>
        public readonly List<string> SeenEvents = new();

        /// <summary>Potions carried, at most PotionService.Slots.</summary>
        public readonly List<PotionData> Potions = new();

        /// <summary>1 for the first fight. Enemy scaling reads this.</summary>
        public int FightNumber = 1;

        public RunMap Map;

        /// <summary>The node currently being resolved — a fight knows whether it is an elite.</summary>
        public MapNode ActiveNode;

        /// <summary>Numbers for the end-of-run screen. Saved with the run; never read by any rule.</summary>
        public RunStats Stats = new();

        public bool IsElite => ActiveNode?.Type == NodeType.Elite;
        public bool IsBoss  => ActiveNode?.Type == NodeType.Boss;

        public void Heal(int amount) => Hp = System.Math.Min(MaxHp, Hp + amount);

        /// <summary>
        /// Replaces one copy of a card with its upgraded version. The run deck holds card
        /// definitions, so an upgrade is a swap to the generated "+" card — which means every
        /// effect, description and save path already works for upgraded cards unchanged.
        /// </summary>
        public bool UpgradeCard(CardData card)
        {
            if (card == null || card.Upgrade == null) return false;
            int index = Deck.IndexOf(card);
            if (index < 0) return false;
            Deck[index] = card.Upgrade;
            return true;
        }

        /// <summary>One entry per distinct card that still has an upgrade available.</summary>
        public List<CardData> UpgradableCards()
        {
            var unique = new List<CardData>();
            foreach (var card in Deck)
                if (card != null && card.Upgrade != null && !unique.Contains(card))
                    unique.Add(card);
            return unique;
        }

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
            run.Gold = config.StartingGold;
            foreach (var relic in config.Relics)
                if (relic != null) run.Relics.Add(relic);
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
