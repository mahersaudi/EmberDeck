using UnityEngine;

namespace EmberDeck.Content.Relics
{
    /// <summary>
    /// A permanent passive. Relics are the second axis of the genre's combinatorics: cards
    /// multiply against each other, and relics multiply against every card at once.
    ///
    /// The asset is the definition; RelicBehaviour is the per-combat instance. They are
    /// separate because a relic like "the first attack each turn" needs state, and a
    /// ScriptableObject is shared by every run — mutating it would leak between runs and,
    /// in the Editor, write the leak to disk.
    /// </summary>
    public abstract class RelicData : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;

        public abstract RelicBehaviour CreateBehaviour();
    }
}
