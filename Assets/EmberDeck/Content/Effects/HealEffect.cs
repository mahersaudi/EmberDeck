using UnityEngine;

namespace EmberDeck.Content.Effects
{
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Heal", fileName = "Heal_")]
    public sealed class HealEffect : CardEffect
    {
        public int Amount = 4;

        public override void Apply(in EffectContext context)
        {
            context.Source?.Heal(Amount);
        }

        public override string Describe() => $"Heal {Amount} HP.";
    }
}
