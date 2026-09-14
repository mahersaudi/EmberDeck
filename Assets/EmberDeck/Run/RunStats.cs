using System;

namespace EmberDeck.Run
{
    /// <summary>
    /// Numbers about a run, for the end-of-run screen.
    ///
    /// Informational only: nothing reads them to make a decision, so the simulator neither needs
    /// nor tracks them, and they cannot change how a run plays. Public fields because JsonUtility
    /// serialises fields, and these travel inside the run save — a run resumed after a restart
    /// ends with the numbers of the whole run, not just the part after the restart.
    /// </summary>
    [Serializable]
    public sealed class RunStats
    {
        public int FightsWon;
        public int ElitesWon;
        public int EnemiesDefeated;
        public int DamageDealt;
        public int DamageTaken;
        public int BiggestHit;
        public int CardsPlayed;
        public int Turns;
        public int CardsAdded;
        public int CardsUpgraded;
        public int CardsRemoved;
        public int GoldEarned;
        public int GoldSpent;
        public int PotionsUsed;
        public float Seconds;

        /// <summary>The enemies of the most recent fight — the ones that ended the run, when it was lost.</summary>
        public string FinalEncounter = "";
    }
}
