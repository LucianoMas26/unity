using Survival.Core;
using UnityEngine;

namespace Survival.World
{
    /// <summary>
    /// One streamed cell of ground in the scene. Owns its meshes and destroys them explicitly:
    /// meshes built at runtime are not garbage collected on their own, and a streaming world
    /// creates thousands of them over a session.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TerrainChunk : MonoBehaviour
    {
        MeshFilter _filter;
        MeshRenderer _renderer;
        MeshCollider _collider;
        Mesh _mesh;
        Mesh _collisionMesh;

        public ChunkCoord Coord { get; private set; }
        public int Lod { get; private set; } = -1;
        public bool HasCollider { get; private set; }

        public static TerrainChunk Create(Transform parent, Material material)
        {
            var go = new GameObject("Chunk");
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;

            var chunk = go.AddComponent<TerrainChunk>();
            chunk.CacheComponents();
            chunk._renderer.sharedMaterial = material;
            return chunk;
        }

        void CacheComponents()
        {
            if (_filter != null) return;
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
        }

        public void Apply(ChunkMeshData data, float chunkSize, bool wantsCollider)
        {
            CacheComponents();

            Coord = data.Coord;
            Lod = data.Lod;

            transform.localPosition = data.Coord.ToWorldOrigin(chunkSize);
            gameObject.name = $"Chunk {data.Coord} LOD{data.Lod}";

            DestroyMeshes();

            _mesh = data.ToMesh();
            _filter.sharedMesh = _mesh;

            if (wantsCollider)
            {
                _collisionMesh = data.ToCollisionMesh();
                if (_collider == null) _collider = gameObject.AddComponent<MeshCollider>();
                _collider.sharedMesh = _collisionMesh;
                _collider.enabled = true;
            }
            else if (_collider != null)
            {
                _collider.sharedMesh = null;
                _collider.enabled = false;
            }

            HasCollider = wantsCollider;
            gameObject.SetActive(true);
        }

        /// <summary>Returns the chunk to a reusable, empty state. Does not destroy the GameObject.</summary>
        public void Release()
        {
            CacheComponents();

            if (_collider != null)
            {
                _collider.sharedMesh = null;
                _collider.enabled = false;
            }

            _filter.sharedMesh = null;
            DestroyMeshes();

            HasCollider = false;
            Lod = -1;
            gameObject.SetActive(false);
        }

        void DestroyMeshes()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
                _mesh = null;
            }

            if (_collisionMesh != null)
            {
                if (Application.isPlaying) Destroy(_collisionMesh); else DestroyImmediate(_collisionMesh);
                _collisionMesh = null;
            }
        }

        void OnDestroy() => DestroyMeshes();
    }
}
