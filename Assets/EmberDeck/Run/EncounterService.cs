using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Core;

namespace EmberDeck.Run
{
    /// <summary>
    /// Chooses which encounter a fight node holds.
    ///
    /// Each tier's pool is shuffled once per run from the seed, and fights walk through it in
    /// order — so a run sees every encounter of a tier before any repeats. Pure random picks
    /// from a pool of four repeat the same fight back-to-back about one time in four, which
    /// reads as "there is no variety" even when there is.
    ///
    /// The position in the pool is the number of fights of the same tier already visited on the
    /// map, which the save already records. A resumed run therefore meets exactly the enemies it
    /// would have met, and RunSimulator, which calls this same method, measures the same fights.
    /// </summary>
    public static class EncounterService
    {
        public static EncounterData Pick(RunState run, RunConfig config)
        {
            if (run?.ActiveNode == null) return null;

            var tier = TierOf(run.ActiveNode, config);
            if (tier == null) return null;

            var pool = new List<EncounterData>();
            foreach (var encounter in config.Encounters)
                if (encounter != null && encounter.Tier == tier && encounter.Enemies.Count > 0)
                    pool.Add(encounter);
            if (pool.Count == 0) return null;

            // Fisher-Yates from a seed private to this run and tier.
            var rng = new DeterministicRng(run.Seed ^ unchecked((int)tier.Value * 0x632BE5AB) ^ 0x2545F491);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            return pool[EarlierFights(run, config, tier.Value) % pool.Count];
        }

        /// <summary>The tier a node's fight belongs to; null for nodes that hold no fight.</summary>
        public static EncounterTier? TierOf(MapNode node, RunConfig config) => node.Type switch
        {
            NodeType.Boss  => EncounterTier.Boss,
            NodeType.Elite => EncounterTier.Elite,
            NodeType.Fight => node.Row < config.EarlyRows ? EncounterTier.Early : EncounterTier.Late,
            _              => null
        };

        static int EarlierFights(RunState run, RunConfig config, EncounterTier tier)
        {
            if (run.Map == null) return 0;

            int count = 0;
            foreach (var row in run.Map.Grid)
                foreach (var node in row)
                    if (node.Visited && node != run.ActiveNode && TierOf(node, config) == tier)
                        count++;
            return count;
        }
    }
}
