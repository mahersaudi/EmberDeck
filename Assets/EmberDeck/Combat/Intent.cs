using EmberDeck.Content;

namespace EmberDeck.Combat
{
    /// <summary>
    /// What an enemy has announced for next turn, already resolved through the same
    /// modifiers the real hit will use. Showing a number the actual attack then contradicts
    /// is worse than showing no number at all, so the preview is computed by the engine
    /// rather than read off the move asset.
    /// </summary>
    public struct Intent
    {
        public EnemyMove Move;
        public IntentKind Kind;
        /// <summary>Damage per hit after Strength/Weak — or block amount. 0 when not numeric.</summary>
        public int Value;
        public int Hits;

        public bool HasValue => Value > 0;

        public string Label => Move != null ? Move.Label : "...";
    }
}
