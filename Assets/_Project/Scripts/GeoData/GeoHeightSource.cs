using Survival.Core;
using Survival.World;
using UnityEngine;

namespace Survival.GeoData
{
    /// <summary>
    /// Ground height read from a real elevation grid instead of noise.
    /// <para>
    /// Plain C# holding nothing but arrays and numbers, because chunk meshes are built on worker
    /// threads and this is what they call. It is also the whole point of the exercise: the
    /// mesher, the LOD and the streaming never learn that the input changed.
    /// </para>
    /// <para>
    /// The data is far coarser than the mesh -- roughly 39 m between samples against 2.7 m
    /// between vertices at LOD 0 -- so bilinear interpolation would give visibly faceted ground.
    /// A little fractal noise is added below the data resolution to break that up. It invents
    /// detail, but only detail the data never had, and it never moves the real shape.
    /// </para>
    /// </summary>
    public sealed class GeoHeightSource : ITerrainHeightSource
    {
        readonly float[] _heights;
        readonly int _resolution;
        readonly float _sizeX;
        readonly float _sizeZ;
        readonly TerrainProfile _profile;
        readonly uint _detailSeed;
        readonly float _detailAmplitude;

        public float SampleSpacingX { get; }
        public float SampleSpacingZ { get; }

        public GeoHeightSource(float[] heights, int resolution, float sizeX, float sizeZ,
                               in TerrainProfile profile, uint detailSeed, float detailAmplitude)
        {
            _heights = heights;
            _resolution = Mathf.Max(2, resolution);
            _sizeX = Mathf.Max(1f, sizeX);
            _sizeZ = Mathf.Max(1f, sizeZ);
            _profile = profile;
            _detailSeed = detailSeed;
            _detailAmplitude = detailAmplitude;

            SampleSpacingX = _sizeX / (_resolution - 1);
            SampleSpacingZ = _sizeZ / (_resolution - 1);
        }

        public float SampleHeight(float worldX, float worldZ) => SampleHeight(worldX, worldZ, _profile);

        public float SampleHeight(float worldX, float worldZ, in TerrainProfile profile)
        {
            float height = SampleGrid(worldX, worldZ);

            if (_detailAmplitude > 0f)
                height += GradientNoise.Fbm(_detailSeed, worldX, worldZ, profile.DetailNoise) * _detailAmplitude;

            return height;
        }

        /// <summary>
        /// Bilinear lookup, clamped at the edges. Clamping means the world does not fall off a
        /// cliff at the boundary: it continues as a flat plain, which is honest about there
        /// being no data rather than inventing a wall.
        /// </summary>
        float SampleGrid(float worldX, float worldZ)
        {
            if (_heights == null || _heights.Length < _resolution * _resolution) return 0f;

            float u = Mathf.Clamp(worldX / _sizeX, 0f, 1f) * (_resolution - 1);
            float v = Mathf.Clamp(worldZ / _sizeZ, 0f, 1f) * (_resolution - 1);

            int x0 = Mathf.Clamp((int)u, 0, _resolution - 2);
            int z0 = Mathf.Clamp((int)v, 0, _resolution - 2);
            int x1 = x0 + 1;
            int z1 = z0 + 1;

            float tx = u - x0;
            float tz = v - z0;

            // Rows run west to east, and are stored south to north, matching the download order.
            float h00 = _heights[z0 * _resolution + x0];
            float h10 = _heights[z0 * _resolution + x1];
            float h01 = _heights[z1 * _resolution + x0];
            float h11 = _heights[z1 * _resolution + x1];

            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
        }
    }
}
