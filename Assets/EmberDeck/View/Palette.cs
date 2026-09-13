using UnityEngine;

namespace EmberDeck.View
{
    /// <summary>
    /// One place for every colour. Defined once so the slice reads as a designed thing
    /// rather than an assortment of debug rectangles — and so a restyle is one file.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Background   = new(0.07f, 0.07f, 0.10f, 1f);
        public static readonly Color PanelDark    = new(0.12f, 0.12f, 0.16f, 0.95f);
        public static readonly Color PanelRaised  = new(0.17f, 0.17f, 0.22f, 1f);
        public static readonly Color Ink          = new(0.93f, 0.92f, 0.88f, 1f);
        public static readonly Color InkMuted     = new(0.62f, 0.62f, 0.68f, 1f);

        public static readonly Color Health       = new(0.78f, 0.25f, 0.28f, 1f);
        public static readonly Color HealthTrack  = new(0.24f, 0.10f, 0.12f, 1f);
        public static readonly Color Block        = new(0.38f, 0.62f, 0.86f, 1f);
        public static readonly Color Energy       = new(0.95f, 0.68f, 0.25f, 1f);

        public static readonly Color IntentAttack = new(0.92f, 0.42f, 0.35f, 1f);
        public static readonly Color IntentBlock  = new(0.45f, 0.70f, 0.90f, 1f);
        public static readonly Color IntentBuff   = new(0.85f, 0.72f, 0.35f, 1f);

        public static readonly Color CardIdle     = new(0.20f, 0.19f, 0.24f, 1f);
        public static readonly Color CardSelected = new(0.31f, 0.29f, 0.38f, 1f);
        public static readonly Color CardDisabled = new(0.13f, 0.13f, 0.16f, 1f);

        public static readonly Color Victory      = new(0.35f, 0.72f, 0.45f, 1f);
        public static readonly Color Defeat       = new(0.72f, 0.28f, 0.30f, 1f);
    }
}
