using EmberDeck.Combat;
using UnityEngine;

namespace EmberDeck.Content.Effects
{
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Deal Damage", fileName = "Damage_")]
    public sealed class DealDamageEffect : CardEffect
    {
        public int Amount = 6;

        [Tooltip("Multi-hit. Each hit is calculated separately, so Strength and Vulnerable apply per hit.")]
        public int Hits = 1;

        public override bool IsPerTarget => true;

        public override void Apply(in EffectContext context)
        {
            if (context.Target == null) return;

            for (int i = 0; i < Hits; i++)
            {
                if (!context.Target.IsAlive) return;
                context.Engine.DealDamage(context.Source, context.Target, Amount, isAttack: true);
            }
        }

        public override string Describe() =>
            Hits > 1 ? $"Deal {Amount} damage {Hits} times." : $"Deal {Amount} damage.";
    }
}
