using Survival.GeoData;
using Survival.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Survival.EditorTools
{
    /// <summary>
    /// Builds a scene whose terrain comes from real elevation and whose buildings come from real
    /// OpenStreetMap footprints.
    /// <para>
    /// The point of this scene is to prove one thing: that swapping the input to the pipeline
    /// changes nothing downstream. It uses the same WorldStreamer, the same chunk mesher, the
    /// same LOD and the same player as the fictional region. Only the height source asset differs.
    /// </para>
    /// </summary>
    public static class RealWorldSceneBuilder
    {
        const string ScenePath = ProjectPaths.Scenes + "/RealWorld.unity";
        const string SettingsPath = ProjectPaths.Settings + "/WorldSettings_RealWorld.asset";
        const string RegionPath = ProjectPaths.Settings + "/Region_RealWorld.asset";

        [MenuItem("Survival/Setup/6 - Build Real World Scene", false, 25)]
        public static void Build()
        {
            if (SceneWiring.RefuseDuringPlayMode("Real World")) return;

            GeoDataset dataset = GeoImporter.Import("Rosario");
            if (dataset == null) return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ProjectPaths.EnsureFolder(ProjectPaths.Scenes);
            CreateSettings(dataset);

            // Ask for the path BEFORE the scene changes. NewScene unloads every asset nothing is
            // holding on to, and a destroyed object cannot tell you where it lives -- it returns
            // an empty path, which loads as null, which only fails two statements later.
            string datasetPath = AssetDatabase.GetAssetPath(dataset);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            dataset = AssetDatabase.LoadAssetAtPath<GeoDataset>(datasetPath);
            WorldSettings settings = AssetDatabase.LoadAssetAtPath<WorldSettings>(SettingsPath);
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(RegionPath);
            var terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.TerrainMaterial);

            if (dataset == null || settings == null || region == null || terrainMaterial == null)
            {
                Debug.LogError(
                    "[Survival] No se pudieron recargar los assets despues de crear la escena. " +
                    $"dataset={Describe(dataset, datasetPath)}, settings={Describe(settings, SettingsPath)}, " +
                    $"region={Describe(region, RegionPath)}, material={Describe(terrainMaterial, ProjectPaths.TerrainMaterial)}");
                return;
            }

            // Not the middle of the bounding box: that lands inside a building downtown.
            Vector2 spawnXZ = dataset.GetPreferredSpawnXZ(Mathf.Max(dataset.SizeX, dataset.SizeZ));
            var spawn = new Vector3(spawnXZ.x, dataset.SampleElevation(spawnXZ.x, spawnXZ.y) + 2f, spawnXZ.y);

            PrototypeSetup.CreateSun(new Color(0.95f, 0.94f, 0.90f));
            ApplyLighting(region);

            GameObject player = PlayerRigBuilder.CreatePlayer(spawn);
            GameObject cameraObject = PlayerRigBuilder.CreateCamera(
                player.transform, new Color(0.62f, 0.65f, 0.68f));
            PlayerRigBuilder.Wire(player, cameraObject.transform);

            CreateWorld(settings, dataset, terrainMaterial, player.transform);
            CreateFeatures(dataset, terrainMaterial);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSetup.RegisterSceneInBuildSettings(ScenePath);

            Selection.activeGameObject = player;
            Debug.Log($"[Survival] Escena de mundo real construida en {ScenePath}. " +
                      $"Origen {dataset.Origin}, aparicion {spawn}.");
        }

        /// <summary>
        /// Builds the placeholder city in the open scene without entering Play.
        /// <para>
        /// The spawner normally runs from Start, so in the editor the scene looks empty and there
        /// is no way to tell "nothing was generated" from "you are standing inside it". Running it
        /// here puts the geometry in the Scene view, where you can pull the camera up and look at
        /// the whole city at once.
        /// </para>
        /// <para>
        /// The meshes are generated, not assets, so they do not survive a scene reload. This is
        /// for looking, not for saving.
        /// </para>
        /// </summary>
        [MenuItem("Survival/Geo/Rebuild Features In Open Scene", false, 41)]
        public static void RebuildFeaturesInOpenScene()
        {
            var spawner = Object.FindFirstObjectByType<GeoFeatureSpawner>();
            if (spawner == null)
            {
                Debug.LogError("[Survival] No hay ningun GeoFeatureSpawner en la escena abierta. " +
                               "Abri RealWorld.unity, o construila con Survival/Setup/6.");
                return;
            }

            spawner.Rebuild();
            Selection.activeGameObject = spawner.gameObject;
            SceneView.FrameLastActiveSceneView();
        }

        /// <summary>Says whether a reload failed and where it was looking, so the error names the
        /// asset instead of leaving a bare NullReference two statements downstream.</summary>
        static string Describe(Object asset, string path)
            => asset != null ? "ok" : $"NULL ('{path}')";

        /// <summary>
        /// A region whose colour bands match the relief that is actually there.
        /// <para>
        /// This is why real Rosario first rendered as featureless grass. The shared palette puts
        /// highlands at 90 m and peaks at 210 m, which suits a fictional region spanning 238 m.
        /// Rosario spans 1 to 55 m, so every last vertex fell in the lowest band and the whole
        /// city came out one flat green. The thresholds have to follow the data, not the other
        /// way round -- and the same applies to any region imported after this one.
        /// </para>
        /// </summary>
        static RegionDefinition CreateRegion(GeoDataset dataset)
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(RegionPath);
            if (region == null)
            {
                region = ScriptableObject.CreateInstance<RegionDefinition>();
                AssetDatabase.CreateAsset(region, RegionPath);
            }

            float low = dataset.MinHeight;
            float span = Mathf.Max(1f, dataset.MaxHeight - dataset.MinHeight);

            var serialized = new SerializedObject(region);
            serialized.FindProperty("_id").stringValue = dataset.DisplayName.ToLowerInvariant();
            serialized.FindProperty("_displayName").stringValue = dataset.DisplayName;

            serialized.FindProperty("_palette.HighlandHeight").floatValue = low + span * 0.45f;
            serialized.FindProperty("_palette.PeakHeight").floatValue = low + span * 0.85f;

            // Steeper than the default: a river bluff is the only real slope here, and it should
            // read as bare ground rather than blending away into the grass.
            serialized.FindProperty("_palette.RockSlope").floatValue = 0.35f;

            // Just above the lowest samples, which are the river surface itself.
            serialized.FindProperty("_terrain.WaterLevel").floatValue = low + 1.5f;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(region);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Survival] Paleta ajustada al relieve real: tierras altas desde " +
                      $"{low + span * 0.45f:F0} m, cimas desde {low + span * 0.85f:F0} m " +
                      $"(rango del dato {low:F0}-{dataset.MaxHeight:F0} m).");

            return region;
        }

        static WorldSettings CreateSettings(GeoDataset dataset)
        {
            ProjectPaths.EnsureFolder(ProjectPaths.Settings);

            var settings = AssetDatabase.LoadAssetAtPath<WorldSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<WorldSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            RegionDefinition region = CreateRegion(dataset);
            var serialized = new SerializedObject(settings);

            serialized.FindProperty("_region").objectReferenceValue = region;
            serialized.FindProperty("_regionSizeMeters").floatValue = Mathf.Max(dataset.SizeX, dataset.SizeZ);

            // The projection origin has to be the dataset's own anchor, or every feature would
            // sit at an offset from the ground it was measured against.
            serialized.FindProperty("_originLatitude").doubleValue = dataset.Origin.LatitudeDeg;
            serialized.FindProperty("_originLongitude").doubleValue = dataset.Origin.LongitudeDeg;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            return settings;
        }

        static void CreateWorld(WorldSettings settings, GeoDataset dataset,
                                Material terrainMaterial, Transform viewer)
        {
            var go = new GameObject("World");
            var streamer = go.AddComponent<WorldStreamer>();

            var serialized = new SerializedObject(streamer);
            SceneWiring.AssignReference(serialized, "_settings", settings);
            SceneWiring.AssignReference(serialized, "_viewer", viewer);
            SceneWiring.AssignReference(serialized, "_terrainMaterial", terrainMaterial);
            SceneWiring.AssignReference(serialized, "_heightSourceOverride", dataset);
        }

        static void CreateFeatures(GeoDataset dataset, Material material)
        {
            var go = new GameObject("GeoFeatures");
            var spawner = go.AddComponent<GeoFeatureSpawner>();

            var serialized = new SerializedObject(spawner);
            SceneWiring.AssignReference(serialized, "_dataset", dataset);
            SceneWiring.AssignReference(serialized, "_material", material);
        }

        static void ApplyLighting(RegionDefinition region)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.55f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.40f, 0.40f, 0.39f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.21f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = region != null ? region.FogColor : new Color(0.62f, 0.65f, 0.68f);

            // Thinner than the fictional region: hiding a real city at 300 m would defeat the
            // point of loading a real city.
            RenderSettings.fogDensity = 0.0012f;
        }
    }
}
