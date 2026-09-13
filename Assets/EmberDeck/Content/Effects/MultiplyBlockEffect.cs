using UnityEngine;

namespace EmberDeck.Content.Effects
{
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Multiply Block", fileName = "BlockMul_")]
    public sealed class MultiplyBlockEffect : CardEffect
    {
        public int Multiplier = 2;

        public override void Apply(in EffectContext context)
        {
            var actor = context.Source;
            if (actor == null || actor.Block <= 0) return;

            // Routed through GainBlock so Dexterity and any listening Power see it, rather
            // than writing Block directly and silently skipping every hook.
            context.Engine.GainBlock(actor, actor.Block * (Multiplier - 1));
        }

        public override string Describe() => Multiplier == 2 ? "Double your Block." : $"Multiply your Block by {Multiplier}.";
    }
}
