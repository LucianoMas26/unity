using Survival.Core;
using Survival.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Survival.EditorTools
{
    public static partial class PrototypeSetup
    {
        [MenuItem("Survival/Setup/3 - Build Prototype Scene", false, 22)]
        public static void BuildPrototypeScene()
        {
            if (SceneWiring.RefuseDuringPlayMode("Prototype")) return;

            if (AssetDatabase.LoadAssetAtPath<WorldSettings>(ProjectPaths.WorldSettingsAsset) == null
                || AssetDatabase.LoadAssetAtPath<RegionDefinition>(ProjectPaths.TestRegionAsset) == null)
            {
                Debug.LogError("[Survival] World assets are missing. Run Survival/Setup/2 first.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ProjectPaths.EnsureFolder(ProjectPaths.Scenes);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load AFTER the new scene exists. NewScene unloads assets nothing is holding on to,
            // which destroys the native half of anything loaded before it. The managed wrapper
            // survives, so field reads still look correct -- but assigning such an object to a
            // SerializedProperty stores null in silence.
            var settings = AssetDatabase.LoadAssetAtPath<WorldSettings>(ProjectPaths.WorldSettingsAsset);
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(ProjectPaths.TestRegionAsset);
            var terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.TerrainMaterial);

            Vector3 spawn = ResolveSpawnPoint(settings, region);

            CreateSun(new Color(0.88f, 0.90f, 0.93f));
            GameObject player = PlayerRigBuilder.CreatePlayer(spawn);
            GameObject cameraObject = PlayerRigBuilder.CreateCamera(player.transform, region.FogColor);
            PlayerRigBuilder.Wire(player, cameraObject.transform);

            CreateWorld(settings, terrainMaterial, player.transform);
            ApplySceneLighting(region);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ProjectPaths.PrototypeScene);
            RegisterSceneInBuildSettings(ProjectPaths.PrototypeScene);

            Selection.activeGameObject = player;
            Debug.Log($"[Survival] Prototype scene built at {ProjectPaths.PrototypeScene}. " +
                      $"Spawn {spawn}, seed {settings.Seed}.");
        }

        /// <summary>
        /// Height is a pure function of the seed, so the editor can place the player on exactly
        /// the ground the game will generate at runtime -- no waiting, no raycast, no guess.
        /// </summary>
        static Vector3 ResolveSpawnPoint(WorldSettings settings, RegionDefinition region)
        {
            var context = new WorldContext(settings.Seed, new FlatWorldProjection(settings.OriginGeo));
            var provider = new ProceduralHeightProvider(context, new SingleRegionProvider(region.ToSnapshot()));

            Vector3 centre = settings.RegionCentre;
            centre.y = provider.SampleHeight(centre.x, centre.z) + 1.5f;
            return centre;
        }

        internal static GameObject CreateSun(Color color)
        {
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();

            light.type = LightType.Directional;
            light.color = color;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;

            go.transform.rotation = Quaternion.Euler(46f, -35f, 0f);
            return go;
        }

        static GameObject CreateWorld(WorldSettings settings, Material terrainMaterial, Transform viewer)
        {
            var go = new GameObject("World");
            var streamer = go.AddComponent<WorldStreamer>();

            var serialized = new SerializedObject(streamer);
            SceneWiring.AssignReference(serialized, "_settings", settings);
            SceneWiring.AssignReference(serialized, "_viewer", viewer);
            SceneWiring.AssignReference(serialized, "_terrainMaterial", terrainMaterial);

            return go;
        }

        static void ApplySceneLighting(RegionDefinition region)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.45f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.33f, 0.31f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.19f, 0.17f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = region.FogColor;
            RenderSettings.fogDensity = region.FogDensity;
        }

        internal static void RegisterSceneInBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene entry in existing)
                if (entry.path == scenePath) return;

            var updated = new EditorBuildSettingsScene[existing.Length + 1];
            existing.CopyTo(updated, 0);
            updated[existing.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
