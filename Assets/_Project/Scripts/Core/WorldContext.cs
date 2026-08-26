using UnityEngine;

namespace Survival.Core
{
    /// <summary>
    /// Everything a generator needs in order to know which world it is building.
    /// Passed down explicitly rather than reached for through a singleton, so generation can
    /// run on worker threads and be tested without a scene.
    /// </summary>
    public sealed class WorldContext
    {
        public uint Seed { get; }
        public IWorldProjection Projection { get; }

        public WorldContext(uint seed, IWorldProjection projection)
        {
            Seed = seed == 0u ? 0x5EED5EEDu : seed;
            Projection = projection;
        }

        /// <summary>The seed a given system should use for a given chunk.</summary>
        public uint SeedFor(ChunkCoord coord, uint salt)
            => DeterministicHash.Hash(DeterministicHash.Salt(Seed, salt), coord.X, coord.Z);

        public SeedStream StreamFor(ChunkCoord coord, uint salt) => new SeedStream(SeedFor(coord, salt));

        /// <summary>
        /// Geo-derived variant: same contract, different source of entropy. This is the switch
        /// that turns the fictional region into a real place without touching any generator.
        /// </summary>
        public uint GeoSeedFor(Vector3 worldPosition, uint salt)
            => Projection.WorldToGeo(worldPosition).ToSeed(DeterministicHash.Salt(Seed, salt));

        public GeoCoordinate ToGeo(Vector3 worldPosition) => Projection.WorldToGeo(worldPosition);
    }
}
