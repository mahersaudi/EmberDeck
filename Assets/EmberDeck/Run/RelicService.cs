using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Content.Relics;
using EmberDeck.Core;

namespace EmberDeck.Run
{
    /// <summary>
    /// Chooses the relic an elite victory grants.
    ///
    /// One implementation, used by both the game and RunSimulator. If the two rolled relics
    /// differently, the simulator would be measuring a game nobody plays.
    /// </summary>
    public static class RelicService
    {
        /// <summary>A relic the run does not already own, or null when the pool is exhausted.</summary>
        public static RelicData Roll(RunState run, RunConfig config)
        {
            var available = new List<RelicData>();
            foreach (var relic in config.RelicPool)
                if (relic != null && !run.Relics.Contains(relic))
                    available.Add(relic);

            if (available.Count == 0) return null;

            // Derived from position, like the card reward, so a resumed save grants the same
            // relic it would have granted without the interruption.
            var rng = new DeterministicRng(run.Seed ^ unchecked(run.FightNumber * 0x165667B1) ^ 0x5BD1E995);
            return available[rng.Range(0, available.Count)];
        }
    }
}
