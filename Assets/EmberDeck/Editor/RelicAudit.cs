using System.Linq;
using System.Text;
using EmberDeck.Combat;
using EmberDeck.Content;
using EmberDeck.Content.Relics;
using EmberDeck.Run;
using UnityEditor;
using UnityEngine;

namespace EmberDeck.EditorTools
{
    /// <summary>
    /// Starts one combat per relic and records what it changed against a combat with none.
    ///
    /// A relic wired to the wrong trigger, or holding an effect that targets nobody, does not
    /// throw — it silently does nothing, and in a run that reads as a weak relic rather than a
    /// broken one. Any relic whose snapshot matches the baseline is flagged.
    /// </summary>
    public static class RelicAudit
    {
        readonly struct Snapshot
        {
            public readonly int Strength, Dexterity, Block, Heat, Threshold, EnemyBurn, AttackDealt, BlockFromExhaust;

            public Snapshot(int strength, int dexterity, int block, int heat, int threshold,
                            int enemyBurn, int attackDealt, int blockFromExhaust)
            {
                Strength = strength;
                Dexterity = dexterity;
                Block = block;
                Heat = heat;
                Threshold = threshold;
                EnemyBurn = enemyBurn;
                AttackDealt = attackDealt;
                BlockFromExhaust = blockFromExhaust;
            }

            public bool SameAs(Snapshot other) =>
                Strength == other.Strength && Dexterity == other.Dexterity && Block == other.Block &&
                Heat == other.Heat && Threshold == other.Threshold && EnemyBurn == other.EnemyBurn &&
                AttackDealt == other.AttackDealt && BlockFromExhaust == other.BlockFromExhaust;
        }

        [MenuItem("EmberDeck/Audit Relics")]
        public static void Audit()
        {
            var config = AssetDatabase.LoadAssetAtPath<RunConfig>("Assets/EmberDeck/Content/RunConfig.asset");
            if (config == null)
            {
                Debug.LogError("[EmberDeck] RunConfig not found. Generate content first.");
                return;
            }

            var baseline = Measure(config, null);
            var report = new StringBuilder();
            report.AppendLine($"=== Relic audit ({config.RelicPoolFor(config.AllUnlockIds()).Count} relics) ===");
            report.AppendLine("relic                Str Dex Block Heat Thresh EnemyBurn Attack(5) ExhaustBlock");
            report.AppendLine(Row("(none)", baseline));

            int inert = 0;
            foreach (var relic in config.RelicPoolFor(config.AllUnlockIds()))
            {
                if (relic == null) continue;
                var snapshot = Measure(config, relic);
                bool unchanged = snapshot.SameAs(baseline);
                if (unchanged) inert++;
                report.AppendLine(Row(relic.DisplayName, snapshot) + (unchanged ? "   <-- DOES NOTHING" : ""));
            }

            report.AppendLine($"inert relics: {inert} (must be 0)");
            Debug.Log(report.ToString());
        }

        static string Row(string name, Snapshot s) =>
            $"{name,-20} {s.Strength,3} {s.Dexterity,3} {s.Block,5} {s.Heat,4} {s.Threshold,6} " +
            $"{s.EnemyBurn,9} {s.AttackDealt,9} {s.BlockFromExhaust,12}";

        static Snapshot Measure(RunConfig config, RelicData relic)
        {
            var run = RunState.Start(config, seed: 4242);
            run.Relics.Clear();
            if (relic != null) run.Relics.Add(relic);

            var session = new CombatSession(config, 4242, run);
            session.Begin();

            var state = session.State;
            var player = state.Player;

            int strength = player.GetStatus(StatusType.Strength);
            int dexterity = player.GetStatus(StatusType.Dexterity);
            int block = player.Block;
            int heat = state.Heat;
            int threshold = state.OverheatThreshold;
            int burn = state.LivingEnemies().Sum(e => e.GetStatus(StatusType.Burn));

            // A fixed 5-damage attack exposes anything that modifies outgoing damage.
            var target = state.FirstLivingEnemy();
            int before = target.Hp + target.Block;
            session.Engine.DealDamage(player, target, 5, isAttack: true);
            int attack = before - (target.Hp + target.Block);

            // One exhaust exposes anything listening for it.
            int blockBefore = player.Block;
            session.Engine.ExhaustTopOfDraw();
            int exhaustBlock = player.Block - blockBefore;

            session.End();
            return new Snapshot(strength, dexterity, block, heat, threshold, burn, attack, exhaustBlock);
        }
    }
}
