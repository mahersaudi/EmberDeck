using System.Collections.Generic;
using UnityEngine;

namespace EmberDeck.Content
{
    /// <summary>Where in a run an encounter can appear.</summary>
    public enum EncounterTier
    {
        /// <summary>Hallway fights on the first rows, while the deck is still the starter deck.</summary>
        Early,
        /// <summary>Hallway fights once the deck has had a few rewards to grow.</summary>
        Late,
        Elite,
        Boss
    }

    /// <summary>
    /// One group of enemies that fights together.
    ///
    /// The unit of variety is the group, not the enemy: an Ash Mite alone is a free kill, three
    /// of them are a test of whether the deck can hit more than one target. Authoring groups
    /// keeps that question deliberate instead of leaving it to a random mix.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDeck/Encounter", fileName = "Encounter_")]
    public sealed class EncounterData : ScriptableObject
    {
        public string Id;
        public EncounterTier Tier;

        [Tooltip("The act whose pools this encounter belongs to.")]
        public int Act = 1;
        public List<EnemyData> Enemies = new();
    }
}
