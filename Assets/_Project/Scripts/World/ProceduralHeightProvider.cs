using Survival.Core;
using UnityEngine;

namespace Survival.World
{
    /// <summary>
    /// Turns a seed and a region profile into ground height. Pure function of its inputs:
    /// no state, no Unity object access, safe to call from any number of worker threads.
    /// <para>
    /// Height is layered rather than one big noise call, because each layer is a separate
    /// design lever: continents decide where the land rolls, the mask decides where mountain
    /// ranges are allowed to exist at all, ridged noise carves their crests, detail roughens
    /// the surface at walking scale.
    /// </para>
    /// </summary>
    public sealed class ProceduralHeightProvider : ITerrainHeightSource
    {
        readonly uint _terrainSeed;
        readonly IRegionProvider _regions;

        public ProceduralHeightProvider(WorldContext context, IRegionProvider regions)
        {
            _terrainSeed = DeterministicHash.Salt(context.Seed, SeedSalt.Terrain);
            _regions = regions;
        }

        public float SampleHeight(float worldX, float worldZ)
        {
            RegionSnapshot region = _regions.GetRegion(worldX, worldZ);
            return SampleHeight(worldX, worldZ, region.Terrain);
        }

        public float SampleHeight(float worldX, float worldZ, in TerrainProfile profile)
        {
            float height = profile.BaseElevation;

            // Broad rolling shape.
            height += GradientNoise.Fbm(_terrainSeed, worldX, worldZ, profile.ContinentNoise)
                      * profile.ContinentAmplitude;

            // Mountains only where the mask opens up, faded in smoothly so ranges have foothills
            // instead of a hard edge where the threshold is crossed.
            uint maskSeed = DeterministicHash.Salt(_terrainSeed, 101u);
            float mask = GradientNoise.Fbm01(maskSeed, worldX, worldZ, profile.MountainMaskNoise);
            float mountainWeight = Mathf.InverseLerp(profile.MountainMaskThreshold,
                                                     Mathf.Min(1f, profile.MountainMaskThreshold + 0.25f),
                                                     mask);
            if (mountainWeight > 0f)
            {
                mountainWeight = mountainWeight * mountainWeight * (3f - 2f * mountainWeight); // smoothstep
                uint ridgeSeed = DeterministicHash.Salt(_terrainSeed, 202u);
                float ridge = GradientNoise.Fbm01(ridgeSeed, worldX, worldZ, profile.MountainNoise);
                height += ridge * profile.MountainAmplitude * mountainWeight;
            }

            // Walking-scale roughness.
            uint detailSeed = DeterministicHash.Salt(_terrainSeed, 303u);
            height += GradientNoise.Fbm(detailSeed, worldX, worldZ, profile.DetailNoise)
                      * profile.DetailAmplitude;

            return height;
        }

    }
}
