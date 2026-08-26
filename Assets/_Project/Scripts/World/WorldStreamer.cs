using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Survival.Core;
using UnityEngine;

namespace Survival.World
{
    /// <summary>
    /// Keeps the ground around the viewer built, at the right detail, and throws away the rest.
    /// <para>
    /// Chunk meshes are built on worker threads and only uploaded on the main thread, so a
    /// 5x5 km region streams without frame spikes. Nothing about this class assumes the world
    /// is finite: it addresses chunks by integer coordinate and asks a height source for
    /// values, which is the same shape the eventual real-world streamer needs.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldStreamer : MonoBehaviour, ITerrainSampler
    {
        [Header("Configuration")]
        [SerializeField] WorldSettings _settings;

        [Tooltip("Usually the player. Chunks are built around this transform.")]
        [SerializeField] Transform _viewer;

        [SerializeField] Material _terrainMaterial;

        [Header("Debug")]
        [Tooltip("Logs the seed and chunk counts. Useful when checking that a seed reproduces.")]
        [SerializeField] bool _logDiagnostics = true;

        readonly Dictionary<ChunkCoord, TerrainChunk> _active = new Dictionary<ChunkCoord, TerrainChunk>();
        readonly Dictionary<ChunkCoord, ChunkBuildRequest> _inFlight = new Dictionary<ChunkCoord, ChunkBuildRequest>();
        readonly List<ChunkBuildRequest> _queue = new List<ChunkBuildRequest>();
        readonly HashSet<ChunkCoord> _wanted = new HashSet<ChunkCoord>();
        readonly List<ChunkCoord> _scratchCoords = new List<ChunkCoord>();
        readonly Stack<TerrainChunk> _pool = new Stack<TerrainChunk>();
        readonly ConcurrentQueue<CompletedChunk> _completed = new ConcurrentQueue<CompletedChunk>();
        readonly ConcurrentQueue<string> _workerErrors = new ConcurrentQueue<string>();

        CancellationTokenSource _cancellation;
        Transform _chunkRoot;
        ProceduralHeightProvider _heightSource;
        IRegionProvider _regionProvider;
        RegionSnapshot _region;
        ChunkCoord _lastViewerCoord;
        int _runningBuilds;
        int _framesSinceRefresh;
        bool _ready;

        const int RefreshIntervalFrames = 5;

        /// <summary>The seed and projection this world was built from. Null until Awake runs.</summary>
        public WorldContext Context { get; private set; }

        public WorldSettings Settings => _settings;
        public RegionSnapshot CurrentRegion => _region;
        public ITerrainHeightSource HeightSource => _heightSource;
        public int ActiveChunkCount => _active.Count;
        public int PendingChunkCount => _inFlight.Count + _queue.Count;

        readonly struct CompletedChunk
        {
            public readonly ChunkBuildRequest Request;
            public readonly ChunkMeshData Data;

            public CompletedChunk(ChunkBuildRequest request, ChunkMeshData data)
            {
                Request = request;
                Data = data;
            }
        }

        public void SetViewer(Transform viewer)
        {
            _viewer = viewer;
            _framesSinceRefresh = RefreshIntervalFrames;
        }

        void Awake() => Initialise();

        /// <summary>
        /// Builds every piece of runtime state. Safe to call again, and that is the point.
        /// <para>
        /// A script recompile while the game is playing triggers a domain reload: the component
        /// and its serialised fields survive it, but plain C# fields -- the context, the region
        /// snapshot, the height source -- are wiped to null, because Unity cannot serialise them.
        /// Without this being re-runnable the streamer keeps going and quietly queues chunks
        /// built from a null region, which surfaces as a NullReference on a worker thread.
        /// </para>
        /// </summary>
        bool Initialise()
        {
            if (_settings == null)
            {
                Debug.LogError("[WorldStreamer] No WorldSettings assigned. The world cannot be built.", this);
                enabled = false;
                return false;
            }

            uint seed = _settings.RandomiseSeedOnPlay
                ? (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue)
                : _settings.Seed;

            Context = new WorldContext(seed, new FlatWorldProjection(_settings.OriginGeo));

            _region = _settings.Region != null ? _settings.Region.ToSnapshot() : RegionSnapshot.CreateFallback();
            _regionProvider = new SingleRegionProvider(_region);
            _heightSource = new ProceduralHeightProvider(Context, _regionProvider);

            DiscardExistingChunks();

            _chunkRoot = new GameObject("Chunks").transform;
            _chunkRoot.SetParent(transform, false);

            ApplyAtmosphere();

            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = new CancellationTokenSource();

            // The registry is static, so a domain reload empties it without OnEnable running
            // again. Registering here keeps whoever asked for the terrain sampler connected.
            ServiceRegistry.Register<ITerrainSampler>(this);

            _framesSinceRefresh = RefreshIntervalFrames;
            _ready = true;

            if (_logDiagnostics)
                Debug.Log($"[WorldStreamer] Seed {Context.Seed} | region '{_region.DisplayName}' | " +
                          $"origin {Context.Projection.Origin}", this);

            return true;
        }

        /// <summary>
        /// Drops chunks left over from a previous initialisation. After a domain reload the
        /// dictionaries that tracked them are empty, so nothing else would ever release them and
        /// they would sit in the scene forever as orphans.
        /// </summary>
        void DiscardExistingChunks()
        {
            _active.Clear();
            _inFlight.Clear();
            _queue.Clear();
            _wanted.Clear();
            _pool.Clear();
            _runningBuilds = 0;

            while (_completed.TryDequeue(out _)) { }
            while (_workerErrors.TryDequeue(out _)) { }

            if (_chunkRoot == null) return;

            if (Application.isPlaying) Destroy(_chunkRoot.gameObject);
            else DestroyImmediate(_chunkRoot.gameObject);
            _chunkRoot = null;
        }

        /// <summary>Whether what <see cref="Initialise"/> builds is still intact.</summary>
        bool IsRuntimeStateReady =>
            _ready && Context != null && _region != null && _heightSource != null && _chunkRoot != null;

        void OnEnable() => ServiceRegistry.Register<ITerrainSampler>(this);

        void OnDisable() => ServiceRegistry.Unregister<ITerrainSampler>(this);

        void OnDestroy()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
        }

        void Update()
        {
            // Rebuilds itself if a domain reload wiped the non-serialisable half of its state.
            if (!IsRuntimeStateReady && !Initialise()) return;
            if (_viewer == null) return;

            DrainWorkerErrors();

            ChunkCoord viewerCoord = ChunkCoord.FromWorld(_viewer.position, _settings.ChunkSize);
            _framesSinceRefresh++;

            if (viewerCoord != _lastViewerCoord || _framesSinceRefresh >= RefreshIntervalFrames)
            {
                _lastViewerCoord = viewerCoord;
                _framesSinceRefresh = 0;
                RefreshWantedChunks(viewerCoord);
            }

            DispatchQueuedBuilds();
            ApplyCompletedChunks();
        }

        /// <summary>Region fog and ambient. Cheap, and it is most of what sells the mood.</summary>
        void ApplyAtmosphere()
        {
            if (_settings.Region == null) return;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = _settings.Region.FogColor;
            RenderSettings.fogDensity = _settings.Region.FogDensity;
        }

        /// <summary>
        /// Works out which chunks should exist right now, at which LOD, and with or without
        /// collision. A chunk whose LOD or collision need has changed is rebuilt rather than
        /// patched: rebuilding costs one off-thread pass, patching costs correctness.
        /// </summary>
        void RefreshWantedChunks(ChunkCoord viewerCoord)
        {
            int viewDistance = _settings.ViewDistanceInChunks;
            int colliderDistance = _settings.ColliderDistanceInChunks;

            _wanted.Clear();
            _queue.Clear();

            for (int dz = -viewDistance; dz <= viewDistance; dz++)
            {
                for (int dx = -viewDistance; dx <= viewDistance; dx++)
                {
                    var coord = new ChunkCoord(viewerCoord.X + dx, viewerCoord.Z + dz);
                    int ring = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));

                    _wanted.Add(coord);

                    int lod = _settings.LodForRingDistance(ring);
                    bool wantsCollider = ring <= colliderDistance;

                    if (_active.TryGetValue(coord, out TerrainChunk chunk)
                        && chunk.Lod == lod && chunk.HasCollider == wantsCollider)
                        continue;

                    // A build already running for this coord is left alone even if its key is now
                    // stale; the next refresh queues the corrected version once it lands.
                    if (_inFlight.ContainsKey(coord)) continue;

                    _queue.Add(new ChunkBuildRequest(
                        coord, lod, _settings.ChunkSize, _settings.ResolutionForLod(lod),
                        _settings.SkirtDepth, _region, wantsCollider));
                }
            }

            // Sorted farthest-first, because builds are taken from the end of the list: the
            // ground under the player has to exist before the horizon does.
            ChunkCoord centre = viewerCoord;
            _queue.Sort((a, b) => ChunkCoord.ChebyshevDistance(b.Coord, centre)
                                   .CompareTo(ChunkCoord.ChebyshevDistance(a.Coord, centre)));

            ReleaseUnwantedChunks();
        }

        void ReleaseUnwantedChunks()
        {
            _scratchCoords.Clear();
            foreach (KeyValuePair<ChunkCoord, TerrainChunk> entry in _active)
                if (!_wanted.Contains(entry.Key))
                    _scratchCoords.Add(entry.Key);

            for (int i = 0; i < _scratchCoords.Count; i++)
            {
                ChunkCoord coord = _scratchCoords[i];
                TerrainChunk chunk = _active[coord];
                _active.Remove(coord);

                chunk.Release();
                _pool.Push(chunk);
            }
        }

        /// <summary>
        /// Starts build tasks up to the configured concurrency. Building is a pure function of
        /// the request, so several can run at once with no locking.
        /// </summary>
        void DispatchQueuedBuilds()
        {
            CancellationToken token = _cancellation?.Token ?? CancellationToken.None;

            while (_runningBuilds < _settings.MaxConcurrentBuilds && _queue.Count > 0)
            {
                ChunkBuildRequest request = _queue[_queue.Count - 1];
                _queue.RemoveAt(_queue.Count - 1);

                // Re-check the coord is still wanted: the viewer may have moved since sorting.
                if (!_wanted.Contains(request.Coord) || _inFlight.ContainsKey(request.Coord)) continue;

                _inFlight[request.Coord] = request;
                Interlocked.Increment(ref _runningBuilds);

                ITerrainHeightSource heightSource = _heightSource;
                Task.Run(() =>
                {
                    try
                    {
                        if (token.IsCancellationRequested) return;
                        ChunkMeshData data = ChunkMeshBuilder.Build(request, heightSource);
                        _completed.Enqueue(new CompletedChunk(request, data));
                    }
                    catch (Exception exception)
                    {
                        // Never let a worker exception vanish: it would show up only as a chunk
                        // that silently never appears. The empty result also clears the in-flight
                        // entry, so the coord is not blocked from ever being retried.
                        _workerErrors.Enqueue($"Chunk {request.Coord} failed: {exception}");
                        _completed.Enqueue(new CompletedChunk(request, null));
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _runningBuilds);
                    }
                }, token);
            }
        }

        /// <summary>
        /// Uploads finished meshes to the GPU, within a per-frame budget. This is the only part
        /// of chunk creation that has to happen on the main thread, which is why it is the only
        /// part that is rationed.
        /// </summary>
        void ApplyCompletedChunks()
        {
            int budget = _settings.MaxChunkAppliesPerFrame;

            while (budget > 0 && _completed.TryDequeue(out CompletedChunk completed))
            {
                _inFlight.Remove(completed.Request.Coord);

                if (completed.Data == null) continue;                    // build failed, already logged
                if (!_wanted.Contains(completed.Request.Coord)) continue; // viewer moved on

                TerrainChunk chunk = AcquireChunk();
                chunk.Apply(completed.Data, _settings.ChunkSize, completed.Request.WantsCollider);
                _active[completed.Request.Coord] = chunk;

                budget--;
            }
        }

        TerrainChunk AcquireChunk()
        {
            while (_pool.Count > 0)
            {
                TerrainChunk pooled = _pool.Pop();
                if (pooled != null) return pooled;
            }

            return TerrainChunk.Create(_chunkRoot, _terrainMaterial);
        }

        void DrainWorkerErrors()
        {
            while (_workerErrors.TryDequeue(out string message))
                Debug.LogError($"[WorldStreamer] {message}", this);
        }

        // --- ITerrainSampler ------------------------------------------------------------

        /// <summary>
        /// Ground height anywhere, whether or not a chunk exists there. Height is a pure
        /// function of the seed, so spawning and placement never have to wait for streaming.
        /// </summary>
        public float SampleHeight(float worldX, float worldZ)
            => _heightSource?.SampleHeight(worldX, worldZ) ?? 0f;

        public bool HasCollisionAt(Vector3 worldPosition)
        {
            if (!_ready) return false;

            ChunkCoord coord = ChunkCoord.FromWorld(worldPosition, _settings.ChunkSize);
            return _active.TryGetValue(coord, out TerrainChunk chunk) && chunk.HasCollider;
        }

        /// <summary>Centre of the test region, lifted clear of the ground. Where the player starts.</summary>
        public Vector3 GetSpawnPoint(float clearance = 2f)
        {
            Vector3 centre = _settings != null ? _settings.RegionCentre : Vector3.zero;
            centre.y = SampleHeight(centre.x, centre.z) + clearance;
            return centre;
        }

        void OnDrawGizmosSelected()
        {
            if (_settings == null) return;

            // The 5x5 km prototype region, so its extent is visible while tuning.
            float size = _settings.RegionSizeMeters;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(size * 0.5f, 0f, size * 0.5f), new Vector3(size, 1f, size));
        }
    }
}
