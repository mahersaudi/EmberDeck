using UnityEngine;

namespace EmberDeck.Content.Effects
{
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Gain Heat", fileName = "Heat_")]
    public sealed class GainHeatEffect : CardEffect
    {
        public int Amount = 2;

        public override void Apply(in EffectContext context) => context.Engine.GainHeat(Amount);

        public override string Describe() => $"Gain {Amount} Heat.";
    }
}
