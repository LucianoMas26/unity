using System.Collections.Generic;

namespace Survival.Core
{
    /// <summary>
    /// A deterministic sequence of values from one seed. Content generators take a
    /// SeedStream instead of a System.Random so results never depend on how many
    /// generators ran before them, or in what order.
    /// </summary>
    public struct SeedStream
    {
        uint _state;
        uint _counter;

        public SeedStream(uint seed)
        {
            _state = DeterministicHash.Mix(seed == 0u ? 0x9E3779B9u : seed);
            _counter = 0u;
        }

        /// <summary>The stream a given system owns for a given chunk. This is the entry
        /// point every per-chunk generator should use.</summary>
        public static SeedStream ForChunk(uint worldSeed, ChunkCoord coord, uint salt)
            => new SeedStream(DeterministicHash.Hash(DeterministicHash.Salt(worldSeed, salt), coord.X, coord.Z));

        public uint NextUInt()
        {
            unchecked
            {
                _counter++;
                return DeterministicHash.Mix(_state ^ (_counter * 2654435761u));
            }
        }

        /// <summary>[0, 1)</summary>
        public float NextFloat() => DeterministicHash.ToUnitFloat(NextUInt());

        public float Range(float minInclusive, float maxExclusive)
            => minInclusive + NextFloat() * (maxExclusive - minInclusive);

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public bool Chance(float probability) => NextFloat() < probability;

        public T Pick<T>(IReadOnlyList<T> items)
            => items == null || items.Count == 0 ? default : items[Range(0, items.Count)];

        /// <summary>Fisher-Yates, deterministic for a given stream state.</summary>
        public void Shuffle<T>(IList<T> items)
        {
            if (items == null) return;
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
