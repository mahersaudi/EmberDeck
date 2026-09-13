using System;
using System.Collections.Generic;
using EmberDeck.Content.Relics;
using UnityEngine;

namespace EmberDeck.Content
{
    /// <summary>
    /// Everything the vertical slice needs to start a combat, as one asset. Tuning lives in
    /// data rather than in code so balance changes never need a recompile — and so the
    /// balance simulator can sweep values without touching a single script.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDeck/Run Config", fileName = "RunConfig")]
    public sealed class RunConfig : ScriptableObject
    {
        [Serializable]
        public sealed class DeckEntry
        {
            public CardData Card;
            [Min(1)] public int Count = 1;
        }

        [Header("Player")]
        public string PlayerName = "Ember";
        public int MaxHp = 72;
        public int EnergyPerTurn = 3;
        public int CardsPerTurn = 5;

        [Header("Starting deck")]
        public List<DeckEntry> StarterDeck = new();

        [Header("Starting relics")]
        public List<RelicData> Relics = new();

        [Header("Encounter — the enemies of each fight")]
        public List<EnemyData> Encounter = new();

        [Header("Elite and boss encounters")]
        public List<EnemyData> EliteEncounter = new();
        public List<EnemyData> BossEncounter = new();

        [Tooltip("Fraction of max HP restored at a Rest node.")]
        public float RestHealFraction = 0.3f;

        [Header("Reward pool — every card that can be offered after a victory")]
        public List<CardData> RewardPool = new();

        [Tooltip("Every card in the game. A save file stores ids; this resolves them back.")]
        public List<CardData> AllCards = new();

        public CardData FindCard(string id)
        {
            foreach (var card in AllCards)
                if (card != null && card.Id == id) return card;
            return null;
        }

        [Tooltip("Enemy HP gained per fight beyond the first, as a fraction.")]
        public float EnemyScalingPerFight = 0.18f;

        public List<CardInstance> BuildDeck()
        {
            var deck = new List<CardInstance>();
            foreach (var entry in StarterDeck)
            {
                if (entry?.Card == null) continue;
                for (int i = 0; i < entry.Count; i++)
                    deck.Add(new CardInstance(entry.Card));
            }
            return deck;
        }
    }
}
