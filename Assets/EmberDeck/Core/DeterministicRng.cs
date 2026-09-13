using System.Collections.Generic;

namespace EmberDeck.Core
{
    /// <summary>
    /// Seeded random source. Gameplay must never use UnityEngine.Random: it is one global
    /// stream shared with VFX, UI and anything else, so a single extra call desynchronises
    /// a run. That makes seeds unshareable and bugs unreproducible — both of which this
    /// genre depends on.
    /// </summary>
    public sealed class DeterministicRng
    {
        readonly System.Random _random;

        public int Seed { get; }

        public DeterministicRng(int seed)
        {
            Seed = seed;
            _random = new System.Random(seed);
        }

        public int Range(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

        public float Value01() => (float)_random.NextDouble();

        public bool Chance(float probability) => _random.NextDouble() < probability;

        public T Pick<T>(IReadOnlyList<T> items) => items[Range(0, items.Count)];

        /// <summary>Fisher-Yates, in place.</summary>
        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
