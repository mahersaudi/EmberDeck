namespace EmberDeck.Core
{
    /// <summary>
    /// Independent RNG streams derived from a single run seed.
    ///
    /// Splitting them is what actually makes a seed reproducible. If shuffling and enemy
    /// decisions shared one stream, drawing a single extra card would change every enemy
    /// decision for the rest of the run — so a replay would diverge from the original and
    /// "seed of the day" would be meaningless.
    /// </summary>
    public sealed class RunRng
    {
        public int MasterSeed { get; }

        public DeterministicRng Shuffle { get; }
        public DeterministicRng Enemies { get; }
        public DeterministicRng Rewards { get; }
        public DeterministicRng Map { get; }

        public RunRng(int masterSeed)
        {
            MasterSeed = masterSeed;
            Shuffle  = new DeterministicRng(masterSeed ^ 0x5F3A1B);
            Enemies  = new DeterministicRng(masterSeed ^ 0x2C9E77);
            Rewards  = new DeterministicRng(masterSeed ^ 0x71D40F);
            Map      = new DeterministicRng(masterSeed ^ 0x13A5C2);
        }
    }
}
