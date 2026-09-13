using EmberDeck.Combat;
using UnityEngine;

namespace EmberDeck.Content.Effects
{
    public enum HeatPayout { Damage, Block, Burn }

    /// <summary>
    /// Cashes Heat in. The Overdrive archetype's whole shape lives in this one effect: Heat
    /// only becomes value when you choose to give it up, which is what makes holding it a
    /// real decision rather than pure accumulation.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Spend Heat", fileName = "SpendHeat_")]
    public sealed class SpendHeatEffect : CardEffect
    {
        public HeatPayout Payout = HeatPayout.Damage;

        [Tooltip("Value produced per point of Heat spent.")]
        public float Ratio = 1f;

        [Tooltip("0 spends everything; otherwise spends at most this much.")]
        public int MaxSpent;

        [Tooltip("Hit every living enemy rather than the chosen one.")]
        public bool AllEnemies;

        public override bool IsPerTarget => !AllEnemies && Payout != HeatPayout.Block;

        public override void Apply(in EffectContext context)
        {
            int spent = MaxSpent > 0 ? context.Engine.SpendHeat(MaxSpent) : context.Engine.SpendAllHeat();
            int value = Mathf.FloorToInt(spent * Ratio);
            if (value <= 0) return;

            if (Payout == HeatPayout.Block)
            {
                context.Engine.GainBlock(context.Source, value);
                return;
            }

            if (AllEnemies)
            {
                foreach (var enemy in new System.Collections.Generic.List<Enemy>(context.State.LivingEnemies()))
                    PayOut(context, enemy, value);
            }
            else
            {
                PayOut(context, context.Target, value);
            }
        }

        void PayOut(in EffectContext context, Actor target, int value)
        {
            if (target == null) return;
            if (Payout == HeatPayout.Damage)
                context.Engine.DealDamage(context.Source, target, value, isAttack: true);
            else
                context.Engine.ApplyStatus(target, StatusType.Burn, value);
        }

        public override string Describe()
        {
            string scope = AllEnemies ? " to ALL enemies" : "";
            string pool = MaxSpent > 0 ? $"Lose {MaxSpent} Heat." : "Lose all Heat.";
            string ratio = Mathf.Approximately(Ratio, 1f) ? "" : $"{Ratio:0.##}x ";
            return Payout switch
            {
                HeatPayout.Block  => $"{pool} Gain {ratio}that much Block.",
                HeatPayout.Burn   => $"{pool} Apply {ratio}that much Burn{scope}.",
                _                 => $"{pool} Deal {ratio}that much damage{scope}."
            };
        }
    }
}
