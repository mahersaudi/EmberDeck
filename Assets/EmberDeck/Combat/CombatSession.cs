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

        /// <summary>Null for a one-off fight (the balance simulator); set during a run.</summary>
        public readonly Run.RunState Run;

        readonly List<RelicBehaviour> _relics = new();

        public CombatSession(RunConfig config, int seed, Run.RunState run = null)
        {
            Config = config;
            Run = run;
            State.Rng = new RunRng(seed);
            State.EnergyPerTurn = config.EnergyPerTurn;
            State.CardsPerTurn = config.CardsPerTurn;

            int maxHp = run?.MaxHp ?? config.MaxHp;
            State.Player = new Actor(config.PlayerName, maxHp, isPlayer: true);
            // Damage carries between fights. That is most of what makes a run a run: a fight
            // won at 8 HP changes every decision in the next one.
            if (run != null) State.Player.Hp = run.Hp;

            Engine = new CombatEngine(State);
        }

        public void Begin()
        {
            // Enemies grow with the fight number so a deck that got stronger still meets
            // resistance. Scaling HP rather than damage keeps the intent numbers — which the
            // player plans around — honest.
            // The boss is exempt. Its numbers are authored as the end of the run; multiplying
            // them by the run's fight count as well put a 150 HP boss at about 250 by the time
            // anyone reached it, and 74 of 74 simulated runs that arrived there died.
            float scale = Run != null && Run.IsBoss
                ? 1f
                : 1f + Config.EnemyScalingPerFight * ((Run?.FightNumber ?? 1) - 1);

            var encounter = Config.Encounter;
            if (Run != null && Run.IsBoss && Config.BossEncounter.Count > 0) encounter = Config.BossEncounter;
            else if (Run != null && Run.IsElite && Config.EliteEncounter.Count > 0) encounter = Config.EliteEncounter;

            foreach (var enemyData in encounter)
            {
                if (enemyData == null) continue;
                int rolled = State.Rng.Enemies.Range(enemyData.MinHp, enemyData.MaxHp + 1);
                State.Enemies.Add(new Enemy(enemyData, UnityEngine.Mathf.RoundToInt(rolled * scale)));
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

            var deck = new List<CardInstance>();
            if (Run != null)
                foreach (var card in Run.Deck) deck.Add(new CardInstance(card));
            else
                deck = Config.BuildDeck();

            Engine.StartCombat(deck);
        }

        public void End()
        {
            foreach (var relic in _relics)
                relic.Detach();
            _relics.Clear();

            foreach (var power in State.ActivePowers)
                power.Detach();
            State.ActivePowers.Clear();

            State.Bus.Clear();
        }
    }
}
