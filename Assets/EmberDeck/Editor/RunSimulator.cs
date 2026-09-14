using System.Collections.Generic;
using System.Linq;
using System.Text;
using EmberDeck.Combat;
using EmberDeck.Content;
using EmberDeck.Run;
using UnityEditor;
using UnityEngine;

namespace EmberDeck.EditorTools
{
    /// <summary>
    /// Plays whole runs — map choices, fights, rests, treasure, elites, the boss — and reports
    /// where runs end.
    ///
    /// The combat simulator answers "is one fight fair?". A run asks different questions that
    /// no single fight can: does a deck that wins fight 1 still win fight 8 against scaled
    /// enemies, is the boss beatable by the deck a run actually produces, and does taking an
    /// elite pay for its risk. Those only show up in aggregate, across the whole path.
    ///
    /// Three map policies are compared rather than one, because "do elites pay off" is a
    /// comparison — a single policy's win rate cannot answer it.
    /// </summary>
    public static class RunSimulator
    {
        const int Runs = 300;
        const int TurnLimit = 60;

        /// <summary>Experiment knob: how many card rewards a won elite grants.</summary>
        static int EliteRewardPicks = 1;

        /// <summary>
        /// Experiment knob: whether the bot buys anything at shops. With it off the map, gold and shop
        /// visits are unchanged — only purchases stop — so the difference between the two passes is
        /// what buying is worth, separated from the change to the map that adding shops also made.
        /// </summary>
        static bool ShopsEnabled = true;

        // Hunter and Avoider exist because Cautious and Greedy turned out to behave almost
        // alike — 0.17 elites per run apart — which is too little difference for a comparison
        // between them to show whether elites pay off. These two plan their route.
        enum Policy { Cautious, Balanced, Greedy, Hunter, Avoider }

        sealed class Result
        {
            public bool BeatBoss;
            public NodeType DiedAt;
            public int DiedOnRow = -1;
            public int HpAtBoss = -1;
            public int DeckAtBoss = -1;
            public int ElitesFought;
            public int Rests, Treasures, Hallways;
            public int CardsAdded, EmptyOffers, RowsVisited;

            /// <summary>
            /// Percent of the boss's health left when the player died to it. This is the number
            /// that says how far off the boss is: 5% left means a small nerf, 60% a rework.
            /// </summary>
            public int BossHpPctLeftOnDeath = -1;
            public int RelicsGained;
            public int Upgrades;
            public int Shops, CardsBought, CardsRemoved, RelicsBought;
            public readonly List<int> HallwayCost = new();
            public readonly List<int> EliteCost = new();
            /// <summary>Every fight entered: which encounter, and the HP it cost if won (-1 if lost).</summary>
            public readonly List<(string Encounter, int Cost)> Fights = new();
        }

        [MenuItem("EmberDeck/Run Full-Run Simulation")]
        public static void RunFromMenu()
        {
            var config = AssetDatabase.LoadAssetAtPath<RunConfig>("Assets/EmberDeck/Content/RunConfig.asset");
            if (config == null)
            {
                Debug.LogError("[EmberDeck] RunConfig not found. Generate content first.");
                return;
            }

            var report = new StringBuilder();
            report.AppendLine($"=== EmberDeck full runs ({Runs} per policy) ===");
            var details = new StringBuilder();
            var allResults = new List<Result>();

            // Add 2 here to test multi-card elite rewards. Tested once: granting two cards
            // instead of one moved greedy boss-reach from 27.7% to 27.3% — no effect — so it is
            // off by default rather than doubling the run time of every simulation.
            foreach (bool shopping in new[] { true, false })
            {
                ShopsEnabled = shopping;
                int picks = EliteRewardPicks;
                report.AppendLine();
                report.AppendLine(shopping ? "[shops on]" : "[shops off: same maps and gold, nothing bought]");
                report.AppendLine("policy     boss win%  reach boss%  elites/run  HP@boss  deck@boss  win|reached  boss HP left at death");
                report.AppendLine("----------------------------------------------------------------------------------------------------");

                foreach (Policy policy in System.Enum.GetValues(typeof(Policy)))
                {
                    var results = new List<Result>(Runs);
                    for (int i = 0; i < Runs; i++)
                        results.Add(PlayRun(config, seed: 10_000 + i, policy));
                    if (shopping) allResults.AddRange(results);

                    int wins = results.Count(r => r.BeatBoss);
                    var reached = results.Where(r => r.HpAtBoss >= 0).ToList();
                    float winGivenReach = reached.Count == 0 ? 0f : 100f * wins / reached.Count;
                    var bossDeaths = results.Where(r => r.BossHpPctLeftOnDeath >= 0)
                                            .Select(r => r.BossHpPctLeftOnDeath);

                    report.AppendLine(
                        $"{policy,-10} {100f * wins / Runs,8:F1}  {100f * reached.Count / Runs,10:F1}  " +
                        $"{results.Average(r => r.ElitesFought),9:F2}  {Median(reached.Select(r => r.HpAtBoss)),7}  " +
                        $"{Median(reached.Select(r => r.DeckAtBoss)),9}  {winGivenReach,10:F1}  {Median(bossDeaths),14}%");

                    if (!shopping) continue;

                    details.AppendLine(
                        $"-- {policy}: HP cost of a won hallway {Median(results.SelectMany(r => r.HallwayCost))}, " +
                        $"won elite {Median(results.SelectMany(r => r.EliteCost))}, " +
                        $"relics gained per run {results.Average(r => r.RelicsGained):F2}, " +
                        $"upgrades per run {results.Average(r => r.Upgrades):F2}, " +
                        $"shops {results.Average(r => r.Shops):F2}, bought {results.Average(r => r.CardsBought):F2}, " +
                        $"removed {results.Average(r => r.CardsRemoved):F2}, relics bought {results.Average(r => r.RelicsBought):F2}");
                    details.AppendLine($"-- {policy}: where runs ended --");
                    foreach (var group in results.Where(r => !r.BeatBoss)
                                                 .GroupBy(r => (r.DiedAt, r.DiedOnRow))
                                                 .OrderBy(g => g.Key.DiedOnRow))
                        details.AppendLine($"   row {group.Key.DiedOnRow,2}  {group.Key.DiedAt,-8} {group.Count(),4}");
                }
            }

            // Which fights are doing the damage. Pooled across policies: an encounter's cost barely
            // depends on the route that led to it, and pooling gives each row enough fights to read.
            report.AppendLine();
            report.AppendLine("encounter              tier    fights  deaths  death%  median HP cost of a win");
            report.AppendLine("------------------------------------------------------------------------------");
            var tiers = config.Encounters.Where(e => e != null).ToDictionary(e => e.Id, e => e.Tier);
            foreach (var group in allResults.SelectMany(r => r.Fights)
                                            .GroupBy(f => f.Encounter)
                                            .OrderBy(g => tiers.TryGetValue(g.Key, out var t) ? (int)t : 9)
                                            .ThenBy(g => g.Key))
            {
                int fights = group.Count();
                int deaths = group.Count(f => f.Cost < 0);
                string tier = tiers.TryGetValue(group.Key, out var tierValue) ? tierValue.ToString() : "?";
                report.AppendLine($"{group.Key,-22} {tier,-7} {fights,6}  {deaths,6}  {100f * deaths / fights,5:F1}  " +
                                  $"{Median(group.Where(f => f.Cost >= 0).Select(f => f.Cost)),10}");
            }

            report.AppendLine();
            report.Append(details);
            Debug.Log(report.ToString());
        }

        // ── One run ──────────────────────────────────────────────────────────────────

        static Result PlayRun(RunConfig config, int seed, Policy policy)
        {
            var run = RunState.Start(config, seed);
            var result = new Result();

            for (int step = 0; step < 64; step++)
            {
                var options = run.Map.Available().ToList();
                if (options.Count == 0) break;

                var node = ChooseNode(run, options, policy);
                result.RowsVisited++;
                run.Map.Current = node;
                run.ActiveNode = node;
                node.Visited = true;

                if (node.Type == NodeType.Rest)
                {
                    result.Rests++;
                    // Heal when the heal would mostly land; otherwise the forge is worth more.
                    // Above 70% health a 30% heal is largely wasted.
                    var upgradable = run.UpgradableCards();
                    if ((float)run.Hp / run.MaxHp < 0.7f || upgradable.Count == 0)
                    {
                        run.Heal(Mathf.RoundToInt(run.MaxHp * config.RestHealFraction));
                    }
                    else
                    {
                        var target = upgradable.OrderByDescending(c => (int)c.Rarity).First();
                        if (run.UpgradeCard(target)) result.Upgrades++;
                    }
                    continue;
                }

                if (node.Type == NodeType.Shop)
                {
                    result.Shops++;
                    VisitShop(run, config, result);
                    continue;
                }

                if (node.Type == NodeType.Treasure)
                {
                    result.Treasures++;
                    GoldService.Earn(run, GoldService.ForTreasure(run));
                    Tally(result, TakeReward(run, config, eliteOdds: true));
                    run.FightNumber++;
                    continue;
                }

                if (node.Type == NodeType.Boss)
                {
                    result.HpAtBoss = run.Hp;
                    result.DeckAtBoss = run.Deck.Count;
                }
                if (node.Type == NodeType.Elite) result.ElitesFought++;
                if (node.Type == NodeType.Fight) result.Hallways++;

                int hpBefore = run.Hp;
                bool won = Fight(run, config, out int enemyPctLeft, out string encounterId);
                result.Fights.Add((encounterId, won ? hpBefore - run.Hp : -1));
                if (!won)
                {
                    result.DiedAt = node.Type;
                    result.DiedOnRow = node.Row;
                    if (node.Type == NodeType.Boss) result.BossHpPctLeftOnDeath = enemyPctLeft;
                    return result;
                }

                if (node.Type == NodeType.Boss)
                {
                    result.BeatBoss = true;
                    return result;
                }

                // Health a WON fight cost. Losses are excluded on purpose: they cost everything
                // by definition and would swamp the number this exists to show.
                if (node.Type == NodeType.Fight) result.HallwayCost.Add(hpBefore - run.Hp);
                if (node.Type == NodeType.Elite) result.EliteCost.Add(hpBefore - run.Hp);

                // Mirrors CombatView: an elite win grants a relic, rolled before the card reward
                // and before the fight counter advances, so both roll from the same position.
                if (node.Type == NodeType.Elite)
                {
                    var relic = RelicService.Roll(run, config);
                    if (relic != null)
                    {
                        run.Relics.Add(relic);
                        result.RelicsGained++;
                    }
                }

                // Mirrors CombatView: gold is paid before the card reward, from the same position.
                GoldService.Earn(run, GoldService.ForVictory(run));
                Tally(result, TakeReward(run, config, eliteOdds: node.Type == NodeType.Elite));
                if (node.Type == NodeType.Elite)
                    for (int extra = 1; extra < EliteRewardPicks; extra++)
                        Tally(result, TakeReward(run, config, eliteOdds: true, extraPick: extra));
                run.FightNumber++;
            }

            return result;
        }

        /// <summary>
        /// Mirrors CombatView's fight exactly — same seed derivation, same session, same turn
        /// policy — so a number here describes the game the player actually plays.
        /// </summary>
        static bool Fight(RunState run, RunConfig config, out int enemyPctLeft, out string encounterId)
        {
            int seed = run.Seed ^ (run.FightNumber * unchecked((int)0x9E3779B1));
            var session = new CombatSession(config, seed, run);
            session.Begin();
            encounterId = session.EncounterId;

            var state = session.State;
            for (int turn = 0; !state.IsOver && turn < TurnLimit; turn++)
            {
                BalanceSimulator.PlayTurn(session);
                if (state.IsOver) break;
                session.Engine.EndPlayerTurn();
            }

            int left = 0, max = 0;
            foreach (var enemy in state.Enemies)
            {
                max += enemy.MaxHp;
                left += Mathf.Max(0, enemy.Hp);
            }
            enemyPctLeft = max > 0 ? Mathf.RoundToInt(100f * left / max) : 0;

            bool won = state.PlayerWon;
            run.Hp = Mathf.Max(0, state.Player.Hp);
            session.End();
            return won;
        }

        static void Tally(Result result, bool added)
        {
            if (added) result.CardsAdded++;
            else result.EmptyOffers++;
        }

        static bool TakeReward(RunState run, RunConfig config, bool eliteOdds, int extraPick = 0)
        {
            // Extra picks need their own roll: RewardRng is a pure function of position, so a
            // second call at the same node would offer the identical three cards again.
            var rng = extraPick == 0
                ? run.RewardRng()
                : new EmberDeck.Core.DeterministicRng(run.Seed ^ unchecked(run.FightNumber * 31 + extraPick * 7717));
            var offers = RewardService.Roll(config.RewardPool, rng, eliteOdds: eliteOdds);
            if (offers.Count == 0) return false;

            // Take the rarest card offered. Crude, and deliberately so: like the combat bot,
            // this plays like a first-time player, not an optimiser.
            var pick = offers.OrderByDescending(c => (int)c.Rarity).First();
            run.AddCard(pick);
            return true;
        }

        /// <summary>
        /// A plain shopper: thin the deck first, then the best card it can afford, then the relic.
        /// Removing a starter Strike is the classic first purchase — every later draw is better for it.
        /// </summary>
        static void VisitShop(RunState run, RunConfig config, Result result)
        {
            if (!ShopsEnabled) return;
            var stock = ShopService.Roll(run, config);

            var strike = run.Deck.FirstOrDefault(c => c != null && c.Id == "strike");
            if (strike != null && ShopService.RemoveCard(run, stock, strike)) result.CardsRemoved++;

            foreach (var item in stock.Cards.OrderByDescending(i => (int)i.Card.Rarity).ThenBy(i => i.Price))
            {
                if (!ShopService.BuyCard(run, item)) continue;
                result.CardsBought++;
                break;
            }

            if (ShopService.BuyRelic(run, stock)) result.RelicsBought++;
        }

        static MapNode ChooseNode(RunState run, List<MapNode> options, Policy policy)
        {
            float health = (float)run.Hp / run.MaxHp;

            MapNode First(NodeType type) => options.FirstOrDefault(n => n.Type == type);
            // A shop is worth a detour only with gold to spend in it.
            MapNode ShopIfRich() => run.Gold >= 100 ? First(NodeType.Shop) : null;

            switch (policy)
            {
                case Policy.Cautious:
                    // Never takes an elite; rests early.
                    if (health < 0.6f && First(NodeType.Rest) is { } rest) return rest;
                    return First(NodeType.Treasure) ?? ShopIfRich() ?? First(NodeType.Fight) ?? First(NodeType.Rest)
                           ?? options.FirstOrDefault(n => n.Type != NodeType.Elite) ?? options[0];

                case Policy.Greedy:
                    // Takes every elite it can survive a guess at.
                    if (health > 0.5f && First(NodeType.Elite) is { } greedyElite) return greedyElite;
                    if (health < 0.35f && First(NodeType.Rest) is { } greedyRest) return greedyRest;
                    return First(NodeType.Treasure) ?? ShopIfRich() ?? First(NodeType.Fight) ?? options[0];

                case Policy.Hunter:
                    // Routes toward the nearest elite and enters every one it reaches, resting
                    // only when close to death.
                    if (health < 0.35f && First(NodeType.Rest) is { } hunterRest) return hunterRest;
                    return options.OrderBy(DistanceToElite)
                                  .ThenBy(n => PreferenceRank(n.Type))
                                  .First();

                case Policy.Avoider:
                    // Never enters an elite it can step around, and steers away from them.
                    if (health < 0.45f && First(NodeType.Rest) is { } avoiderRest) return avoiderRest;
                    return options.OrderBy(n => n.Type == NodeType.Elite ? 1 : 0)
                                  .ThenByDescending(DistanceToElite)
                                  .ThenBy(n => PreferenceRank(n.Type))
                                  .First();

                default:
                    if (health < 0.45f && First(NodeType.Rest) is { } balancedRest) return balancedRest;
                    if (health > 0.75f && First(NodeType.Elite) is { } balancedElite) return balancedElite;
                    return First(NodeType.Treasure) ?? ShopIfRich() ?? First(NodeType.Fight) ?? options[0];
            }
        }

        static int PreferenceRank(NodeType type) => type switch
        {
            NodeType.Treasure => 0,
            NodeType.Fight    => 1,
            NodeType.Shop     => 1,
            NodeType.Rest     => 2,
            NodeType.Elite    => 3,
            _                 => 4,
        };

        /// <summary>Steps from this node to the nearest reachable elite; 99 when none is reachable.</summary>
        static int DistanceToElite(MapNode start)
        {
            var frontier = new Queue<(MapNode node, int depth)>();
            var seen = new HashSet<MapNode>();
            frontier.Enqueue((start, 0));

            while (frontier.Count > 0)
            {
                var (node, depth) = frontier.Dequeue();
                if (!seen.Add(node)) continue;
                if (node.Type == NodeType.Elite) return depth;
                foreach (var next in node.Next)
                    frontier.Enqueue((next, depth + 1));
            }
            return 99;
        }

        static int Median(IEnumerable<int> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted.Count == 0 ? -1 : sorted[sorted.Count / 2];
        }
    }
}
