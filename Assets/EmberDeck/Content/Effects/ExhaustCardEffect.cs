using UnityEngine;

namespace EmberDeck.Content.Effects
{
    public enum ExhaustSource { TopOfDrawPile, RandomFromHand }

    [CreateAssetMenu(menuName = "EmberDeck/Effects/Exhaust Card", fileName = "Exhaust_")]
    public sealed class ExhaustCardEffect : CardEffect
    {
        public ExhaustSource Source = ExhaustSource.TopOfDrawPile;
        public int Count = 1;

        public override void Apply(in EffectContext context)
        {
            for (int i = 0; i < Count; i++)
            {
                if (Source == ExhaustSource.TopOfDrawPile)
                {
                    context.Engine.ExhaustTopOfDraw();
                    continue;
                }

                var hand = context.State.Hand;
                if (hand.Count == 0) return;
                // Shuffle stream, not the enemy stream: which card burns is a deck event.
                context.Engine.ExhaustCard(hand[context.State.Rng.Shuffle.Range(0, hand.Count)]);
            }
        }

        public override string Describe()
        {
            string what = Source == ExhaustSource.TopOfDrawPile
                ? "the top card of your draw pile"
                : "a random card from your hand";
            return Count == 1 ? $"Exhaust {what}." : $"Exhaust {what}, {Count} times.";
        }
    }
}
