using EmberDeck.Combat;
using UnityEngine;

namespace EmberDeck.Content.Effects
{
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Apply Status", fileName = "Status_")]
    public sealed class ApplyStatusEffect : CardEffect
    {
        public StatusType Status = StatusType.Vulnerable;
        public int Amount = 2;

        [Tooltip("Apply to whoever played the card instead of the chosen target.")]
        public bool ApplyToSelf;

        public override bool IsPerTarget => !ApplyToSelf;

        public override void Apply(in EffectContext context)
        {
            var target = ApplyToSelf ? context.Source : context.Target;
            if (target == null) return;
            context.Engine.ApplyStatus(target, Status, Amount);
        }

        public override string Describe()
        {
            var who = ApplyToSelf ? "yourself" : "the target";
            return $"Apply {Amount} {Status.DisplayName()} to {who}.";
        }
    }
}
