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

        [Header("Encounter — the enemies of the slice's single fight")]
        public List<EnemyData> Encounter = new();

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
