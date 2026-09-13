using UnityEngine;

namespace EmberDeck.Content.Effects
{
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Gain Energy", fileName = "Energy_")]
    public sealed class GainEnergyEffect : CardEffect
    {
        public int Amount = 1;

        public override void Apply(in EffectContext context)
        {
            context.State.Energy += Amount;
        }

        public override string Describe() => $"Gain {Amount} Energy.";
    }
}
