using System.Collections.Generic;
using UnityEngine;

namespace EmberDeck.Content
{
    /// <summary>
    /// The immutable definition of a card. Runtime state (upgrades, temporary cost changes)
    /// lives on CardInstance, never here — a ScriptableObject is shared by every copy in the
    /// deck, and mutating one in play would silently edit the asset on disk in the Editor.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDeck/Card", fileName = "Card_")]
    public sealed class CardData : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        public CardType Type = CardType.Attack;
        public CardRarity Rarity = CardRarity.Common;

        [Header("Rules")]
        public int Cost = 1;
        public TargetMode Target = TargetMode.SingleEnemy;

        [Tooltip("Removed from the deck for the rest of the combat once played.")]
        public bool Exhaust;

        [Header("Effects — applied in order")]
        public List<CardEffect> Effects = new();

        [Header("Presentation")]
        public Color TintColor = new(0.85f, 0.45f, 0.25f);
        public Sprite Art;

        [Header("Upgrade")]
        [Tooltip("The upgraded version of this card. Null on a card that is already upgraded.")]
        public CardData Upgrade;
        public bool IsUpgraded;

        /// <summary>Built from the effects themselves, so text can never contradict behaviour.</summary>
        public string BuildDescription()
        {
            var parts = new List<string>(Effects.Count + 1);
            foreach (var effect in Effects)
            {
                if (effect == null) continue;
                var text = effect.Describe();
                if (!string.IsNullOrEmpty(text)) parts.Add(text);
            }
            if (Exhaust) parts.Add("Exhaust.");
            return string.Join(" ", parts);
        }
    }
}
