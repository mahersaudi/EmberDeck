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

        // The multiplier must be in the text. Without it Immolate (1.5x) read as plain "equal to
        // Burn", and its 2x upgrade printed the same sentence — the card lied about its damage.
        public override string Describe()
        {
            string scale = Mathf.Approximately(Multiplier, 1f) ? "" : $"{Multiplier:0.##}x ";
            return $"Deal damage equal to {scale}the target's {Status.DisplayName()}.";
        }
    }
}
