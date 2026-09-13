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

        [Header("Per hit")]
        [Tooltip("Applied after every individual hit. Must be per-hit rather than once at the " +
                 "end: Searing Blade pays per application, so collapsing four 1-Burn hits into " +
                 "one 4-Burn application would quietly turn +4 Strength into +1.")]
        public StatusType PerHitStatus = StatusType.Burn;
        public int PerHitStatusAmount;

        public override bool IsPerTarget => true;

        public override void Apply(in EffectContext context)
        {
            if (context.Target == null) return;

            for (int i = 0; i < Hits; i++)
            {
                if (!context.Target.IsAlive) return;
                context.Engine.DealDamage(context.Source, context.Target, Amount, isAttack: true);

                if (PerHitStatusAmount > 0)
                    context.Engine.ApplyStatus(context.Target, PerHitStatus, PerHitStatusAmount);
            }
        }

        public override string Describe()
        {
            string hit = Hits > 1 ? $"Deal {Amount} damage {Hits} times." : $"Deal {Amount} damage.";
            if (PerHitStatusAmount <= 0) return hit;
            return $"{hit} Each hit applies {PerHitStatusAmount} {PerHitStatus.DisplayName()}.";
        }
    }
}
