using EmberDeck.Combat;
using UnityEngine;

namespace EmberDeck.Content.Effects
{
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Damage From Status", fileName = "StatusDamage_")]
    public sealed class DamageFromStatusEffect : CardEffect
    {
        public StatusType Status = StatusType.Burn;
        public float Multiplier = 1f;

        public override bool IsPerTarget => true;

        public override void Apply(in EffectContext context)
        {
            if (context.Target == null) return;
            int damage = Mathf.FloorToInt(context.Target.GetStatus(Status) * Multiplier);
            if (damage <= 0) return;
            context.Engine.DealDamage(context.Source, context.Target, damage, isAttack: true);
        }

        public override string Describe() =>
            $"Deal damage equal to the target's {Status.DisplayName()}.";
    }
}
