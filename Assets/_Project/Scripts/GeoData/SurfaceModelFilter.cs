using UnityEngine;

namespace Survival.GeoData
{
    /// <summary>
    /// Approximates bare ground from a surface elevation model by removing the buildings.
    /// <para>
    /// Free global elevation data is radar-derived and reflects off rooftops and tree canopy, so
    /// it is a model of the surface, not of the ground. In a city the buildings are inside the
    /// heightmap. Measured on the Rosario extract, the dense centre came out twice as rough as
    /// the open outskirts, when a river plain should be the smoother of the two, and the river
    /// itself was perfectly flat -- exactly the signature of buildings baked into the terrain.
    /// </para>
    /// <para>
    /// Left uncorrected it ruins the scene twice over: the city looks mountainous, and the real
    /// buildings get placed on top of hills that are already those same buildings.
    /// </para>
    /// <para>
    /// The fix is a morphological opening -- a minimum filter followed by a maximum filter.
    /// It erases raised features narrower than the window and leaves wide landforms alone, so
    /// city blocks disappear and the river bluff survives.
    /// </para>
    /// </summary>
    public static class SurfaceModelFilter
    {
        /// <summary>
        /// Returns a copy with building-scale bumps removed.
        /// </summary>
        /// <param name="heights">Square grid, row-major, south to north.</param>
        /// <param name="resolution">Side of the grid.</param>
        /// <param name="radius">Window radius in cells. Must be wider than a city block and
        /// narrower than the landforms worth keeping. On a 39 m grid, 2 removes blocks up to
        /// about 160 m across and leaves a 500 m bluff untouched.</param>
        public static float[] RemoveBuildings(float[] heights, int resolution, int radius)
        {
            if (heights == null || resolution < 3 || radius < 1) return heights;

            float[] eroded = Filter(heights, resolution, radius, takeMinimum: true);
            return Filter(eroded, resolution, radius, takeMinimum: false);
        }

        /// <summary>
        /// Mean absolute step between neighbouring cells. Buildings in the data show up as
        /// roughness, so this is how the filter is checked rather than eyeballed: a dense centre
        /// should end up smoother than the hills around it, not rougher.
        /// </summary>
        public static float MeasureRoughness(float[] heights, int resolution)
        {
            if (heights == null || resolution < 2) return 0f;

            double total = 0d;
            int count = 0;

            for (int z = 1; z < resolution; z++)
            {
                for (int x = 1; x < resolution; x++)
                {
                    float here = heights[z * resolution + x];
                    total += Mathf.Abs(here - heights[z * resolution + x - 1]);
                    total += Mathf.Abs(here - heights[(z - 1) * resolution + x]);
                    count += 2;
                }
            }

            return count > 0 ? (float)(total / count) : 0f;
        }

        /// <summary>
        /// Separable would be faster, but the grids here are 128 square and this runs once at
        /// import. Clarity is worth more than the milliseconds.
        /// </summary>
        static float[] Filter(float[] source, int resolution, int radius, bool takeMinimum)
        {
            var result = new float[source.Length];

            for (int z = 0; z < resolution; z++)
            {
                int z0 = Mathf.Max(0, z - radius);
                int z1 = Mathf.Min(resolution - 1, z + radius);

                for (int x = 0; x < resolution; x++)
                {
                    int x0 = Mathf.Max(0, x - radius);
                    int x1 = Mathf.Min(resolution - 1, x + radius);

                    float best = source[z * resolution + x];
                    for (int zz = z0; zz <= z1; zz++)
                    {
                        for (int xx = x0; xx <= x1; xx++)
                        {
                            float value = source[zz * resolution + xx];
                            if (takeMinimum ? value < best : value > best) best = value;
                        }
                    }

                    result[z * resolution + x] = best;
                }
            }

            return result;
        }
    }
}
