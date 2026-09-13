namespace EmberDeck.Combat
{
    /// <summary>
    /// Deliberately small. Five statuses that each answer a different question produce far
    /// more interesting decisions than twenty that overlap — and every new card can be read
    /// against a vocabulary the player already knows.
    /// </summary>
    public enum StatusType
    {
        /// <summary>+N damage on every attack. Lasts the whole combat.</summary>
        Strength,

        /// <summary>+N block whenever block is gained. Lasts the whole combat.</summary>
        Dexterity,

        /// <summary>Takes +50% attack damage. Ticks down by 1 at end of the owner's turn.</summary>
        Vulnerable,

        /// <summary>Deals -25% attack damage. Ticks down by 1 at end of the owner's turn.</summary>
        Weak,

        /// <summary>Loses N HP at end of the owner's turn, then N drops by 1.</summary>
        Poison
    }

    public static class StatusTypeExtensions
    {
        /// <summary>Intensity statuses persist; duration statuses tick down each turn.</summary>
        public static bool IsDuration(this StatusType type) =>
            type is StatusType.Vulnerable or StatusType.Weak;

        public static string DisplayName(this StatusType type) => type switch
        {
            StatusType.Strength   => "Strength",
            StatusType.Dexterity  => "Dexterity",
            StatusType.Vulnerable => "Vulnerable",
            StatusType.Weak       => "Weak",
            StatusType.Poison     => "Poison",
            _ => type.ToString()
        };
    }
}
