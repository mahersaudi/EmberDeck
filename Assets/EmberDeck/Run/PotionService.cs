using System.Collections.Generic;
using System.Linq;
using EmberDeck.Combat;
using EmberDeck.Content;
using EmberDeck.Core;

namespace EmberDeck.Run
{
    /// <summary>
    /// The potion rules: how many can be carried, what a fight drops, what a shop sells, and using one.
    ///
    /// Like gold and card rewards, drops and shelves are derived from the run's position, so a resumed
    /// save finds the same potion. The game and RunSimulator both go through here.
    /// </summary>
    public static class PotionService
    {
        /// <summary>
        /// Two. Three let the bot reach the boss with a full belt and drink it all on the first turn: even
        /// with rarer drops, wins after reaching the boss stayed near two in three. Two keeps a potion a
        /// plan, not a stockpile.
        /// </summary>
        public const int Slots = 2;

        public const int ShopShelf = 2;

        public static bool HasRoom(RunState run) => run.Potions.Count < Slots;

        /// <summary>
        /// The potion a won fight drops, or null. Elites drop more often: they are the fights a potion
        /// is most often spent on, so they refill what they cost.
        /// </summary>
        public static PotionData RollDrop(RunState run, RunConfig config)
        {
            if (run.IsBoss) return null;
            var rng = new DeterministicRng(run.Seed ^ unchecked(run.FightNumber * 0x3243F6A9) ^ ((run.ActiveNode?.Row ?? 0) * 0x0D2B7A55));
            // Pass 1 of potion balance. At 35% and 60% the bot drank about two potions a run, most of them on
            // the boss's first turn from a full belt: run wins went from 27-32% to 44-50%, and wins after
            // reaching the boss from about half to three in four. Fewer potions reach the boss.
            float chance = run.IsElite ? 0.4f : 0.2f;
            return rng.Value01() < chance ? Pick(config, rng) : null;
        }

        /// <summary>Adds a potion if there is a free slot. A full belt refuses rather than replacing one.</summary>
        public static bool TryAdd(RunState run, PotionData potion)
        {
            if (potion == null || !HasRoom(run)) return false;
            run.Potions.Add(potion);
            return true;
        }

        /// <summary>Two different potions for a shop, with prices, from the shop's own random stream.</summary>
        public static List<(PotionData potion, int price)> RollShelf(RunConfig config, DeterministicRng rng)
        {
            var pool = config.PotionPool.Where(p => p != null).ToList();
            var shelf = new List<(PotionData, int)>();
            for (int i = 0; i < ShopShelf && pool.Count > 0; i++)
            {
                int index = rng.Range(0, pool.Count);
                shelf.Add((pool[index], rng.Range(40, 61)));
                pool.RemoveAt(index);
            }
            return shelf;
        }

        /// <summary>Drinks the potion in a slot. Returns false, and keeps the potion, if it cannot be used now.</summary>
        public static bool Use(RunState run, int slot, CombatEngine engine, Actor target)
        {
            if (slot < 0 || slot >= run.Potions.Count) return false;
            var potion = run.Potions[slot];
            if (!engine.UsePotion(potion, target)) return false;
            run.Potions.RemoveAt(slot);
            run.Stats.PotionsUsed++;
            return true;
        }

        static PotionData Pick(RunConfig config, DeterministicRng rng)
        {
            var pool = config.PotionPool.Where(p => p != null).ToList();
            return pool.Count == 0 ? null : pool[rng.Range(0, pool.Count)];
        }
    }
}
