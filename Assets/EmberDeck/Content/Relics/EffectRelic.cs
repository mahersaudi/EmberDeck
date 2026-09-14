using System.Collections.Generic;
using EmberDeck.Combat;
using UnityEngine;

namespace EmberDeck.Content.Relics
{
    /// <summary>
    /// A relic built from the ordinary effect list: its effects are applied once as each combat
    /// begins.
    ///
    /// A relic is a Power the player already owns when the fight starts. Treating it that way
    /// means a relic that grants Block every turn is a TriggeredPowerEffect, one that raises the
    /// overheat threshold is a RuleChangeEffect, and a new relic is authored content rather
    /// than another subclass with its own event wiring.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDeck/Relics/Effect Relic", fileName = "Relic_")]
    public sealed class EffectRelic : RelicData
    {
        [Tooltip("Applied once as combat begins. Powers activated here last the whole combat.")]
        public List<CardEffect> Effects = new();

        public override RelicBehaviour CreateBehaviour() => new Behaviour(this);

        sealed class Behaviour : RelicBehaviour
        {
            readonly EffectRelic _definition;

            public Behaviour(EffectRelic definition) => _definition = definition;

            protected override void OnAttach()
            {
                foreach (var effect in _definition.Effects)
                {
                    if (effect == null) continue;

                    // Same rule a card aimed at every enemy follows: effects that act on a
                    // target repeat per enemy, everything else happens once.
                    if (effect.IsPerTarget)
                    {
                        foreach (var enemy in new List<Enemy>(State.LivingEnemies()))
                            effect.Apply(new EffectContext(State, Engine, State.Player, enemy, null));
                    }
                    else
                    {
                        effect.Apply(new EffectContext(State, Engine, State.Player, State.FirstLivingEnemy(), null));
                    }
                }
            }

            // Nothing to undo here. Powers this relic activated are registered with the combat
            // and detached by CombatSession.End along with every other Power.
            protected override void OnDetach() { }
        }
    }
}
