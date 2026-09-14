using System.Collections.Generic;
using EmberDeck.Content;

namespace EmberDeck.Run
{
    /// <summary>
    /// How many Embers a run earns, and what a total has unlocked.
    ///
    /// Embers reward getting further, not only winning: a first run that dies on floor 6 still moves the
    /// track, so the first unlock arrives within a run or two instead of after a win most new players
    /// are hours from. Plain C#, so the simulator can measure how many runs the track takes.
    /// </summary>
    public static class UnlockService
    {
        public const int PerFloor = 1;
        public const int PerElite = 3;
        public const int PerBoss = 10;
        public const int ForWinning = 10;

        /// <param name="floor">Floors reached across every act, as the end-of-run screen counts them.</param>
        public static int Score(RunState run, bool won, int floor)
        {
            int bosses = (run.Act - 1) + (won ? 1 : 0);
            return floor * PerFloor + run.Stats.ElitesWon * PerElite + bosses * PerBoss + (won ? ForWinning : 0);
        }

        public static List<string> UnlockedIds(RunConfig config, int embers)
        {
            var ids = new List<string>();
            foreach (var unlock in config.Unlocks)
                if (unlock != null && embers >= unlock.Threshold) ids.Add(unlock.Id);
            return ids;
        }

        /// <summary>The cheapest unlock not yet reached, or null when the track is complete.</summary>
        public static UnlockData Next(RunConfig config, int embers)
        {
            UnlockData next = null;
            foreach (var unlock in config.Unlocks)
                if (unlock != null && unlock.Threshold > embers && (next == null || unlock.Threshold < next.Threshold))
                    next = unlock;
            return next;
        }

        /// <summary>The highest threshold already passed, 0 when none: where the progress bar starts.</summary>
        public static int PreviousThreshold(RunConfig config, int embers)
        {
            int previous = 0;
            foreach (var unlock in config.Unlocks)
                if (unlock != null && unlock.Threshold <= embers && unlock.Threshold > previous)
                    previous = unlock.Threshold;
            return previous;
        }

        /// <summary>Unlocks whose threshold lies between two totals: what one run just opened.</summary>
        public static List<UnlockData> Crossed(RunConfig config, int before, int after)
        {
            var crossed = new List<UnlockData>();
            foreach (var unlock in config.Unlocks)
                if (unlock != null && unlock.Threshold > before && unlock.Threshold <= after)
                    crossed.Add(unlock);
            return crossed;
        }
    }
}
