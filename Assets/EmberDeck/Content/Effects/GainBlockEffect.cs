using UnityEngine;

namespace EmberDeck.Content.Effects
{
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Gain Block", fileName = "Block_")]
    public sealed class GainBlockEffect : CardEffect
    {
        public int Amount = 5;

        public override void Apply(in EffectContext context)
        {
            context.Engine.GainBlock(context.Source, Amount);
        }

        public override string Describe() => $"Gain {Amount} Block.";
    }
}
