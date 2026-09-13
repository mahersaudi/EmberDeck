namespace EmberDeck.Content.Powers
{
    /// <summary>
    /// When a Power fires. Deliberately a small, closed list: every trigger here maps to an
    /// event the engine already publishes, so adding a Power never means touching the engine.
    /// </summary>
    public enum PowerTrigger
    {
        TurnStart,
        TurnEnd,
        CardPlayed,
        AttackPlayed,
        BlockGained,
        HeatGained,
        CardExhausted,
        BurnApplied
    }
}
