using System.Collections.Generic;
using UnityEngine;

namespace EmberDeck.Content
{
    public enum IntentKind { Attack, Block, Buff, Debuff }

    /// <summary>
    /// One action an enemy can announce and then perform. It reuses CardEffect, so an
    /// enemy move and a player card are the same machinery — which means a mechanic only
    /// ever has to be written once, and any card effect is automatically available to
    /// enemy design.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDeck/Enemy Move", fileName = "Move_")]
    public sealed class EnemyMove : ScriptableObject
    {
        public string Label = "Attack";
        public IntentKind Kind = IntentKind.Attack;

        [Tooltip("Relative likelihood when the enemy picks moves at random.")]
        public int Weight = 1;

        public List<CardEffect> Effects = new();
    }
}
