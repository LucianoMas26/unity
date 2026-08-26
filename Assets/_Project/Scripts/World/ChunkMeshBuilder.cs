using Survival.Core;
using UnityEngine;

namespace Survival.World
{
    /// <summary>
    /// Builds one chunk mesh from a seed. Static and stateless: every call is a pure function
    /// of its request, which is what lets the streamer run several at once on worker threads.
    /// Nothing in here touches a Unity native object.
    /// </summary>
    public static class ChunkMeshBuilder
    {
        public static ChunkMeshData Build(in ChunkBuildRequest request, ITerrainHeightSource heightSource)
        {
            // Worker-thread failures are reported far from where they were caused, so say what is
            // actually wrong rather than letting it surface as a bare NullReference.
            if (request.Region == null)
                throw new System.ArgumentException(
                    $"Chunk {request.Coord} was requested without a region snapshot.", nameof(request));

            int res = Mathf.Max(2, request.Resolution);
            int side = res + 1;
            float step = request.ChunkSize / res;

            Vector3 origin = request.Coord.ToWorldOrigin(request.ChunkSize);
            TerrainProfile profile = request.Region.Terrain;
            TerrainPalette palette = request.Region.Palette;

            int surfaceVertexCount = side * side;
            int surfaceIndexCount = res * res * 6;

            var vertices = new Vector3[surfaceVertexCount + 4 * side];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[surfaceIndexCount + 4 * res * 6];

            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;

            // --- Surface vertices -------------------------------------------------------
            // Positions are chunk-local; the chunk GameObject carries the world offset, which
            // keeps float precision usable a long way from the origin.
            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    int index = z * side + x;

                    float localX = x * step;
                    float localZ = z * step;
                    float worldX = origin.x + localX;
                    float worldZ = origin.z + localZ;

                    float height = heightSource.SampleHeight(worldX, worldZ, profile);
                    Vector3 normal = heightSource.SampleNormal(worldX, worldZ, profile);

                    vertices[index] = new Vector3(localX, height, localZ);
                    normals[index] = normal;
                    uvs[index] = new Vector2((float)x / res, (float)z / res);
                    colors[index] = ShadeVertex(height, normal, profile, palette);

                    if (height < minHeight) minHeight = height;
                    if (height > maxHeight) maxHeight = height;
                }
            }

            // --- Surface triangles ------------------------------------------------------
            // Written first, so the collision mesh can be taken as a prefix of this array.
            int t = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int bottomLeft = z * side + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + side;
                    int topRight = topLeft + 1;

                    triangles[t++] = bottomLeft;
                    triangles[t++] = topLeft;
                    triangles[t++] = bottomRight;

                    triangles[t++] = bottomRight;
                    triangles[t++] = topLeft;
                    triangles[t++] = topRight;
                }
            }

            // --- Skirt ------------------------------------------------------------------
            // A curtain hanging below each chunk edge. Neighbouring chunks at different LODs
            // sample the border at different spacings, so their edges do not line up exactly.
            // The skirt fills the resulting hairline cracks without any LOD stitching logic.
            int southBase = surfaceVertexCount;
            int northBase = southBase + side;
            int westBase = southBase + side * 2;
            int eastBase = southBase + side * 3;

            for (int i = 0; i < side; i++)
            {
                CopyAsSkirt(vertices, normals, uvs, colors, i, southBase + i, request.SkirtDepth);
                CopyAsSkirt(vertices, normals, uvs, colors, res * side + i, northBase + i, request.SkirtDepth);
                CopyAsSkirt(vertices, normals, uvs, colors, i * side, westBase + i, request.SkirtDepth);
                CopyAsSkirt(vertices, normals, uvs, colors, i * side + res, eastBase + i, request.SkirtDepth);
            }

            // Winding differs per edge: each quad must read clockwise seen from outside the
            // chunk, and which way "left" points depends on which side you are standing on.
            for (int i = 0; i < res; i++)
            {
                // South edge, outside is -Z, so +X runs left to right.
                AddSkirtQuad(triangles, ref t, i, i + 1, southBase + i, southBase + i + 1);

                // North edge, outside is +Z, so +X runs right to left.
                int north = res * side;
                AddSkirtQuad(triangles, ref t, north + i + 1, north + i, northBase + i + 1, northBase + i);

                // West edge, outside is -X, so +Z runs right to left.
                AddSkirtQuad(triangles, ref t, (i + 1) * side, i * side, westBase + i + 1, westBase + i);

                // East edge, outside is +X, so +Z runs left to right.
                AddSkirtQuad(triangles, ref t, i * side + res, (i + 1) * side + res, eastBase + i, eastBase + i + 1);
            }

            // Bounds are chunk-local and include the skirt, otherwise chunks cull at their edges.
            var centre = new Vector3(request.ChunkSize * 0.5f,
                                     (minHeight + maxHeight) * 0.5f,
                                     request.ChunkSize * 0.5f);
            var size = new Vector3(request.ChunkSize,
                                   Mathf.Max(1f, maxHeight - minHeight) + request.SkirtDepth * 2f,
                                   request.ChunkSize);

            return new ChunkMeshData(request.Coord, request.Lod, vertices, normals, uvs, colors,
                                     triangles, new Bounds(centre, size),
                                     surfaceVertexCount, surfaceIndexCount);
        }

        static void AddSkirtQuad(int[] triangles, ref int t,
                                 int topLeft, int topRight, int bottomLeft, int bottomRight)
        {
            triangles[t++] = topLeft;
            triangles[t++] = topRight;
            triangles[t++] = bottomRight;

            triangles[t++] = topLeft;
            triangles[t++] = bottomRight;
            triangles[t++] = bottomLeft;
        }

        static void CopyAsSkirt(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, Color[] colors,
                                int sourceIndex, int targetIndex, float skirtDepth)
        {
            Vector3 v = vertices[sourceIndex];
            v.y -= skirtDepth;
            vertices[targetIndex] = v;

            // Reusing the surface normal, rather than a sideways one, makes the skirt shade like
            // the ground continuing downward so it never draws attention to itself.
            normals[targetIndex] = normals[sourceIndex];
            uvs[targetIndex] = uvs[sourceIndex];
            colors[targetIndex] = colors[sourceIndex];
        }

        /// <summary>
        /// Placeholder vertex colouring by height and slope. This is the only thing making the
        /// terrain readable before any textures exist, and it costs nothing to throw away later.
        /// </summary>
        static Color ShadeVertex(float height, Vector3 normal, in TerrainProfile profile, in TerrainPalette palette)
        {
            float shoreBlend = Smoothstep(profile.WaterLevel, profile.WaterLevel + 8f, height);
            Color color = Color.Lerp(palette.Shore, palette.Lowland, shoreBlend);

            float highlandBlend = Mathf.InverseLerp(palette.HighlandHeight, palette.PeakHeight, height);
            color = Color.Lerp(color, palette.Highland, highlandBlend);

            float peakBlend = Mathf.InverseLerp(palette.PeakHeight, palette.PeakHeight + 90f, height);
            color = Color.Lerp(color, palette.Peak, peakBlend);

            // Steep ground shows rock whatever its height: that is what reads as a cliff.
            float slope = 1f - Mathf.Clamp01(normal.y);
            float rockBlend = Smoothstep(palette.RockSlope - 0.12f, palette.RockSlope + 0.12f, slope);
            color = Color.Lerp(color, palette.Rock, rockBlend);

            return color;
        }

        static float Smoothstep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
