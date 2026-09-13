using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Core;

namespace EmberDeck.Combat
{
    /// <summary>
    /// The entire combat, as plain C#. No MonoBehaviour, no UnityEngine types in the rules.
    ///
    /// This is what makes the balance simulator possible: ten thousand combats can be run
    /// headlessly in a second because nothing here needs a scene, a frame, or a renderer.
    /// A model that can only run inside Play mode is a model you cannot tune.
    /// </summary>
    public sealed class CombatState
    {
        public Actor Player;
        public readonly List<Enemy> Enemies = new();

        public int Energy;
        public int EnergyPerTurn = 3;
        public int CardsPerTurn = 5;
        public int TurnNumber;

        public readonly List<CardInstance> DrawPile = new();
        public readonly List<CardInstance> Hand = new();
        public readonly List<CardInstance> DiscardPile = new();
        public readonly List<CardInstance> ExhaustPile = new();

        public readonly EventBus Bus = new();
        public RunRng Rng;

        public bool IsOver;
        public bool PlayerWon;

        public IEnumerable<Enemy> LivingEnemies()
        {
            foreach (var enemy in Enemies)
                if (enemy.IsAlive) yield return enemy;
        }

        public bool AnyEnemyAlive()
        {
            foreach (var enemy in Enemies)
                if (enemy.IsAlive) return true;
            return false;
        }

        public Enemy FirstLivingEnemy()
        {
            foreach (var enemy in Enemies)
                if (enemy.IsAlive) return enemy;
            return null;
        }
    }
}
