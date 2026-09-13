using UnityEngine;

namespace EmberDeck.Content.Effects
{
    /// <summary>
    /// Reads Heat without consuming it. Kept weaker per point than SpendHeatEffect on
    /// purpose: an effect that scales with a resource it never spends is pure upside, and
    /// pure upside removes the decision the resource exists to create.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Scale With Heat", fileName = "ScaleHeat_")]
    public sealed class ScaleWithHeatEffect : CardEffect
    {
        public HeatPayout Payout = HeatPayout.Damage;
        public int Base;

        public override bool IsPerTarget => Payout == HeatPayout.Damage;

        public override void Apply(in EffectContext context)
        {
            int value = Base + context.State.Heat;
            if (value <= 0) return;

            if (Payout == HeatPayout.Block)
                context.Engine.GainBlock(context.Source, value);
            else if (context.Target != null)
                context.Engine.DealDamage(context.Source, context.Target, value, isAttack: true);
        }

        public override string Describe() =>
            Payout == HeatPayout.Block
                ? (Base > 0 ? $"Gain {Base} + Heat Block." : "Gain Block equal to your Heat.")
                : (Base > 0 ? $"Deal {Base} + Heat damage." : "Deal damage equal to your Heat.");
    }
}
