using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Survival.GeoData
{
    /// <summary>
    /// Draws the real features as placeholder volumes: a box per building, ribbons along the
    /// streets, flat polygons for water and parks.
    /// <para>
    /// Explicitly not the modular building system. Nothing here generates walls, doors or
    /// interiors -- it renders the ingested data so the pipeline can be judged, using the exact
    /// same <see cref="GeoDataset"/> that the real generator will consume later.
    /// </para>
    /// <para>
    /// Everything is merged into a handful of meshes. Seven thousand GameObjects would be seven
    /// thousand draw calls and a Hierarchy nobody can scroll.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GeoFeatureSpawner : MonoBehaviour
    {
        [SerializeField] GeoDataset _dataset;

        [Tooltip("Vertex-colour material. The terrain one works: colours are baked per vertex.")]
        [SerializeField] Material _material;

        [Header("What to draw")]
        [SerializeField] bool _buildings = true;
        [SerializeField] bool _roads = true;
        [SerializeField] bool _water = true;
        [SerializeField] bool _parks = false; // Giant untessellated polygons slice across terrain

        [Header("Placement")]
        [Tooltip("Metres each building is sunk into the ground, so it does not float on a slope.")]
        [SerializeField] float _foundationDepth = 6f;

        [Tooltip("Metres that flat features sit above the terrain, to stop z-fighting.")]
        [SerializeField] float _surfaceOffset = 0.35f;

        [SerializeField] bool _generateColliders = true;

        public GeoDataset Dataset
        {
            get => _dataset;
            set => _dataset = value;
        }

        int _builtVertices;

        void Start() => Rebuild();

        public void Rebuild()
        {
            if (_dataset == null)
            {
                Debug.LogError("[GeoFeatureSpawner] No dataset assigned.", this);
                return;
            }

            ClearChildren();

            _builtVertices = 0;

            if (_buildings) BuildBuildings();
            if (_roads) BuildRoads();
            if (_water) BuildAreas(_dataset.Water, "Water", new Color(0.20f, 0.28f, 0.33f), -0.4f);
            if (_parks) BuildAreas(_dataset.Parks, "Parks", new Color(0.28f, 0.36f, 0.24f), 0f);

            // Reported rather than silent. "Nothing appeared" is otherwise indistinguishable from
            // "the component never ran", and those two have completely different causes.
            // Bounds too, not just counts. "180k vertices" proves the meshes were built; only
            // their extent proves they were built where the terrain actually is.
            var bounds = new Bounds();
            bool first = true;
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                if (first) { bounds = filter.sharedMesh.bounds; first = false; }
                else bounds.Encapsulate(filter.sharedMesh.bounds);
            }

            Debug.Log($"[GeoFeatureSpawner] '{_dataset.DisplayName}': {transform.childCount} mallas, " +
                      $"{_builtVertices:N0} vertices. Edificios {_dataset.Buildings.Length}, " +
                      $"calles {_dataset.Roads.Length}, agua {_dataset.Water.Length}, " +
                      $"parques {_dataset.Parks.Length}. Extension X {bounds.min.x:F0}..{bounds.max.x:F0}, " +
                      $"Y {bounds.min.y:F0}..{bounds.max.y:F0}, Z {bounds.min.z:F0}..{bounds.max.z:F0}.", this);
        }

        void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }

        // --- Buildings ------------------------------------------------------------------

        [Header("Building Heights & Explorable LODs")]
        [Tooltip("Minimum height in metres so buildings never look smaller than the player.")]
        [SerializeField] float _minBuildingHeight = 10.0f;

        [Tooltip("Height per floor in metres for explorable buildings.")]
        [SerializeField] float _floorHeight = 3.2f;

        [Tooltip("Ratio of buildings generated as explorable interiors with doors and stairs (LOD 1).")]
        [Range(0f, 0.5f)][SerializeField] float _explorableRatio = 0.08f;

        [SerializeField] int _seed = 1337;

        void BuildBuildings()
        {
            var solidVertices = new List<Vector3>();
            var solidColors = new List<Color>();
            var solidTriangles = new List<int>();

            int explorableCount = 0;
            int solidCount = 0;

            var explorableParent = new GameObject("ExplorableBuildings");
            explorableParent.transform.SetParent(transform, false);

            for (int i = 0; i < _dataset.Buildings.Length; i++)
            {
                GeoBuilding building = _dataset.Buildings[i];
                float ground = _dataset.SampleElevation(building.Centre.x, building.Centre.y);
                Color colour = ColourFor(building.Archetype);

                bool isExplorable = IsBuildingExplorable(i, building);

                if (isExplorable)
                {
                    explorableCount++;
                    var explorableVertices = new List<Vector3>();
                    var explorableColors = new List<Color>();
                    var explorableTriangles = new List<int>();

                    BuildingMeshBuilder.AddExplorableBuilding(
                        explorableVertices, explorableColors, explorableTriangles,
                        building, ground, _foundationDepth, _minBuildingHeight, _floorHeight, colour);

                    CreateMesh($"Building_{i}_Explorable", explorableVertices, explorableColors,
                               explorableTriangles, true, explorableParent.transform);
                }
                else
                {
                    solidCount++;
                    BuildingMeshBuilder.AddSolidBuilding(
                        solidVertices, solidColors, solidTriangles,
                        building, ground, _foundationDepth, _minBuildingHeight, colour);
                }
            }

            if (solidVertices.Count > 0)
            {
                CreateMesh("SolidBuildings_LOD0", solidVertices, solidColors, solidTriangles, _generateColliders, transform);
            }

            Debug.Log($"[GeoFeatureSpawner] Edificios: {solidCount} solidos (LOD 0) y {explorableCount} explorables (LOD 1).", this);
        }

        bool IsBuildingExplorable(int index, in GeoBuilding building)
        {
            if (_explorableRatio <= 0f) return false;

            int hash = (index * 397) ^ (_seed * 17) ^ (int)(building.Centre.x * 100f) ^ (int)(building.Centre.y * 100f);
            float normalized = Mathf.Abs(hash % 1000) / 1000f;

            if (building.Archetype == BuildingArchetype.Hospital ||
                building.Archetype == BuildingArchetype.Commercial ||
                building.Archetype == BuildingArchetype.Apartments)
            {
                return normalized < Mathf.Min(0.35f, _explorableRatio * 3f);
            }

            return normalized < _explorableRatio;
        }

        /// <summary>
        /// Placeholder palette. Deliberately readable rather than pretty: the point is to see at
        /// a glance which archetypes the tag mapping actually produced.
        /// </summary>
        static Color ColourFor(BuildingArchetype archetype) => archetype switch
        {
            BuildingArchetype.House => new Color(0.52f, 0.46f, 0.40f),
            BuildingArchetype.Apartments => new Color(0.44f, 0.44f, 0.50f),
            BuildingArchetype.Commercial => new Color(0.38f, 0.44f, 0.50f),
            BuildingArchetype.Retail => new Color(0.55f, 0.48f, 0.36f),
            BuildingArchetype.Industrial => new Color(0.42f, 0.38f, 0.34f),
            BuildingArchetype.Hospital => new Color(0.70f, 0.35f, 0.35f),
            BuildingArchetype.School => new Color(0.42f, 0.55f, 0.42f),
            BuildingArchetype.Religious => new Color(0.60f, 0.55f, 0.35f),
            BuildingArchetype.Civic => new Color(0.50f, 0.42f, 0.55f),
            BuildingArchetype.Roof => new Color(0.35f, 0.35f, 0.35f),
            BuildingArchetype.Parking => new Color(0.33f, 0.33f, 0.30f),
            _ => new Color(0.46f, 0.45f, 0.43f),
        };

        // --- Roads ----------------------------------------------------------------------

        void BuildRoads()
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            Vector2[] points = _dataset.Points;

            foreach (GeoWay road in _dataset.Roads)
            {
                Color colour = road.Class == RoadClass.Path
                    ? new Color(0.40f, 0.38f, 0.34f)
                    : new Color(0.26f, 0.26f, 0.27f);

                float half = Mathf.Max(1f, road.Width) * 0.5f;

                for (int i = road.PointStart; i < road.PointStart + road.PointCount - 1; i++)
                {
                    Vector2 a = points[i];
                    Vector2 b = points[i + 1];
                    Vector2 direction = b - a;
                    if (direction.sqrMagnitude < 0.01f) continue;

                    Vector2 normal = new Vector2(-direction.y, direction.x).normalized * half;
                    int v = vertices.Count;

                    AddSurfaceVertex(vertices, colors, a - normal, colour, 0f);
                    AddSurfaceVertex(vertices, colors, a + normal, colour, 0f);
                    AddSurfaceVertex(vertices, colors, b + normal, colour, 0f);
                    AddSurfaceVertex(vertices, colors, b - normal, colour, 0f);

                    triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 1);
                    triangles.Add(v); triangles.Add(v + 3); triangles.Add(v + 2);
                }
            }

            CreateMesh("Roads", vertices, colors, triangles, false);
        }

        // --- Areas ----------------------------------------------------------------------

        void BuildAreas(GeoArea[] areas, string label, Color colour, float heightOffset)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var polygon = new List<Vector2>();
            var indices = new List<int>();
            Vector2[] points = _dataset.Points;

            foreach (GeoArea area in areas)
            {
                polygon.Clear();
                for (int i = area.PointStart; i < area.PointStart + area.PointCount; i++)
                    polygon.Add(points[i]);

                // OSM closes rings by repeating the first node; the triangulator must not see it.
                if (polygon.Count > 2 && (polygon[0] - polygon[polygon.Count - 1]).sqrMagnitude < 0.01f)
                    polygon.RemoveAt(polygon.Count - 1);
                if (polygon.Count < 3) continue;

                indices.Clear();
                if (!PolygonTriangulator.Triangulate(polygon, indices)) continue;

                int baseIndex = vertices.Count;
                foreach (Vector2 point in polygon)
                    AddSurfaceVertex(vertices, colors, point, colour, heightOffset);

                foreach (int index in indices) triangles.Add(baseIndex + index);
            }

            CreateMesh(label, vertices, colors, triangles, false);
        }

        void AddSurfaceVertex(List<Vector3> vertices, List<Color> colors,
                              Vector2 xz, Color colour, float heightOffset)
        {
            float y = _dataset.SampleElevation(xz.x, xz.y) + _surfaceOffset + heightOffset;
            vertices.Add(new Vector3(xz.x, y, xz.y));
            colors.Add(colour);
        }

        // --- Mesh plumbing --------------------------------------------------------------

        void CreateMesh(string label, List<Vector3> vertices, List<Color> colors,
                        List<int> triangles, bool collider, Transform parent = null)
        {
            if (vertices.Count == 0 || triangles.Count == 0) return;

            var go = new GameObject(label);
            go.transform.SetParent(parent != null ? parent : transform, false);

            var mesh = new Mesh { name = label };
            // Well past 65535 vertices for the buildings, so 32-bit indices are not optional.
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _builtVertices += vertices.Count;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _material;

            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
    }
}
