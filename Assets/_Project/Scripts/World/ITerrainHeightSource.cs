using UnityEngine;

namespace Survival.World
{
    /// <summary>
    /// Where ground height comes from. The prototype uses noise
    /// (<see cref="ProceduralHeightProvider"/>); the long-term plan is an implementation backed
    /// by real elevation tiles, blended with noise below the data resolution. Chunk building
    /// only ever talks to this, so that swap touches one class.
    /// </summary>
    public interface ITerrainHeightSource
    {
        float SampleHeight(float worldX, float worldZ);
        float SampleHeight(float worldX, float worldZ, in TerrainProfile profile);
    }

    public static class TerrainHeightSourceExtensions
    {
        /// <summary>
        /// Surface normal from central differences at a fixed epsilon. Fixed rather than
        /// LOD-dependent on purpose: a vertex on a chunk border then gets the identical normal
        /// from both sides and at every LOD, which is what kills shading seams.
        /// </summary>
        public static Vector3 SampleNormal(this ITerrainHeightSource source,
                                           float worldX, float worldZ,
                                           in TerrainProfile profile,
                                           float epsilon = 1f)
        {
            float left = source.SampleHeight(worldX - epsilon, worldZ, profile);
            float right = source.SampleHeight(worldX + epsilon, worldZ, profile);
            float down = source.SampleHeight(worldX, worldZ - epsilon, profile);
            float up = source.SampleHeight(worldX, worldZ + epsilon, profile);

            return new Vector3(left - right, 2f * epsilon, down - up).normalized;
        }
    }
}
