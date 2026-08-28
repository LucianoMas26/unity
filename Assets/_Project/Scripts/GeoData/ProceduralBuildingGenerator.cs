using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Survival.GeoData
{
    /// <summary>
    /// Procedurally generates city buildings using real OpenStreetMap footprints.
    /// <para>
    /// Distributes buildings across Levels of Detail (LOD):
    /// - LOD 0: Fast batch-extruded solid exterior volumes (~90-95% of the city).
    /// - LOD 1: Selected explorable buildings with walk-in doors, interior floors and staircases (~5-10%).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralBuildingGenerator : MonoBehaviour
    {
        [SerializeField] GeoDataset _dataset;
        [SerializeField] Material _material;

        [Header("Height & Proportions")]
        [Tooltip("Minimum height in metres so buildings never look smaller than the player.")]
        [SerializeField] float _minHeight = 7.5f;

        [Tooltip("Height per floor in metres.")]
        [SerializeField] float _floorHeight = 3.2f;

        [Tooltip("Metres each building is sunk into the ground.")]
        [SerializeField] float _foundationDepth = 4.0f;

        [Header("LOD & Explorable Selection")]
        [Tooltip("Percentage of buildings generated as explorable interiors (LOD 1).")]
        [Range(0f, 1f)][SerializeField] float _explorableRatio = 0.08f;

        [Tooltip("Seed used for deterministic selection of explorable buildings.")]
        [SerializeField] int _seed = 1337;

        public GeoDataset Dataset
        {
            get => _dataset;
            set => _dataset = value;
        }

        public Material Material
        {
            get => _material;
            set => _material = value;
        }

        public void Rebuild()
        {
            if (_dataset == null)
            {
                Debug.LogError("[ProceduralBuildingGenerator] No dataset assigned.", this);
                return;
            }

            ClearChildren();

            var solidVertices = new List<Vector3>();
            var solidColors = new List<Color>();
            var solidTriangles = new List<int>();

            int explorableCount = 0;
            int solidCount = 0;

            var explorableHolder = new GameObject("ExplorableBuildings");
            explorableHolder.transform.SetParent(transform, false);

            for (int i = 0; i < _dataset.Buildings.Length; i++)
            {
                GeoBuilding building = _dataset.Buildings[i];
                float groundY = _dataset.SampleElevation(building.Centre.x, building.Centre.y);
                Color wallColor = ColourFor(building.Archetype);

                bool isExplorable = IsBuildingExplorable(i, building);

                if (isExplorable)
                {
                    explorableCount++;
                    BuildSingleExplorable(explorableHolder.transform, i, building, groundY, wallColor);
                }
                else
                {
                    solidCount++;
                    BuildingMeshBuilder.AddSolidBuilding(
                        solidVertices, solidColors, solidTriangles,
                        building, groundY, _foundationDepth, _minHeight, wallColor);
                }
            }

            // Create batch mesh for all solid LOD 0 buildings
            if (solidVertices.Count > 0)
            {
                CreateMesh("SolidBuildings_LOD0", solidVertices, solidColors, solidTriangles, true, transform);
            }

            Debug.Log($"[ProceduralBuildingGenerator] Generados {_dataset.Buildings.Length} edificios: " +
                      $"{solidCount} sólidos (LOD 0) y {explorableCount} explorables (LOD 1).", this);
        }

        bool IsBuildingExplorable(int index, in GeoBuilding building)
        {
            // Deterministic hash based on index and seed
            int hash = (index * 397) ^ (_seed * 17) ^ (int)(building.Centre.x * 100f) ^ (int)(building.Centre.y * 100f);
            float normalized = Mathf.Abs(hash % 1000) / 1000f;

            // Prioritize key archetypes
            if (building.Archetype == BuildingArchetype.Hospital ||
                building.Archetype == BuildingArchetype.Commercial ||
                building.Archetype == BuildingArchetype.Apartments)
            {
                return normalized < Mathf.Min(0.40f, _explorableRatio * 3f);
            }

            return normalized < _explorableRatio;
        }

        void BuildSingleExplorable(Transform parent, int id, in GeoBuilding building, float groundY, Color wallColor)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            BuildingMeshBuilder.AddExplorableBuilding(
                vertices, colors, triangles,
                building, groundY, _foundationDepth, _minHeight, _floorHeight, wallColor);

            var go = CreateMesh($"Building_{id}_Explorable", vertices, colors, triangles, true, parent);
            go.transform.position = Vector3.zero;
        }

        GameObject CreateMesh(string label, List<Vector3> vertices, List<Color> colors,
                              List<int> triangles, bool collider, Transform parent)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);

            var mesh = new Mesh { name = label, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _material;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;

            return go;
        }

        void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }

        static Color ColourFor(BuildingArchetype archetype) => archetype switch
        {
            BuildingArchetype.House => new Color(0.58f, 0.52f, 0.45f),
            BuildingArchetype.Apartments => new Color(0.48f, 0.50f, 0.56f),
            BuildingArchetype.Commercial => new Color(0.42f, 0.48f, 0.54f),
            BuildingArchetype.Retail => new Color(0.60f, 0.52f, 0.40f),
            BuildingArchetype.Industrial => new Color(0.46f, 0.42f, 0.38f),
            BuildingArchetype.Hospital => new Color(0.75f, 0.40f, 0.40f),
            BuildingArchetype.School => new Color(0.46f, 0.60f, 0.46f),
            BuildingArchetype.Religious => new Color(0.65f, 0.60f, 0.40f),
            BuildingArchetype.Civic => new Color(0.55f, 0.46f, 0.60f),
            _ => new Color(0.50f, 0.49f, 0.47f),
        };
    }
}
