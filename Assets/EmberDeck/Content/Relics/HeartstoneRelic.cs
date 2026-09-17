using EmberDeck.Combat;
using UnityEngine;

namespace EmberDeck.Content.Relics
{
    /// <summary>
    /// "While you are overheated, your attacks deal 3 more damage."
    ///
    /// The Emberheart's relic, found only there (MinAct 3). It is the other half of the act's rule:
    /// the Heart's Fire pays Energy for starting a turn over the threshold, and this pays damage for
    /// staying there — so a deck that commits to living overheated has two reasons to, and the HP it
    /// loses at every turn's end is the whole of the price.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDeck/Relics/Heartstone", fileName = "Relic_Heartstone")]
    public sealed class HeartstoneRelic : RelicData
    {
        public int BonusDamage = 3;

        public override RelicBehaviour CreateBehaviour() => new Behaviour(BonusDamage);

        sealed class Behaviour : RelicBehaviour
        {
            readonly int _bonus;

            public Behaviour(int bonus) => _bonus = bonus;

            protected override void OnAttach() => State.Bus.Subscribe<DamageCalculation>(OnDamageCalculation);

            protected override void OnDetach() => State.Bus.Unsubscribe<DamageCalculation>(OnDamageCalculation);

            void OnDamageCalculation(DamageCalculation calculation)
            {
                if (!calculation.IsAttack || calculation.Source != State.Player) return;
                if (State.Heat <= State.OverheatThreshold) return;
                calculation.Amount += _bonus;
            }
        }
    }
}
