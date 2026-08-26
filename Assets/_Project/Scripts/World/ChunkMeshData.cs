using Survival.Core;
using UnityEngine;

namespace Survival.World
{
    /// <summary>
    /// Everything needed to build one chunk, snapshotted on the main thread.
    /// Contains no Unity objects, so a worker thread can read it safely.
    /// </summary>
    public readonly struct ChunkBuildRequest
    {
        public readonly ChunkCoord Coord;
        public readonly int Lod;
        public readonly float ChunkSize;
        public readonly int Resolution;
        public readonly float SkirtDepth;
        public readonly RegionSnapshot Region;

        /// <summary>Part of the build key: a chunk that gains or loses collision is rebuilt
        /// rather than kept in memory, because holding every chunk's arrays alive costs far
        /// more than regenerating the handful near the player.</summary>
        public readonly bool WantsCollider;

        public ChunkBuildRequest(ChunkCoord coord, int lod, float chunkSize, int resolution,
                                 float skirtDepth, RegionSnapshot region, bool wantsCollider)
        {
            Coord = coord;
            Lod = lod;
            ChunkSize = chunkSize;
            Resolution = resolution;
            SkirtDepth = skirtDepth;
            Region = region;
            WantsCollider = wantsCollider;
        }
    }

    /// <summary>
    /// A finished chunk mesh in plain arrays. Built off the main thread; converted into an
    /// actual <see cref="Mesh"/> only when the streamer applies it, because Mesh is a native
    /// object and may not be touched from a worker.
    /// </summary>
    public sealed class ChunkMeshData
    {
        public ChunkCoord Coord { get; }
        public int Lod { get; }
        public Vector3[] Vertices { get; }
        public Vector3[] Normals { get; }
        public Vector2[] Uvs { get; }
        public Color[] Colors { get; }
        public int[] Triangles { get; }
        public Bounds Bounds { get; }

        /// <summary>Surface-only vertex count, excluding the skirt. Used to build the collider
        /// mesh, which must not include skirt geometry or the player catches on invisible walls.</summary>
        public int SurfaceVertexCount { get; }
        public int SurfaceTriangleIndexCount { get; }

        public ChunkMeshData(ChunkCoord coord, int lod, Vector3[] vertices, Vector3[] normals,
                             Vector2[] uvs, Color[] colors, int[] triangles, Bounds bounds,
                             int surfaceVertexCount, int surfaceTriangleIndexCount)
        {
            Coord = coord;
            Lod = lod;
            Vertices = vertices;
            Normals = normals;
            Uvs = uvs;
            Colors = colors;
            Triangles = triangles;
            Bounds = bounds;
            SurfaceVertexCount = surfaceVertexCount;
            SurfaceTriangleIndexCount = surfaceTriangleIndexCount;
        }

        /// <summary>Main thread only.</summary>
        public Mesh ToMesh()
        {
            var mesh = new Mesh { name = $"Chunk_{Coord.X}_{Coord.Z}_LOD{Lod}" };
            if (Vertices.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = Vertices;
            mesh.normals = Normals;
            mesh.uv = Uvs;
            mesh.colors = Colors;
            mesh.triangles = Triangles;
            mesh.bounds = Bounds;
            return mesh;
        }

        /// <summary>
        /// Collision mesh: the visible surface without the skirt, and without normals, uvs or
        /// colours, none of which physics reads.
        /// </summary>
        public Mesh ToCollisionMesh()
        {
            var vertices = new Vector3[SurfaceVertexCount];
            System.Array.Copy(Vertices, vertices, SurfaceVertexCount);

            var triangles = new int[SurfaceTriangleIndexCount];
            System.Array.Copy(Triangles, triangles, SurfaceTriangleIndexCount);

            var mesh = new Mesh { name = $"ChunkCollision_{Coord.X}_{Coord.Z}" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.bounds = Bounds;
            return mesh;
        }
    }
}
