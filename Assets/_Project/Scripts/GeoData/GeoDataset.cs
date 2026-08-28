using Survival.Core;
using Survival.World;
using UnityEngine;

namespace Survival.GeoData
{
    /// <summary>
    /// A real place, cached as an asset: an elevation grid plus the OpenStreetMap features that
    /// sit on it, already projected to local metres.
    /// <para>
    /// Downloaded once by an editor tool and stored here on purpose. The game never touches the
    /// network, works offline, stays deterministic, and does not hammer two free public APIs
    /// every time somebody presses Play.
    /// </para>
    /// <para>
    /// Nothing in this type is Rosario-specific. Point the importer at another bounding box and
    /// you get another asset, which is the difference between a demo and a pipeline.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "GeoDataset_", menuName = "Survival/Geo/Dataset")]
    public sealed class GeoDataset : TerrainHeightSourceAsset
    {
        [Header("Identity")]
        [SerializeField] string _displayName = "Region real";

        [Header("Anchor")]
        [Tooltip("South-west corner of the bounding box. World XZ (0,0) sits exactly here.")]
        [SerializeField] double _originLatitude;
        [SerializeField] double _originLongitude;
        [SerializeField] float _sizeX = 5000f;
        [SerializeField] float _sizeZ = 5000f;

        [Header("Elevation")]
        [SerializeField] int _heightResolution;
        [SerializeField] float[] _heights;
        [SerializeField] float _minHeight;
        [SerializeField] float _maxHeight;

        [Tooltip("Metres of invented roughness added below the data's own resolution, to stop " +
                 "the interpolated grid reading as flat facets. Keep it small: it must not " +
                 "compete with the real relief.")]
        [SerializeField] float _detailAmplitude = 0f;

        [Header("Features")]
        [SerializeField] GeoBuilding[] _buildings = new GeoBuilding[0];
        [SerializeField] GeoWay[] _roads = new GeoWay[0];
        [SerializeField] GeoArea[] _water = new GeoArea[0];
        [SerializeField] GeoArea[] _parks = new GeoArea[0];

        [Tooltip("Shared pool every footprint and polyline indexes into. Flattened because Unity " +
                 "cannot serialise arrays of arrays.")]
        [SerializeField] Vector2[] _points = new Vector2[0];

        public string DisplayName => _displayName;
        public GeoCoordinate Origin => new GeoCoordinate(_originLatitude, _originLongitude);
        public float SizeX => _sizeX;
        public float SizeZ => _sizeZ;
        public float MinHeight => _minHeight;
        public float MaxHeight => _maxHeight;
        public int HeightResolution => _heightResolution;

        public GeoBuilding[] Buildings => _buildings;
        public GeoWay[] Roads => _roads;
        public GeoArea[] Water => _water;
        public GeoArea[] Parks => _parks;
        public Vector2[] Points => _points;

        public bool HasElevation => _heights != null && _heights.Length >= 4;

        public override ITerrainHeightSource CreateSource(WorldContext context, IRegionProvider regions)
        {
            TerrainProfile profile = regions != null
                ? regions.GetRegion(_sizeX * 0.5f, _sizeZ * 0.5f).Terrain
                : TerrainProfile.Default;

            if (!HasElevation)
            {
                Debug.LogError($"[GeoDataset] '{name}' has no elevation data. Falling back to noise.");
                return new ProceduralHeightProvider(context, regions);
            }

            return new GeoHeightSource(
                _heights, _heightResolution, _sizeX, _sizeZ, profile,
                DeterministicHash.Salt(context.Seed, SeedSalt.Terrain), _detailAmplitude);
        }

        /// <summary>
        /// A spot in the open, as near the middle of the region as possible.
        /// <para>
        /// The middle of a real city centre is almost certainly inside a building, and a box is
        /// invisible from within because its faces point outwards -- so spawning there shows you
        /// terrain through walls you cannot see, and looks exactly like nothing loaded. Standing
        /// in a street is both guaranteed to be outside a footprint and the most legible place to
        /// arrive in a real place.
        /// </para>
        /// </summary>
        public override Vector2 GetPreferredSpawnXZ(float regionSizeMeters)
        {
            var centre = new Vector2(_sizeX * 0.5f, _sizeZ * 0.5f);
            if (_roads == null || _roads.Length == 0 || _points == null) return centre;

            // Gather the nearest handful of street vertices in one pass, then test only those
            // against the footprints. Testing every street point against every building would be
            // hundreds of millions of comparisons for no benefit.
            const int Candidates = 64;
            var best = new (float Distance, Vector2 Point)[Candidates];
            for (int i = 0; i < Candidates; i++) best[i] = (float.MaxValue, centre);

            foreach (GeoWay road in _roads)
            {
                for (int i = road.PointStart; i < road.PointStart + road.PointCount; i++)
                {
                    if (i < 0 || i >= _points.Length) continue;

                    float distance = (_points[i] - centre).sqrMagnitude;
                    if (distance >= best[Candidates - 1].Distance) continue;

                    int slot = Candidates - 1;
                    while (slot > 0 && best[slot - 1].Distance > distance)
                    {
                        best[slot] = best[slot - 1];
                        slot--;
                    }
                    best[slot] = (distance, _points[i]);
                }
            }

            foreach ((float distance, Vector2 point) in best)
            {
                if (distance == float.MaxValue) break;
                if (!IsInsideAnyBuilding(point)) return point;
            }

            return centre;
        }

        /// <summary>Point-in-oriented-box against every footprint. Only ever called a few dozen
        /// times, so the brute force costs nothing worth optimising away.</summary>
        public bool IsInsideAnyBuilding(Vector2 localXZ)
        {
            if (_buildings == null) return false;

            foreach (GeoBuilding building in _buildings)
            {
                Vector2 offset = localXZ - building.Centre;
                float radians = -building.RotationDegrees * Mathf.Deg2Rad;
                float cos = Mathf.Cos(radians);
                float sin = Mathf.Sin(radians);

                float x = offset.x * cos - offset.y * sin;
                float y = offset.x * sin + offset.y * cos;

                if (Mathf.Abs(x) <= building.Size.x * 0.5f && Mathf.Abs(y) <= building.Size.y * 0.5f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Height straight from the grid, no detail noise. For editor-time placement, where the
        /// exact surface matters less than not needing a WorldContext to ask.
        /// </summary>
        public float SampleElevation(float worldX, float worldZ)
        {
            if (!HasElevation) return 0f;

            int resolution = Mathf.Max(2, _heightResolution);
            float u = Mathf.Clamp(worldX / Mathf.Max(1f, _sizeX), 0f, 1f) * (resolution - 1);
            float v = Mathf.Clamp(worldZ / Mathf.Max(1f, _sizeZ), 0f, 1f) * (resolution - 1);

            int x0 = Mathf.Clamp((int)u, 0, resolution - 2);
            int z0 = Mathf.Clamp((int)v, 0, resolution - 2);
            float tx = u - x0;
            float tz = v - z0;

            float h00 = _heights[z0 * resolution + x0];
            float h10 = _heights[z0 * resolution + x0 + 1];
            float h01 = _heights[(z0 + 1) * resolution + x0];
            float h11 = _heights[(z0 + 1) * resolution + x0 + 1];

            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
        }

        /// <summary>
        /// Seed for one feature, derived from where it actually is on Earth rather than from its
        /// index in an array. Two runs, two machines, or a re-download in a different order all
        /// give the same result -- which is the determinism requirement, applied to real data.
        /// </summary>
        public uint SeedForLocalPosition(Vector2 localXZ, uint worldSeed, uint salt)
        {
            var geo = new GeoCoordinate(
                _originLatitude + localXZ.y / MetersPerDegreeLatitude(),
                _originLongitude + localXZ.x / MetersPerDegreeLongitude());

            return geo.ToSeed(DeterministicHash.Salt(worldSeed, salt), 0.00001d);
        }

        double MetersPerDegreeLatitude() => new FlatWorldProjection(Origin).MetersPerDegreeLatitude(_originLatitude);
        double MetersPerDegreeLongitude() => new FlatWorldProjection(Origin).MetersPerDegreeLongitude(_originLatitude);

        /// <summary>Fills the asset from imported data. Editor-only in practice.</summary>
        public void Populate(string displayName, double originLat, double originLon,
                             float sizeX, float sizeZ,
                             int heightResolution, float[] heights,
                             GeoBuilding[] buildings, GeoWay[] roads,
                             GeoArea[] water, GeoArea[] parks, Vector2[] points)
        {
            _displayName = displayName;
            _originLatitude = originLat;
            _originLongitude = originLon;
            _sizeX = sizeX;
            _sizeZ = sizeZ;
            _heightResolution = heightResolution;
            _heights = heights;
            _buildings = buildings;
            _roads = roads;
            _water = water;
            _parks = parks;
            _points = points;

            _minHeight = float.MaxValue;
            _maxHeight = float.MinValue;
            if (heights == null) return;

            foreach (float h in heights)
            {
                if (h < _minHeight) _minHeight = h;
                if (h > _maxHeight) _maxHeight = h;
            }
        }
    }
}
