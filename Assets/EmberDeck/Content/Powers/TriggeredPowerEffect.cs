using System.Collections.Generic;
using EmberDeck.Combat;
using UnityEngine;

namespace EmberDeck.Content.Powers
{
    /// <summary>
    /// A Power, expressed as a trigger plus the ordinary effect list every card already uses.
    ///
    /// Sixteen Powers in the set could have been sixteen classes. They are one, because a
    /// Power is only ever "when X happens, do Y" — and Y is already a solved problem. The
    /// result is that a new Power is authored content, not a code change, and the engine
    /// still knows nothing about any of them.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDeck/Effects/Triggered Power", fileName = "Power_")]
    public sealed class TriggeredPowerEffect : CardEffect
    {
        public PowerTrigger Trigger = PowerTrigger.TurnStart;

        [Tooltip("Applied per living enemy instead of once.")]
        public bool PerEnemy;

        public List<CardEffect> Effects = new();

        [TextArea, Tooltip("Card text. Written by hand because a trigger has no self-description.")]
        public string Text = "";

        public override void Apply(in EffectContext context)
        {
            context.Engine.ActivatePower(new Behaviour(this));
        }

        public override string Describe() => Text;

        sealed class Behaviour : PowerBehaviour
        {
            readonly TriggeredPowerEffect _definition;

            /// <summary>
            /// Re-entrancy guard. Ember Engine (Heat -> Block) and Forge Rite (Block -> Heat)
            /// together are an infinite loop, and that pair is a combo the set actively
            /// encourages. One bounce is intended; the second is a hang.
            /// </summary>
            bool _running;

            public Behaviour(TriggeredPowerEffect definition) => _definition = definition;

            protected override void OnAttach()
            {
                switch (_definition.Trigger)
                {
                    case PowerTrigger.TurnStart:
                    case PowerTrigger.TurnEnd:
                        State.Bus.Subscribe<TurnStartedEvent>(OnTurnStarted);
                        State.Bus.Subscribe<TurnEndedEvent>(OnTurnEnded);
                        break;
                    case PowerTrigger.CardPlayed:
                    case PowerTrigger.AttackPlayed:
                        State.Bus.Subscribe<CardPlayedEvent>(OnCardPlayed);
                        break;
                    case PowerTrigger.BlockGained:
                        State.Bus.Subscribe<BlockGainedEvent>(OnBlockGained);
                        break;
                    case PowerTrigger.HeatGained:
                        State.Bus.Subscribe<HeatGainedEvent>(OnHeatGained);
                        break;
                    case PowerTrigger.CardExhausted:
                        State.Bus.Subscribe<CardExhaustedEvent>(OnCardExhausted);
                        break;
                    case PowerTrigger.BurnApplied:
                        State.Bus.Subscribe<StatusAppliedEvent>(OnStatusApplied);
                        break;
                }
            }

            protected override void OnDetach()
            {
                State.Bus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
                State.Bus.Unsubscribe<TurnEndedEvent>(OnTurnEnded);
                State.Bus.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
                State.Bus.Unsubscribe<BlockGainedEvent>(OnBlockGained);
                State.Bus.Unsubscribe<HeatGainedEvent>(OnHeatGained);
                State.Bus.Unsubscribe<CardExhaustedEvent>(OnCardExhausted);
                State.Bus.Unsubscribe<StatusAppliedEvent>(OnStatusApplied);
            }

            void OnTurnStarted(TurnStartedEvent e)
            {
                if (_definition.Trigger == PowerTrigger.TurnStart && e.IsPlayerTurn) Fire();
            }

            void OnTurnEnded(TurnEndedEvent e)
            {
                if (_definition.Trigger == PowerTrigger.TurnEnd && e.IsPlayerTurn) Fire();
            }

            void OnCardPlayed(CardPlayedEvent e)
            {
                if (_definition.Trigger == PowerTrigger.AttackPlayed &&
                    e.Card?.Data.Type != CardType.Attack) return;
                Fire();
            }

            void OnBlockGained(BlockGainedEvent e)
            {
                if (e.Target == State.Player) Fire();
            }

            void OnHeatGained(HeatGainedEvent e) => Fire();

            void OnCardExhausted(CardExhaustedEvent e) => Fire();

            void OnStatusApplied(StatusAppliedEvent e)
            {
                if (e.Status == StatusType.Burn) Fire();
            }

            void Fire()
            {
                if (_running || State.IsOver) return;
                _running = true;
                try
                {
                    if (_definition.PerEnemy)
                    {
                        var targets = new List<Enemy>(State.LivingEnemies());
                        foreach (var enemy in targets)
                            RunEffects(enemy);
                    }
                    else
                    {
                        RunEffects(State.FirstLivingEnemy());
                    }
                }
                finally
                {
                    _running = false;
                }
            }

            void RunEffects(Actor target)
            {
                foreach (var effect in _definition.Effects)
                    effect?.Apply(new EffectContext(State, Engine, State.Player, target, null));
            }
        }
    }
}
