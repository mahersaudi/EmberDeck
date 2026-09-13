using UnityEngine;

namespace EmberDeck.Content.Effects
{
    /// <summary>The Forge payoff: a defensive pile becomes an offensive number.</summary>
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Damage From Block", fileName = "BlockDamage_")]
    public sealed class DamageFromBlockEffect : CardEffect
    {
        public float Multiplier = 1f;

        [Tooltip("Spend the Block to do it. The higher multipliers are paid for this way.")]
        public bool ConsumeBlock;

        public override bool IsPerTarget => true;

        public override void Apply(in EffectContext context)
        {
            if (context.Target == null || context.Source == null) return;

            int damage = Mathf.FloorToInt(context.Source.Block * Multiplier);
            if (ConsumeBlock) context.Source.Block = 0;
            if (damage <= 0) return;

            context.Engine.DealDamage(context.Source, context.Target, damage, isAttack: true);
        }

        public override string Describe()
        {
            string scale = Mathf.Approximately(Multiplier, 1f) ? "your Block" : $"{Multiplier:0.##}x your Block";
            return ConsumeBlock ? $"Deal damage equal to {scale}. Lose all Block." : $"Deal damage equal to {scale}.";
        }
    }
}
