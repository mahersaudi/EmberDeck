using EmberDeck.Combat;
using UnityEngine;

namespace EmberDeck.Content.Relics
{
    /// <summary>
    /// "The first attack you play each turn deals +3 damage."
    ///
    /// This exists to prove the hook system end to end. Note what it does NOT require:
    /// no change to CombatEngine, no change to any card, no new event type. A relic that
    /// doubled block, or made every third card free, would plug in exactly the same way —
    /// which is the property that lets relic count grow without the combat code growing.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDeck/Relics/Ember Core", fileName = "Relic_EmberCore")]
    public sealed class EmberCoreRelic : RelicData
    {
        public int BonusDamage = 3;

        public override RelicBehaviour CreateBehaviour() => new Behaviour(BonusDamage);

        sealed class Behaviour : RelicBehaviour
        {
            readonly int _bonus;
            bool _spentThisTurn;

            public Behaviour(int bonus) => _bonus = bonus;

            protected override void OnAttach()
            {
                State.Bus.Subscribe<TurnStartedEvent>(OnTurnStarted);
                State.Bus.Subscribe<DamageCalculation>(OnDamageCalculation);
            }

            protected override void OnDetach()
            {
                State.Bus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
                State.Bus.Unsubscribe<DamageCalculation>(OnDamageCalculation);
            }

            void OnTurnStarted(TurnStartedEvent evt)
            {
                if (evt.IsPlayerTurn) _spentThisTurn = false;
            }

            void OnDamageCalculation(DamageCalculation calculation)
            {
                if (_spentThisTurn || !calculation.IsAttack) return;
                if (calculation.Source != State.Player) return;

                calculation.Amount += _bonus;
                _spentThisTurn = true;
            }
        }
    }
}
