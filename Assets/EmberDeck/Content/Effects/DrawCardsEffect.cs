using UnityEngine;

namespace EmberDeck.Content.Effects
{
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Draw Cards", fileName = "Draw_")]
    public sealed class DrawCardsEffect : CardEffect
    {
        public int Amount = 1;

        public override void Apply(in EffectContext context)
        {
            context.Engine.DrawCards(Amount);
        }

        public override string Describe() => Amount == 1 ? "Draw a card." : $"Draw {Amount} cards.";
    }
}
