namespace EmberDeck.Content
{
    public enum CardType { Attack, Skill, Power }

    public enum CardRarity { Starter, Common, Uncommon, Rare }

    public enum TargetMode
    {
        /// <summary>No target picked — effects resolve on the player or globally.</summary>
        Self,
        /// <summary>Player picks one enemy.</summary>
        SingleEnemy,
        /// <summary>Hits every living enemy.</summary>
        AllEnemies
    }
}
