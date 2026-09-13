using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EmberDeck.Combat;
using EmberDeck.Content;
using EmberDeck.Content.Effects;
using UnityEditor;
using UnityEngine;

namespace EmberDeck.EditorTools
{
    /// <summary>
    /// Plays the encounter thousands of times with a simple policy and reports the spread.
    ///
    /// This is the tool that makes the genre tunable. Balancing a deckbuilder by hand means
    /// playing the same fight fifty times and trusting an impression; a number card that is
    /// 15% too strong is invisible that way and obvious here. It is only possible because
    /// the rules are plain C# with no MonoBehaviour, no scene and no frame loop — which is
    /// the practical payoff of keeping the model separate from the view.
    ///
    /// The policy is intentionally mediocre: it plays like a competent first-time player,
    /// not an optimiser. A fight a bot wins 95% of the time is a fight with no decisions in
    /// it; the target for an opening encounter is roughly 80-90%.
    /// </summary>
    public static class BalanceSimulator
    {
        const int DefaultRuns = 2000;
        const int TurnLimit = 60;

        [MenuItem("EmberDeck/Run Balance Simulation (2000 fights)")]
        public static void RunFromMenu()
        {
            var config = AssetDatabase.LoadAssetAtPath<RunConfig>("Assets/EmberDeck/Content/RunConfig.asset");
            if (config == null)
            {
                Debug.LogError("[EmberDeck] RunConfig not found. Run EmberDeck > Generate Content and Scene first.");
                return;
            }
            Debug.Log(Simulate(config, DefaultRuns, baseSeed: 1));
        }

        public static string Simulate(RunConfig config, int runs, int baseSeed)
        {
            int wins = 0;
            var turnsToWin = new List<int>();
            var hpRemaining = new List<int>();
            int stalled = 0;

            for (int run = 0; run < runs; run++)
            {
                var session = new CombatSession(config, baseSeed + run);
                session.Begin();

                var state = session.State;
                int guard = 0;

                while (!state.IsOver && guard++ < TurnLimit)
                {
                    PlayTurn(session);
                    if (state.IsOver) break;
                    session.Engine.EndPlayerTurn();
                }

                if (guard >= TurnLimit && !state.IsOver) stalled++;

                if (state.PlayerWon)
                {
                    wins++;
                    turnsToWin.Add(state.TurnNumber);
                    hpRemaining.Add(state.Player.Hp);
                }

                session.End();
            }

            return Report(config, runs, wins, stalled, turnsToWin, hpRemaining);
        }

        // ── The policy ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Blocks what it can see coming, then hits whatever dies soonest. No lookahead, no
        /// combo awareness — a deliberately unsophisticated baseline, so a high win rate
        /// means the fight is genuinely easy rather than that the bot is clever.
        /// </summary>
        static void PlayTurn(CombatSession session)
        {
            var state = session.State;
            var engine = session.Engine;

            while (!state.IsOver)
            {
                int incoming = IncomingDamage(state);
                int shortfall = incoming - state.Player.Block;

                CardInstance chosen = null;
                Actor target = null;

                if (shortfall > 0)
                    chosen = BestPlayable(state, engine, IsDefensive);

                if (chosen == null)
                {
                    chosen = BestPlayable(state, engine, IsOffensive);
                    target = WeakestEnemy(state);
                }

                // Utility last: drawing or buffing is only worth energy once the turn's
                // pressing question — survive or kill — has no affordable answer left.
                chosen ??= BestPlayable(state, engine, card => !IsOffensive(card) && !IsDefensive(card));

                if (chosen == null) return;

                var resolvedTarget = chosen.Data.Target == TargetMode.SingleEnemy
                    ? target ?? WeakestEnemy(state)
                    : state.Player;

                if (!engine.TryPlayCard(chosen, resolvedTarget)) return;  // never spin on a card that won't play
            }
        }

        static int IncomingDamage(CombatState state)
        {
            int total = 0;
            foreach (var enemy in state.LivingEnemies())
            {
                var intent = enemy.CurrentIntent;
                if (intent.Kind == IntentKind.Attack)
                    total += intent.Value * Mathf.Max(1, intent.Hits);
            }
            return total;
        }

        static Actor WeakestEnemy(CombatState state)
        {
            Actor weakest = null;
            foreach (var enemy in state.LivingEnemies())
                if (weakest == null || enemy.Hp < weakest.Hp) weakest = enemy;
            return weakest;
        }

        static CardInstance BestPlayable(CombatState state, CombatEngine engine, Func<CardInstance, bool> predicate)
        {
            CardInstance best = null;
            int bestCost = int.MinValue;

            foreach (var card in state.Hand)
            {
                if (!predicate(card)) continue;

                int cost = engine.GetCardCost(card);
                if (cost > state.Energy) continue;

                // Spend the expensive card first: a 2-cost left in hand at end of turn is
                // wasted energy, while a 0-cost usually still fits afterwards.
                if (cost <= bestCost) continue;
                best = card;
                bestCost = cost;
            }
            return best;
        }

        static bool IsOffensive(CardInstance card) => card.Data.Effects.Any(e => e is DealDamageEffect);
        static bool IsDefensive(CardInstance card) => card.Data.Effects.Any(e => e is GainBlockEffect);

        // ── Report ───────────────────────────────────────────────────────────────────

        static string Report(RunConfig config, int runs, int wins, int stalled,
                             List<int> turnsToWin, List<int> hpRemaining)
        {
            var report = new StringBuilder();
            report.AppendLine("=== EmberDeck balance ===");
            report.AppendLine($"Encounter : {string.Join(" + ", config.Encounter.Where(e => e != null).Select(e => e.DisplayName))}");
            report.AppendLine($"Deck      : {config.StarterDeck.Sum(entry => entry.Count)} cards, {config.MaxHp} HP, {config.EnergyPerTurn} energy");
            report.AppendLine($"Fights    : {runs}");
            report.AppendLine();
            report.AppendLine($"Win rate  : {100f * wins / runs:F1}%   ({wins}/{runs})");

            if (turnsToWin.Count > 0)
            {
                turnsToWin.Sort();
                hpRemaining.Sort();
                report.AppendLine($"Turns     : median {Median(turnsToWin)}, range {turnsToWin[0]}-{turnsToWin[^1]}");
                report.AppendLine($"HP left   : median {Median(hpRemaining)}, 10th pct {Percentile(hpRemaining, 0.10f)}");
            }
            if (stalled > 0)
                report.AppendLine($"Stalled   : {stalled} fights hit the {TurnLimit}-turn limit — check for an unkillable state.");

            report.AppendLine();
            report.AppendLine("Target for an opening fight: 80-90% win rate. Above that there is no decision;");
            report.AppendLine("below it the run ends before the deck has a chance to become interesting.");
            return report.ToString();
        }

        static int Median(List<int> sorted) => sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];

        static int Percentile(List<int> sorted, float fraction) =>
            sorted.Count == 0 ? 0 : sorted[Mathf.Clamp(Mathf.FloorToInt(sorted.Count * fraction), 0, sorted.Count - 1)];
    }
}
