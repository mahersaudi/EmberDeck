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

        /// <summary>1 for the first fight. Counts on across acts; rewards and gold derive from it.</summary>
        public int FightNumber = 1;

        /// <summary>1 or 2. Each act is its own map, enemies and boss.</summary>
        public int Act = 1;

        /// <summary>
        /// FightNumber when the current act began. Enemy scaling counts fights within the act, so Act 2's
        /// enemies are authored at their own strength instead of arriving pre-multiplied by every fight
        /// of Act 1. For Act 1 this is 1, which leaves Act 1 exactly as it was balanced.
        /// </summary>
        public int ActStartFight = 1;

        /// <summary>0 for normal; see DifficultyRules. Fixed when the run starts.</summary>
        public int Difficulty;

        /// <summary>
        /// Unlock ids this run draws from, fixed when it starts. A run keeps the pools it began with, so an
        /// unlock earned by another run never changes one already saved mid-way.
        /// </summary>
        public readonly List<string> Unlocked = new();

        /// <summary>Set once the run's result has gone into the profile, so it is never counted twice.</summary>
        public bool ResultRecorded;

        public List<CardData> RewardPool(RunConfig config) => config.RewardPoolFor(Unlocked);
        public List<RelicData> RelicPool(RunConfig config) => config.RelicPoolFor(Unlocked);

        public int FightsIntoAct => FightNumber - ActStartFight;

        public RunMap Map;

        /// <summary>The node currently being resolved — a fight knows whether it is an elite.</summary>
        public MapNode ActiveNode;

        /// <summary>Numbers for the end-of-run screen. Saved with the run; never read by any rule.</summary>
        public RunStats Stats = new();

        public bool IsElite => ActiveNode?.Type == NodeType.Elite;
        public bool IsBoss  => ActiveNode?.Type == NodeType.Boss;

        /// <summary>The boss that ends the run, as opposed to one that opens the next act.</summary>
        public bool IsFinalBoss(RunConfig config) => IsBoss && Act >= config.Acts;

        /// <summary>
        /// The map for the current act. Act 1 uses the run's map stream exactly as before; later acts
        /// derive their own seed, so a save needs only the act number to rebuild the map it was on.
        /// </summary>
        public void GenerateMap()
        {
            Map = RunMap.Generate(Act == 1 ? new RunRng(Seed).Map : new DeterministicRng(Seed ^ unchecked(Act * 0x7F4A7C15)));
        }

        /// <summary>
        /// Past the boss into the next act: a new map, full health, and enemy scaling counted afresh.
        /// Call after the boss reward has advanced FightNumber, so the act's first fight is unscaled.
        /// </summary>
        public void BeginNextAct()
        {
            Act++;
            ActStartFight = FightNumber;
            Hp = MaxHp;
            ActiveNode = null;
            GenerateMap();
        }

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

        /// <summary>
        /// A new run. <paramref name="deck"/> is the deck built before it started; without one the run
        /// falls back to the config's starter deck, which is what the simulator's reference pass and any
        /// one-off fight use.
        /// </summary>
        public static RunState Start(RunConfig config, int seed, int difficulty = 0, IEnumerable<string> unlocked = null,
                                     IEnumerable<CardData> deck = null)
        {
            var run = new RunState(seed, DifficultyRules.StartingMaxHp(config, difficulty));
            run.Difficulty = difficulty;
            if (unlocked != null) run.Unlocked.AddRange(unlocked);
            run.GenerateMap();
            run.Gold = config.StartingGold;
            foreach (var relic in config.Relics)
                if (relic != null) run.Relics.Add(relic);
            if (deck != null)
            {
                foreach (var card in deck)
                    if (card != null) run.Deck.Add(card);
            }

            if (run.Deck.Count == 0)
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
