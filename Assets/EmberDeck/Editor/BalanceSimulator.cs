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
    /// Plays the encounter thousands of times and reports the spread — for the starter deck
    /// and for one representative deck per archetype.
    ///
    /// The per-archetype run is the part that matters for a 60-card set. A single win rate
    /// tells you the opening fight is fair; it tells you nothing about whether five
    /// archetypes are all actually viable, and a set where two of the five are unplayable
    /// looks exactly the same from one number.
    ///
    /// The policy is intentionally mediocre: it plays like a competent first-time player,
    /// not an optimiser. A fight a bot wins 95% of the time is a fight with no decisions in
    /// it; the target for an opening encounter is roughly 80-90%.
    /// </summary>
    public static class BalanceSimulator
    {
        const int DefaultRuns = 2000;
        const int TurnLimit = 60;
        const string CardFolder = "Assets/EmberDeck/Content/Cards";

        /// <summary>
        /// Representative decks, one per archetype, each 14 cards. They are not optimal
        /// builds — they are what a player plausibly ends up with a few fights in, which is
        /// the population the numbers need to describe.
        /// </summary>
        static readonly (string Name, string[] Cards)[] TestDecks =
        {
            ("Starter", new[]
            {
                "strike:4", "guard:4", "ember_lash:1", "stoke:1"
            }),
            ("Forge", new[]
            {
                "strike:3", "guard:3", "bulwark:1", "anvil_strike:2", "temper:1",
                "reinforce:1", "ironhide:1", "counterweight:1", "second_wind:1"
            }),
            ("Swarm", new[]
            {
                "strike:3", "guard:3", "twin_fangs:2", "flurry:1", "whetstone:2",
                "cinder_storm:1", "quick_jab:1", "frenzy:1"
            }),
            ("Pyre", new[]
            {
                "strike:2", "guard:3", "ember_lash:1", "kindle:2", "scorch:2",
                "wildfire:1", "bellows_blast:1", "immolate:1", "slow_roast:1"
            }),
            ("Overdrive", new[]
            {
                "strike:2", "guard:3", "stoke:1", "bellows:2", "overclock:1",
                "flare:2", "detonate:1", "vent:1", "heat_shield:1"
            }),
            ("Ashfall", new[]
            {
                "strike:3", "guard:3", "cremate:2", "salvage:1", "smoulder:1",
                "pyre_rite:1", "ash_armor:1", "burnt_offering:1", "phoenix_ash:1"
            }),
        };

        [MenuItem("EmberDeck/Run Balance Simulation (all decks)")]
        public static void RunFromMenu()
        {
            var config = AssetDatabase.LoadAssetAtPath<RunConfig>("Assets/EmberDeck/Content/RunConfig.asset");
            if (config == null)
            {
                Debug.LogError("[EmberDeck] RunConfig not found. Run EmberDeck > Generate Content and Scene first.");
                return;
            }

            var library = LoadCardLibrary();
            var report = new StringBuilder();
            report.AppendLine("=== EmberDeck balance ===");
            report.AppendLine($"Encounter : {string.Join(" + ", config.Encounter.Where(e => e != null).Select(e => e.DisplayName))}");
            report.AppendLine($"Player    : {config.MaxHp} HP, {config.EnergyPerTurn} energy, {DefaultRuns} fights per deck");
            report.AppendLine();
            report.AppendLine("deck         cards   win%    turns(med)  HP left(med)  10th pct");
            report.AppendLine("------------------------------------------------------------------");

            foreach (var (name, entries) in TestDecks)
            {
                var deck = BuildDeck(library, entries, out var missing);
                if (missing.Count > 0)
                {
                    report.AppendLine($"{name,-12} SKIPPED — unknown cards: {string.Join(", ", missing)}");
                    continue;
                }
                report.AppendLine(Measure(config, deck, name, DefaultRuns));
            }

            report.AppendLine();
            report.AppendLine("Target: 80-90% for the starter deck. Every archetype deck should clear it more");
            report.AppendLine("comfortably than the starter — that is what makes building one feel like progress —");
            report.AppendLine("but an archetype far above the rest is one that makes the others pointless.");
            Debug.Log(report.ToString());
        }

        // ── Measurement ──────────────────────────────────────────────────────────────

        static string Measure(RunConfig config, List<CardData> deck, string label, int runs)
        {
            int wins = 0, stalled = 0;
            var turnsToWin = new List<int>();
            var hpRemaining = new List<int>();

            for (int run = 0; run < runs; run++)
            {
                var session = new CombatSession(config, 1 + run);
                session.Begin();
                ReplaceDeck(session, deck);

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

            turnsToWin.Sort();
            hpRemaining.Sort();

            string note = stalled > 0 ? $"   ({stalled} stalled)" : "";
            return $"{label,-12} {deck.Count,5}   {100f * wins / runs,5:F1}   " +
                   $"{Median(turnsToWin),10}  {Median(hpRemaining),12}  {Percentile(hpRemaining, 0.10f),8}{note}";
        }

        /// <summary>
        /// Swaps in the test deck after Begin() has already dealt the opening hand, then
        /// redeals. Cheaper than parameterising CombatSession for a tool that only the
        /// editor runs.
        /// </summary>
        static void ReplaceDeck(CombatSession session, List<CardData> deck)
        {
            var state = session.State;
            state.DrawPile.Clear();
            state.Hand.Clear();
            state.DiscardPile.Clear();
            state.ExhaustPile.Clear();

            foreach (var card in deck)
                state.DrawPile.Add(new CardInstance(card));

            state.Rng.Shuffle.Shuffle(state.DrawPile);
            session.Engine.DrawCards(state.CardsPerTurn);
        }

        // ── The policy ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Vents before it overheats, blocks what it can see coming, then hits whatever dies
        /// soonest. No lookahead and no combo awareness — a deliberately unsophisticated
        /// baseline, so a high win rate means the fight is genuinely easy rather than that
        /// the bot is clever.
        /// </summary>
        static void PlayTurn(CombatSession session)
        {
            var state = session.State;
            var engine = session.Engine;

            while (!state.IsOver)
            {
                CardInstance chosen = null;
                Actor target = null;

                // Heat first. Without this branch the bot builds Heat it never spends and
                // the Overdrive archetype reads as suicidal rather than as a trade.
                bool nearOverheat = state.Heat >= state.OverheatThreshold - 2;
                if (nearOverheat)
                    chosen = BestPlayable(state, engine, IsHeatSpender);

                if (chosen == null)
                {
                    int shortfall = IncomingDamage(state) - state.Player.Block;
                    if (shortfall > 0)
                        chosen = BestPlayable(state, engine, IsDefensive);
                }

                if (chosen == null)
                {
                    chosen = BestPlayable(state, engine, card => IsOffensive(card) && !AddsUnwantedHeat(state, card));
                    target = WeakestEnemy(state);
                }

                // Utility last: drawing or buffing is only worth energy once the turn's
                // pressing question — survive or kill — has no affordable answer left.
                chosen ??= BestPlayable(state, engine, card =>
                    !IsOffensive(card) && !IsDefensive(card) && !AddsUnwantedHeat(state, card));

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

        /// <summary>
        /// Picks the best playable card for one role, by estimated value rather than by cost.
        ///
        /// Cost was the first heuristic and it measured the wrong thing: it made the bot play
        /// Anvil Strike ("damage equal to your Block") while holding zero Block, for zero
        /// damage, which scored the whole Forge archetype at 15% and told us nothing about
        /// the cards. A first-time human would not make that play either.
        /// </summary>
        static CardInstance BestPlayable(CombatState state, CombatEngine engine, Func<CardInstance, bool> predicate)
        {
            CardInstance best = null;
            int bestScore = 0;

            foreach (var card in state.Hand)
            {
                if (!predicate(card)) continue;

                int cost = engine.GetCardCost(card);
                if (cost > state.Energy) continue;

                int score = EstimateValue(state, card);
                if (score <= bestScore) continue;
                best = card;
                bestScore = score;
            }
            return best;
        }

        /// <summary>
        /// A rough worth in "points of damage or block". Crude on purpose — it only needs to
        /// order the cards in hand sensibly, not to play well.
        /// </summary>
        static int EstimateValue(CombatState state, CardInstance card)
        {
            int strength = state.Player.GetStatus(StatusType.Strength);
            int dexterity = state.Player.GetStatus(StatusType.Dexterity);
            int worstBurn = state.LivingEnemies().Select(e => e.GetStatus(StatusType.Burn)).DefaultIfEmpty(0).Max();
            int enemies = Mathf.Max(1, state.LivingEnemies().Count());
            bool hitsAll = card.Data.Target == TargetMode.AllEnemies;
            int spread = hitsAll ? enemies : 1;

            int value = 0;
            foreach (var effect in card.Data.Effects)
            {
                switch (effect)
                {
                    case DealDamageEffect d:
                        value += (d.Amount + strength) * Mathf.Max(1, d.Hits) * spread;
                        value += d.PerHitStatusAmount * Mathf.Max(1, d.Hits) * 2 * spread;
                        break;
                    case DamageFromBlockEffect b:
                        value += Mathf.FloorToInt(state.Player.Block * b.Multiplier) * spread;
                        break;
                    case DamageFromStatusEffect s:
                        value += Mathf.FloorToInt(worstBurn * s.Multiplier);
                        break;
                    case ScaleWithHeatEffect h:
                        value += (h.Base + state.Heat) * (h.Payout == HeatPayout.Damage ? spread : 1);
                        break;
                    case SpendHeatEffect sh:
                    {
                        int pool = sh.MaxSpent > 0 ? Mathf.Min(state.Heat, sh.MaxSpent) : state.Heat;
                        int payout = Mathf.FloorToInt(pool * sh.Ratio);
                        value += sh.Payout == HeatPayout.Burn ? payout * 2 : payout * (sh.AllEnemies ? enemies : 1);
                        break;
                    }
                    case ScaleWithCounterEffect c:
                    {
                        int count = c.Counter == CombatCounter.CardsPlayedThisTurn
                            ? state.CardsPlayedThisTurn
                            : state.CardsExhaustedThisCombat;
                        value += count * c.PerPoint;
                        break;
                    }
                    case GainBlockEffect gb:
                        value += gb.Amount + dexterity;
                        break;
                    case MultiplyBlockEffect mb:
                        value += state.Player.Block * (mb.Multiplier - 1);
                        break;
                    case MultiplyStatusEffect ms:
                        value += worstBurn * (ms.Multiplier - 1) * 2 * (ms.AllEnemies ? enemies : 1);
                        break;
                    case ApplyStatusEffect st:
                        // Burn pays out over several turns; the flat 2x is deliberately blunt.
                        value += st.ApplyToSelf ? st.Amount * 4 : st.Amount * 2 * spread;
                        break;
                    case DrawCardsEffect dr:
                        value += dr.Amount * 4;
                        break;
                    case GainEnergyEffect en:
                        value += en.Amount * 6;
                        break;
                    case HealEffect he:
                        value += he.Amount;
                        break;
                    case GainHeatEffect gh:
                        // Heat is only worth anything if there is room to hold it.
                        value += state.Heat + gh.Amount > state.OverheatThreshold ? -gh.Amount : gh.Amount;
                        break;
                    case ExhaustCardEffect:
                        value += 1;
                        break;
                    case Content.Powers.TriggeredPowerEffect:
                    case RuleChangeEffect:
                        // Powers pay off over the whole fight; a flat premium beats trying to
                        // model that, and the bot only has to want to play them early.
                        value += 10;
                        break;
                }
            }
            return value;
        }

        static bool IsOffensive(CardInstance card) =>
            card.Data.Effects.Any(e => e is DealDamageEffect or DamageFromBlockEffect or DamageFromStatusEffect
                                            or MultiplyStatusEffect or ApplyStatusEffect { ApplyToSelf: false });

        static bool IsDefensive(CardInstance card) =>
            card.Data.Effects.Any(e => e is GainBlockEffect or MultiplyBlockEffect
                                            or SpendHeatEffect { Payout: HeatPayout.Block }
                                            or ScaleWithHeatEffect { Payout: HeatPayout.Block }
                                            or ScaleWithCounterEffect { Payout: HeatPayout.Block });

        static bool IsHeatSpender(CardInstance card) => card.Data.Effects.Any(e => e is SpendHeatEffect);

        /// <summary>Don't stoke a fire that is already costing HP.</summary>
        static bool AddsUnwantedHeat(CombatState state, CardInstance card) =>
            state.Heat >= state.OverheatThreshold &&
            card.Data.Effects.Any(e => e is GainHeatEffect);

        // ── Deck loading ─────────────────────────────────────────────────────────────

        static Dictionary<string, CardData> LoadCardLibrary()
        {
            var library = new Dictionary<string, CardData>();
            foreach (var guid in AssetDatabase.FindAssets("t:CardData", new[] { CardFolder }))
            {
                var card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
                if (card != null && !string.IsNullOrEmpty(card.Id)) library[card.Id] = card;
            }
            return library;
        }

        static List<CardData> BuildDeck(Dictionary<string, CardData> library, string[] entries, out List<string> missing)
        {
            var deck = new List<CardData>();
            missing = new List<string>();

            foreach (var entry in entries)
            {
                var parts = entry.Split(':');
                if (!library.TryGetValue(parts[0], out var card))
                {
                    missing.Add(parts[0]);
                    continue;
                }
                int count = parts.Length > 1 ? int.Parse(parts[1]) : 1;
                for (int i = 0; i < count; i++) deck.Add(card);
            }
            return deck;
        }

        static int Median(List<int> sorted) => sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];

        static int Percentile(List<int> sorted, float fraction) =>
            sorted.Count == 0 ? 0 : sorted[Mathf.Clamp(Mathf.FloorToInt(sorted.Count * fraction), 0, sorted.Count - 1)];
    }
}
