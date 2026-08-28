using Survival.Core;
using UnityEngine;

namespace Survival.World
{
    /// <summary>
    /// An asset that can supply ground height, so where the terrain comes from becomes a thing
    /// you drop into a field rather than a branch in the streamer.
    /// <para>
    /// This is the seam the real-world plan hangs on. <see cref="WorldStreamer"/> falls back to
    /// noise when nothing is assigned; assigning an elevation-backed asset swaps the input to the
    /// whole pipeline without the mesher, the LOD or the streaming knowing anything changed.
    /// </para>
    /// <para>
    /// Declared here rather than in the module that implements it, so World stays the thing
    /// everything else depends on and never depends back.
    /// </para>
    /// </summary>
    public abstract class TerrainHeightSourceAsset : ScriptableObject
    {
        /// <summary>
        /// Builds the runtime source. Called on the main thread; whatever comes back is read from
        /// worker threads, so it must be plain C# holding no Unity objects.
        /// </summary>
        public abstract ITerrainHeightSource CreateSource(WorldContext context, IRegionProvider regions);

        /// <summary>Where a player should start. Real data has real ground, so the default of
        /// "the middle of the region" is not always sensible.</summary>
        public virtual Vector2 GetPreferredSpawnXZ(float regionSizeMeters)
            => new Vector2(regionSizeMeters * 0.5f, regionSizeMeters * 0.5f);
    }
}
