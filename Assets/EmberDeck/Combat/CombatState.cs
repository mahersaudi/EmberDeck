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

        /// <summary>
        /// The set's second currency. Unlike Energy it accumulates across turns, and past
        /// OverheatThreshold it costs HP at end of turn — so every turn asks whether to bank
        /// it or spend it.
        /// </summary>
        public int Heat;
        public int OverheatThreshold = 10;

        /// <summary>Reset each turn. Read by cards that scale with tempo (Frenzy).</summary>
        public int CardsPlayedThisTurn;

        /// <summary>Never reset during a combat. Read by Ash Armor.</summary>
        public int CardsExhaustedThisCombat;

        // Rule switches owned by Powers rather than by the engine, so the engine needs no
        // knowledge of which card turned them on.
        public bool BlockPersists;      // Molten Armor
        public bool BurnDecays = true;  // Slow Roast sets this false

        /// <summary>
        /// The Emberheart's rule: starting a turn overheated gives 1 extra Energy. Set by the session
        /// from the act, not by a card, so it belongs to the place rather than to the deck.
        /// </summary>
        public bool OverheatFeeds;

        public readonly List<CardInstance> DrawPile = new();
        public readonly List<CardInstance> Hand = new();
        public readonly List<CardInstance> DiscardPile = new();
        public readonly List<CardInstance> ExhaustPile = new();

        /// <summary>Powers played this combat. The session detaches every one at the end —
        /// a Power that outlives its combat keeps modifying the next one.</summary>
        public readonly List<Content.Powers.PowerBehaviour> ActivePowers = new();

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
