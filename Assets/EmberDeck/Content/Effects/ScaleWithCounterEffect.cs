using UnityEngine;

namespace EmberDeck.Content.Effects
{
    public enum CombatCounter { CardsPlayedThisTurn, CardsExhaustedThisCombat }

    /// <summary>Cards that pay for tempo (Frenzy) or for attrition (Ash Armor).</summary>
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Scale With Counter", fileName = "Counter_")]
    public sealed class ScaleWithCounterEffect : CardEffect
    {
        public CombatCounter Counter = CombatCounter.CardsPlayedThisTurn;
        public HeatPayout Payout = HeatPayout.Damage;
        public int PerPoint = 3;

        public override bool IsPerTarget => Payout == HeatPayout.Damage;

        public override void Apply(in EffectContext context)
        {
            int count = Counter == CombatCounter.CardsPlayedThisTurn
                ? context.State.CardsPlayedThisTurn
                : context.State.CardsExhaustedThisCombat;

            int value = count * PerPoint;
            if (value <= 0) return;

            if (Payout == HeatPayout.Block)
                context.Engine.GainBlock(context.Source, value);
            else if (context.Target != null)
                context.Engine.DealDamage(context.Source, context.Target, value, isAttack: true);
        }

        public override string Describe()
        {
            string per = Counter == CombatCounter.CardsPlayedThisTurn
                ? "card you played this turn"
                : "card exhausted this combat";
            return Payout == HeatPayout.Block
                ? $"Gain {PerPoint} Block per {per}."
                : $"Deal {PerPoint} damage per {per}.";
        }
    }
}
