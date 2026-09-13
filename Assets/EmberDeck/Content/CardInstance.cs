namespace EmberDeck.Content
{
    /// <summary>
    /// One physical copy of a card in a run. Two Strikes are two instances sharing one
    /// CardData, which is what lets a single copy be upgraded or discounted without
    /// affecting the other.
    /// </summary>
    public sealed class CardInstance
    {
        static int _nextUid;

        /// <summary>Stable within a run — used by views to track a card across shuffles.</summary>
        public readonly int Uid;
        public readonly CardData Data;

        /// <summary>Cost override for this copy this combat; -1 means "use the definition".</summary>
        public int CostOverride = -1;

        public CardInstance(CardData data)
        {
            Data = data;
            Uid = ++_nextUid;
        }

        public int BaseCost => CostOverride >= 0 ? CostOverride : Data.Cost;

        public override string ToString() => $"{Data.DisplayName}#{Uid}";
    }
}
