using System.Collections.Generic;
using UnityEngine;

namespace EmberDeck.Content
{
    /// <summary>
    /// A single-use effect carried between fights and used at any moment of the player's turn,
    /// for no energy.
    ///
    /// Potions are the run's emergency lever: the answer to the fight that went wrong, or the push
    /// that makes an elite winnable. They are built from the same CardEffect assets as cards, so a
    /// potion needs no rule of its own — "heal 15" is the heal effect, used without a card.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDeck/Potion", fileName = "Potion_")]
    public sealed class PotionData : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public TargetMode Target = TargetMode.Self;
        public List<CardEffect> Effects = new();

        [Tooltip("Icon id under Resources/Icons. The colour tells the player what kind of potion it is.")]
        public string Icon = "potion_red";

        public string BuildDescription()
        {
            var parts = new List<string>(Effects.Count + 1);
            foreach (var effect in Effects)
            {
                if (effect == null) continue;
                var text = effect.Describe();
                if (!string.IsNullOrEmpty(text)) parts.Add(text);
            }
            if (Target == TargetMode.AllEnemies) parts.Add("Hits ALL enemies.");
            return string.Join(" ", parts);
        }
    }
}
