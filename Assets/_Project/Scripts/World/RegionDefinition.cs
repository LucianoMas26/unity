using UnityEngine;

namespace Survival.World
{
    /// <summary>
    /// One region of the world: how its ground is shaped, how dangerous it is, and (later) what
    /// grows, spawns and can be looted there. The prototype ships a single region, but the type
    /// is already plural -- Amazonia, Patagonia and Desert differ only by the values in here.
    /// </summary>
    [CreateAssetMenu(fileName = "Region_", menuName = "Survival/World/Region Definition")]
    public sealed class RegionDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id. Used for save data and discovery tracking -- do not rename casually.")]
        [SerializeField] string _id = "test_region";
        [SerializeField] string _displayName = "Region de Prueba";
        [TextArea(2, 4)][SerializeField] string _description = "";

        [Tooltip("Baseline threat, before local modifiers. Drives the HUD danger readout.")]
        [Range(0, 5)][SerializeField] int _dangerLevel = 1;

        [Header("Terrain")]
        [SerializeField] TerrainProfile _terrain = TerrainProfile.Default;
        [SerializeField] TerrainPalette _palette = TerrainPalette.Default;

        [Header("Atmosphere")]
        [SerializeField] Color _fogColor = new Color(0.55f, 0.57f, 0.54f);
        [SerializeField] float _fogDensity = 0.0035f;

        // Content tables land here as the other systems come online. Kept as explicit TODOs
        // rather than empty lists, so nobody wires up a half-defined format by accident.
        // TODO(vegetation): VegetationTable
        // TODO(creatures):  CreatureSpawnTable
        // TODO(loot):       LootTable
        // TODO(buildings):  BuildingStyleSet

        public string Id => _id;
        public string DisplayName => _displayName;
        public int DangerLevel => _dangerLevel;
        public Color FogColor => _fogColor;
        public float FogDensity => _fogDensity;

        /// <summary>
        /// Thread-safe copy for the generation workers. Call this on the main thread only.
        /// </summary>
        public RegionSnapshot ToSnapshot() => new RegionSnapshot(_id, _displayName, _dangerLevel, _terrain, _palette);

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id)) _id = name.ToLowerInvariant();
            _fogDensity = Mathf.Max(0f, _fogDensity);
        }
    }

    /// <summary>
    /// Immutable, plain-C# view of a region. This is what generators actually read, which is
    /// what makes off-thread chunk building legal.
    /// </summary>
    public sealed class RegionSnapshot
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int DangerLevel { get; }
        public TerrainProfile Terrain { get; }
        public TerrainPalette Palette { get; }

        public RegionSnapshot(string id, string displayName, int dangerLevel,
                              TerrainProfile terrain, TerrainPalette palette)
        {
            Id = id;
            DisplayName = displayName;
            DangerLevel = dangerLevel;
            Terrain = terrain;
            Palette = palette;
        }

        public static RegionSnapshot CreateFallback() => new RegionSnapshot(
            "fallback", "Region sin definir", 0, TerrainProfile.Default, TerrainPalette.Default);
    }
}
