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
        [SerializeField] bool _parks = true;

        [Header("Placement")]
        [Tooltip("Metres each building is sunk into the ground, so it does not float on a slope.")]
        [SerializeField] float _foundationDepth = 4f;

        [Tooltip("Metres that flat features sit above the terrain, to stop z-fighting.")]
        [SerializeField] float _surfaceOffset = 0.15f;

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

        void BuildBuildings()
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            foreach (GeoBuilding building in _dataset.Buildings)
            {
                float ground = _dataset.SampleElevation(building.Centre.x, building.Centre.y);
                AddBox(vertices, colors, triangles, building, ground);
            }

            CreateMesh("Buildings", vertices, colors, triangles, _generateColliders);
        }

        void AddBox(List<Vector3> vertices, List<Color> colors, List<int> triangles,
                    in GeoBuilding building, float ground)
        {
            float radians = building.RotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            Vector2 half = building.Size * 0.5f;
            float bottom = ground - _foundationDepth;
            float top = ground + building.Height;
            Color colour = ColourFor(building.Archetype);

            // Four corners of the footprint, rotated into place.
            var corners = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float sx = (i == 0 || i == 3) ? -half.x : half.x;
                float sz = (i < 2) ? -half.y : half.y;
                corners[i] = new Vector3(
                    building.Centre.x + sx * cos - sz * sin,
                    0f,
                    building.Centre.y + sx * sin + sz * cos);
            }

            int baseIndex = vertices.Count;

            // Walls: each side gets its own vertices so the box stays flat-shaded.
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i];
                Vector3 b = corners[(i + 1) % 4];

                vertices.Add(new Vector3(a.x, bottom, a.z));
                vertices.Add(new Vector3(b.x, bottom, b.z));
                vertices.Add(new Vector3(b.x, top, b.z));
                vertices.Add(new Vector3(a.x, top, a.z));

                for (int c = 0; c < 4; c++) colors.Add(colour);

                int v = baseIndex + i * 4;
                triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 1);
                triangles.Add(v); triangles.Add(v + 3); triangles.Add(v + 2);
            }

            // Roof, lightened so the massing reads from above.
            int roof = vertices.Count;
            Color roofColour = Color.Lerp(colour, Color.white, 0.18f);
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(new Vector3(corners[i].x, top, corners[i].z));
                colors.Add(roofColour);
            }

            triangles.Add(roof); triangles.Add(roof + 1); triangles.Add(roof + 2);
            triangles.Add(roof); triangles.Add(roof + 2); triangles.Add(roof + 3);
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
                        List<int> triangles, bool collider)
        {
            if (vertices.Count == 0 || triangles.Count == 0) return;

            var go = new GameObject(label);
            go.transform.SetParent(transform, false);

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
