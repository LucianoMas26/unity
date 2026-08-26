using Survival.Core;
using UnityEngine;

namespace Survival.World
{
    /// <summary>
    /// Every knob that decides how the world is streamed and how big it is.
    /// Lives as an asset so tuning does not require touching a scene or recompiling.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldSettings", menuName = "Survival/World/World Settings")]
    public sealed class WorldSettings : ScriptableObject
    {
        [Header("Seed")]
        [Tooltip("The whole world derives from this. Same seed = same world, on every machine.")]
        [SerializeField] uint _seed = 20260826u;
        [Tooltip("Roll a fresh seed each time Play is pressed. Handy for judging variety, off for debugging.")]
        [SerializeField] bool _randomiseSeedOnPlay;

        [Header("Region")]
        [SerializeField] RegionDefinition _region;
        [Tooltip("Side of the prototype test region, in metres. 5000 = the agreed 5x5 km patch.")]
        [SerializeField] float _regionSizeMeters = 5000f;

        [Header("Geographic anchor (prepares the real-world plan)")]
        [Tooltip("Latitude the local tangent plane is anchored at. Fictional for now; this is where real coordinates go in later.")]
        [SerializeField] double _originLatitude = -34.6037d;
        [SerializeField] double _originLongitude = -58.3816d;

        [Header("Chunks")]
        [Tooltip("Side of one streamed cell, in metres.")]
        [SerializeField] float _chunkSize = 128f;
        [Tooltip("Quads per chunk side at LOD 0. Must stay divisible by 2^(number of LOD levels - 1).")]
        [SerializeField] int _chunkResolution = 48;
        [Tooltip("How many chunks out from the player stay loaded. 8 x 128 m = roughly 1 km of view.")]
        [SerializeField] int _viewDistanceInChunks = 8;
        [Tooltip("Chunks within this ring get a MeshCollider. Baking collision is expensive, so keep it tight.")]
        [SerializeField] int _colliderDistanceInChunks = 2;

        [Header("LOD")]
        [Tooltip("Ring distance at which each LOD ends. {2,4,7} means LOD0 out to ring 2, LOD1 to 4, LOD2 to 7, LOD3 beyond.")]
        [SerializeField] int[] _lodRingDistances = { 2, 4, 7 };
        [Tooltip("Metres each chunk edge drops below the surface to hide cracks between LOD levels.")]
        [SerializeField] float _skirtDepth = 12f;

        [Header("Budget")]
        [Tooltip("Meshes uploaded to the GPU per frame. Raising this loads faster but can hitch.")]
        [SerializeField] int _maxChunkAppliesPerFrame = 2;
        [Tooltip("Chunk builds allowed to run on worker threads at once.")]
        [SerializeField] int _maxConcurrentBuilds = 4;

        public uint Seed => _seed;
        public bool RandomiseSeedOnPlay => _randomiseSeedOnPlay;
        public RegionDefinition Region => _region;
        public float RegionSizeMeters => _regionSizeMeters;
        public double OriginLatitude => _originLatitude;
        public double OriginLongitude => _originLongitude;
        public float ChunkSize => _chunkSize;
        public int ChunkResolution => _chunkResolution;
        public int ViewDistanceInChunks => _viewDistanceInChunks;
        public int ColliderDistanceInChunks => _colliderDistanceInChunks;
        public float SkirtDepth => _skirtDepth;
        public int MaxChunkAppliesPerFrame => _maxChunkAppliesPerFrame;
        public int MaxConcurrentBuilds => _maxConcurrentBuilds;
        public int LodCount => (_lodRingDistances?.Length ?? 0) + 1;

        public GeoCoordinate OriginGeo => new GeoCoordinate(_originLatitude, _originLongitude);

        /// <summary>Centre of the test region, which is where the player starts.</summary>
        public Vector3 RegionCentre => new Vector3(_regionSizeMeters * 0.5f, 0f, _regionSizeMeters * 0.5f);

        /// <summary>LOD level for a chunk this many rings away from the viewer.</summary>
        public int LodForRingDistance(int ringDistance)
        {
            if (_lodRingDistances == null) return 0;
            for (int i = 0; i < _lodRingDistances.Length; i++)
                if (ringDistance <= _lodRingDistances[i])
                    return i;
            return _lodRingDistances.Length;
        }

        /// <summary>Quads per chunk side at a given LOD. Never drops below 2.</summary>
        public int ResolutionForLod(int lod) => Mathf.Max(2, _chunkResolution >> Mathf.Max(0, lod));

        void OnValidate()
        {
            _chunkSize = Mathf.Max(16f, _chunkSize);
            // Resolution must stay divisible by 2^(LodCount-1), or a coarse LOD would land
            // between quads and its edge would not line up with its neighbours.
            int lodDivisor = 1 << Mathf.Max(0, LodCount - 1);
            _chunkResolution = Mathf.Max(lodDivisor, Mathf.RoundToInt((float)_chunkResolution / lodDivisor) * lodDivisor);
            _chunkResolution = Mathf.Clamp(_chunkResolution, 8, 128);
            _viewDistanceInChunks = Mathf.Clamp(_viewDistanceInChunks, 1, 32);
            _colliderDistanceInChunks = Mathf.Clamp(_colliderDistanceInChunks, 1, _viewDistanceInChunks);
            _maxChunkAppliesPerFrame = Mathf.Clamp(_maxChunkAppliesPerFrame, 1, 16);
            _maxConcurrentBuilds = Mathf.Clamp(_maxConcurrentBuilds, 1, 16);
            _skirtDepth = Mathf.Max(0f, _skirtDepth);
            _regionSizeMeters = Mathf.Max(_chunkSize, _regionSizeMeters);
        }
    }
}
