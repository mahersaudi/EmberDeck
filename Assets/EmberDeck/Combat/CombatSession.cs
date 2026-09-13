using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Content.Relics;
using EmberDeck.Core;

namespace EmberDeck.Combat
{
    /// <summary>
    /// Builds a combat from a RunConfig and a seed, wires the relics, and tears everything
    /// down afterwards. Deliberately not a MonoBehaviour: the simulator constructs thousands
    /// of these per second with no scene loaded.
    /// </summary>
    public sealed class CombatSession
    {
        public readonly CombatState State = new();
        public readonly CombatEngine Engine;
        public readonly RunConfig Config;

        readonly List<RelicBehaviour> _relics = new();

        public CombatSession(RunConfig config, int seed)
        {
            Config = config;
            State.Rng = new RunRng(seed);
            State.EnergyPerTurn = config.EnergyPerTurn;
            State.CardsPerTurn = config.CardsPerTurn;
            State.Player = new Actor(config.PlayerName, config.MaxHp, isPlayer: true);
            Engine = new CombatEngine(State);
        }

        public void Begin()
        {
            foreach (var enemyData in Config.Encounter)
            {
                if (enemyData == null) continue;
                int hp = State.Rng.Enemies.Range(enemyData.MinHp, enemyData.MaxHp + 1);
                State.Enemies.Add(new Enemy(enemyData, hp));
            }

            // Relics attach BEFORE the first turn begins: one that grants block or draws a
            // card on turn 1 has to be listening before StartCombat fires that turn.
            foreach (var relicData in Config.Relics)
            {
                if (relicData == null) continue;
                var behaviour = relicData.CreateBehaviour();
                behaviour.Attach(relicData, State, Engine);
                _relics.Add(behaviour);
            }

            Engine.StartCombat(Config.BuildDeck());
        }

        public void End()
        {
            foreach (var relic in _relics)
                relic.Detach();
            _relics.Clear();
            State.Bus.Clear();
        }
    }
}
