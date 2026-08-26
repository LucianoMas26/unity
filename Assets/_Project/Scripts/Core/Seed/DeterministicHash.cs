using System.Runtime.CompilerServices;

namespace Survival.Core
{
    /// <summary>
    /// Integer hashing for everything that must be "random but reproducible".
    /// Deliberately avoids UnityEngine.Random and Mathf.PerlinNoise: both are fine for
    /// effects, but neither guarantees identical output across Unity versions or platforms,
    /// and "same seed = same world" has to survive both.
    /// </summary>
    public static class DeterministicHash
    {
        const uint PrimeX = 73856093u;
        const uint PrimeY = 19349663u;
        const uint PrimeZ = 83492791u;
        const uint PrimeSalt = 2654435761u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix(uint value)
        {
            unchecked
            {
                value ^= 2747636419u;
                value *= 2654435769u;
                value ^= value >> 16;
                value *= 2654435769u;
                value ^= value >> 16;
                value *= 2654435769u;
                return value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash(uint seed, int x)
        {
            unchecked { return Mix(seed ^ ((uint)x * PrimeX)); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash(uint seed, int x, int y)
        {
            unchecked { return Mix(seed ^ ((uint)x * PrimeX) ^ ((uint)y * PrimeY)); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash(uint seed, int x, int y, int z)
        {
            unchecked { return Mix(seed ^ ((uint)x * PrimeX) ^ ((uint)y * PrimeY) ^ ((uint)z * PrimeZ)); }
        }

        /// <summary>Derives an independent sub-seed, so unrelated systems never share a stream.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Salt(uint seed, uint salt)
        {
            unchecked { return Mix(seed ^ (salt * PrimeSalt)); }
        }

        /// <summary>Hash -> [0, 1). Uses the top bits, which mix best.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToUnitFloat(uint hash) => (hash >> 8) * (1f / 16777216f);

        /// <summary>Hash -> [-1, 1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToSignedFloat(uint hash) => ToUnitFloat(hash) * 2f - 1f;
    }

    /// <summary>
    /// Well-known salts, so two systems asking the same chunk for content never collide.
    /// Add new entries here rather than sprinkling magic numbers through generators.
    /// </summary>
    public static class SeedSalt
    {
        public const uint Terrain = 1u;
        public const uint Vegetation = 2u;
        public const uint Buildings = 3u;
        public const uint Caves = 4u;
        public const uint Resources = 5u;
        public const uint Creatures = 6u;
        public const uint Loot = 7u;
        public const uint PointsOfInterest = 8u;
    }
}
