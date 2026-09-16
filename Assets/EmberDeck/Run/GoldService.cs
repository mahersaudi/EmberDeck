using EmberDeck.Core;

namespace EmberDeck.Run
{
    /// <summary>
    /// How much gold a run earns, and the only way it is earned or spent.
    ///
    /// Amounts are derived from the run's position, like card rewards, so a resumed save pays out
    /// exactly what the interrupted run would have. The game and RunSimulator both call this, so
    /// the simulator's economy is the game's economy.
    /// </summary>
    public static class GoldService
    {
        /// <summary>Gold for winning the current fight. A boss pays like two elites, for the act that follows it.</summary>
        public static int ForVictory(RunState run)
        {
            var rng = new DeterministicRng(run.Seed ^ unchecked(run.FightNumber * 0x5851F42D) ^ ((run.ActiveNode?.Row ?? 0) * 0x2F1E3D));
            // An elite pays about an extra hallway on top of its relic and its card: it costs about that
            // much more health. A hallway pays double what it used to, because it no longer pays a card
            // (RewardService.OffersCards) — the gold is what the player buys a chosen card with instead.
            return run.IsBoss ? rng.Range(55, 71) : run.IsElite ? rng.Range(30, 41) : rng.Range(22, 31);
        }

        /// <summary>Gold found at a treasure node, alongside its card.</summary>
        public static int ForTreasure(RunState run)
        {
            var rng = new DeterministicRng(run.Seed ^ unchecked(run.FightNumber * 0x4F6CDD1D) ^ ((run.ActiveNode?.Row ?? 0) * 0x1D8E4E27));
            return rng.Range(20, 31);
        }

        public static void Earn(RunState run, int amount)
        {
            if (amount <= 0) return;
            run.Gold += amount;
            run.Stats.GoldEarned += amount;
        }

        /// <summary>Spends the amount if the run can afford it; spends nothing otherwise.</summary>
        public static bool Spend(RunState run, int amount)
        {
            if (amount < 0 || run.Gold < amount) return false;
            run.Gold -= amount;
            run.Stats.GoldSpent += amount;
            return true;
        }
    }
}
