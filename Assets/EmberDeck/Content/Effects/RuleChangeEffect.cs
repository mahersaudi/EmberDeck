using UnityEngine;

namespace EmberDeck.Content.Effects
{
    /// <summary>
    /// Powers that change a rule rather than react to an event: Molten Armor, Slow Roast,
    /// Thermal Mass. They need no subscription at all — the flags live on CombatState and
    /// die with it, so there is nothing to unsubscribe and nothing to leak.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Rule Change", fileName = "Rule_")]
    public sealed class RuleChangeEffect : CardEffect
    {
        public bool MakeBlockPersist;
        public bool StopBurnDecaying;
        public int OverheatThresholdDelta;

        [TextArea] public string Text = "";

        public override void Apply(in EffectContext context)
        {
            if (MakeBlockPersist) context.State.BlockPersists = true;
            if (StopBurnDecaying) context.State.BurnDecays = false;
            context.State.OverheatThreshold += OverheatThresholdDelta;
        }

        public override string Describe() => Text;
    }
}
