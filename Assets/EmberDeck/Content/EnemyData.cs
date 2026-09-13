using System.Collections.Generic;
using UnityEngine;

namespace EmberDeck.Content
{
    public enum MovePattern
    {
        /// <summary>Cycle the move list in order. Fully readable — the player can learn it.</summary>
        Sequence,
        /// <summary>Weighted random, never the same move three turns running.</summary>
        WeightedRandom
    }

    [CreateAssetMenu(menuName = "EmberDeck/Enemy", fileName = "Enemy_")]
    public sealed class EnemyData : ScriptableObject
    {
        public string Id;
        public string DisplayName;

        [Tooltip("Rolled once per combat, inclusive.")]
        public int MinHp = 20;
        public int MaxHp = 24;

        public MovePattern Pattern = MovePattern.Sequence;
        public List<EnemyMove> Moves = new();

        public Color TintColor = new(0.72f, 0.28f, 0.32f);
        public Sprite Art;
    }
}
