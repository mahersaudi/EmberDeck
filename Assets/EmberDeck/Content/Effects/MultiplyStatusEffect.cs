using EmberDeck.Combat;
using UnityEngine;

namespace EmberDeck.Content.Effects
{
    /// <summary>
    /// The Pyre payoff. Pyre scales by multiplying what is already there rather than adding
    /// more, which is why its cards reward setting up a turn early instead of spamming.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Multiply Status", fileName = "StatusMul_")]
    public sealed class MultiplyStatusEffect : CardEffect
    {
        public StatusType Status = StatusType.Burn;
        public int Multiplier = 2;
        public bool AllEnemies;

        public override bool IsPerTarget => !AllEnemies;

        public override void Apply(in EffectContext context)
        {
            if (AllEnemies)
            {
                foreach (var enemy in new System.Collections.Generic.List<Enemy>(context.State.LivingEnemies()))
                    Multiply(context, enemy);
            }
            else
            {
                Multiply(context, context.Target);
            }
        }

        void Multiply(in EffectContext context, Actor target)
        {
            if (target == null) return;
            int current = target.GetStatus(Status);
            if (current <= 0) return;
            context.Engine.ApplyStatus(target, Status, current * (Multiplier - 1));
        }

        public override string Describe()
        {
            string scope = AllEnemies ? "ALL enemies" : "a target";
            return Multiplier == 2
                ? $"Double the {Status.DisplayName()} on {scope}."
                : $"Multiply the {Status.DisplayName()} on {scope} by {Multiplier}.";
        }
    }
}
