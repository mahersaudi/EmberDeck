namespace EmberDeck.View
{
    /// <summary>
    /// How long the fight takes to show itself. Every combat timing reads through here.
    ///
    /// The game resolves a card or an enemy turn instantly; what the player sees is a replay, and its
    /// speed is a choice. Normal is slow on purpose — a card is lifted, thrown and lands, an enemy
    /// lunges before its hit arrives, a hand is dealt one card at a time — because the first playtest
    /// said the fight felt weak, and it felt weak because it was over before it could be seen. Fast
    /// halves all of it, for the player on their fortieth run who has seen it all.
    /// </summary>
    public static class Pace
    {
        /// <summary>Multiplies every combat timing: 1 at Normal, 0.5 at Fast.</summary>
        public static float Scale => Settings.FastAnimations ? 0.5f : 1f;

        public static float S(float seconds) => seconds * Scale;

        // ── A card played ──
        /// <summary>The card lifts out of the hand and draws back before it is thrown.</summary>
        public static float CardWindup => S(0.22f);
        /// <summary>The throw, accelerating into its target.</summary>
        public static float CardFlight => S(0.30f);
        /// <summary>From the click to the moment a card's effects land.</summary>
        public static float CardImpact => CardWindup + CardFlight;
        /// <summary>Between the hits of one multi-hit card, and between the targets of an area card.</summary>
        public static float HitGap => S(0.16f);

        // ── An enemy acting ──
        /// <summary>The lunge before an enemy's blow lands.</summary>
        public static float EnemyLunge => S(0.26f);
        /// <summary>From one enemy's action to the next.</summary>
        public static float EnemyActionGap => S(0.85f);

        // ── A hand dealt ──
        /// <summary>Between one card leaving the draw pile and the next.</summary>
        public static float DealInterval => S(0.17f);
        /// <summary>One card's flight from the pile to its place.</summary>
        public static float DealFlight => S(0.45f);
        /// <summary>Between cards swept into the discard pile at the end of a turn.</summary>
        public static float DiscardInterval => S(0.06f);
    }
}
