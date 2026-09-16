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

        [Tooltip("Encounter pools for a run. The fixed lists above remain for one-off fights.")]
        public List<EncounterData> Encounters = new();

        [Tooltip("Acts in a run. Each has its own map, encounter pools and boss; beating the last boss wins.")]
        public int Acts = 2;

        [Tooltip("Shown on the map, one per act.")]
        public List<string> ActNames = new();

        public string ActName(int act) =>
            act >= 1 && act <= ActNames.Count && !string.IsNullOrEmpty(ActNames[act - 1]) ? ActNames[act - 1] : $"Act {act}";

        /// <summary>The boss waiting at the top of an act's map, for its name and portrait.</summary>
        public EnemyData BossOf(int act)
        {
            foreach (var encounter in Encounters)
                if (encounter != null && encounter.Tier == EncounterTier.Boss && encounter.Act == act && encounter.Enemies.Count > 0)
                    return encounter.Enemies[0];
            return null;
        }

        [Tooltip("Hallway fights on rows below this draw from the Early pool; the rest from Late.")]
        public int EarlyRows = 3;

        [Tooltip("Fraction of max HP restored at a Rest node.")]
        public float RestHealFraction = 0.3f;

        [Header("Reward pool — every card that can be offered after a victory")]
        public List<CardData> RewardPool = new();

        [Tooltip("Every card in the game. A save file stores ids; this resolves them back.")]
        public List<CardData> AllCards = new();

        [Tooltip("Relics an elite victory can grant. Starting relics are not in this pool.")]
        public List<RelicData> RelicPool = new();

        [Tooltip("The unlock track. Its cards and relics are in no pool above until a profile unlocks them.")]
        public List<UnlockData> Unlocks = new();

        /// <summary>The reward pool with the given unlocks' cards added.</summary>
        public List<CardData> RewardPoolFor(ICollection<string> unlocked)
        {
            var pool = new List<CardData>(RewardPool);
            if (unlocked == null) return pool;
            foreach (var unlock in Unlocks)
                if (unlock != null && unlocked.Contains(unlock.Id))
                    foreach (var card in unlock.Cards)
                        if (card != null) pool.Add(card);
            return pool;
        }

        /// <summary>The relic pool with the given unlocks' relics added.</summary>
        public List<RelicData> RelicPoolFor(ICollection<string> unlocked)
        {
            var pool = new List<RelicData>(RelicPool);
            if (unlocked == null) return pool;
            foreach (var unlock in Unlocks)
                if (unlock != null && unlocked.Contains(unlock.Id))
                    foreach (var relic in unlock.Relics)
                        if (relic != null) pool.Add(relic);
            return pool;
        }

        public List<string> AllUnlockIds()
        {
            var ids = new List<string>();
            foreach (var unlock in Unlocks)
                if (unlock != null) ids.Add(unlock.Id);
            return ids;
        }

        [Tooltip("Potions that fights drop and shops sell.")]
        public List<PotionData> PotionPool = new();

        public CardData FindCard(string id)
        {
            foreach (var card in AllCards)
                if (card != null && card.Id == id) return card;
            return null;
        }

        public PotionData FindPotion(string id)
        {
            foreach (var potion in PotionPool)
                if (potion != null && potion.Id == id) return potion;
            return null;
        }

        public RelicData FindRelic(string id)
        {
            foreach (var relic in Relics)
                if (relic != null && relic.Id == id) return relic;
            foreach (var relic in RelicPool)
                if (relic != null && relic.Id == id) return relic;
            foreach (var unlock in Unlocks)
                if (unlock != null)
                    foreach (var relic in unlock.Relics)
                        if (relic != null && relic.Id == id) return relic;
            return null;
        }

        [Tooltip("Enemy HP gained per fight beyond the first, as a fraction.")]
        public float EnemyScalingPerFight = 0.18f;

        [Tooltip("Per-act overrides of EnemyScalingPerFight, first entry for Act 1. Missing entries use it.")]
        public List<float> ActScalingPerFight = new();

        [Tooltip("Multiplies every enemy's rolled HP. One knob for the whole bestiary; see docs/deck-building.md.")]
        public float EnemyHpFactor = 1f;

        public float ScalingPerFight(int act) =>
            act >= 1 && act <= ActScalingPerFight.Count ? ActScalingPerFight[act - 1] : EnemyScalingPerFight;

        [Tooltip("Gold a run starts with: enough for one card or one removal at the first shop.")]
        public int StartingGold = 99;

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
