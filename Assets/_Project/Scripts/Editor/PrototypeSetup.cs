using Survival.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

#if SURVIVAL_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Survival.EditorTools
{
    /// <summary>
    /// One-click project setup. Everything here is reproducible from an empty project, which
    /// matters more than it sounds: scene wiring done by hand is the part of a Unity project
    /// that cannot be reviewed, diffed or rebuilt.
    /// </summary>
    public static partial class PrototypeSetup
    {
        [MenuItem("Survival/Setup/Run Full Setup", false, 0)]
        public static void RunFullSetup()
        {
            ConfigureRenderPipeline();
            CreateWorldAssets();
            BuildPrototypeScene();

            Debug.Log("[Survival] Setup complete. Open Assets/_Project/Scenes/Prototype.unity and press Play.");
        }

        [MenuItem("Survival/Setup/1 - Configure URP", false, 20)]
        public static void ConfigureRenderPipeline()
        {
#if !SURVIVAL_URP
            Debug.LogError("[Survival] URP is not installed. Check Packages/manifest.json for " +
                           "com.unity.render-pipelines.universal and let the Package Manager resolve it.");
#else
            ProjectPaths.EnsureFolder(ProjectPaths.Settings);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ProjectPaths.PipelineAsset);
            if (pipeline == null)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ProjectPaths.RendererAsset);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rendererData, ProjectPaths.RendererAsset);
                }

                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, ProjectPaths.PipelineAsset);
            }

            // A streaming open world needs shadows far past URP's 50 m default, or every hill
            // beyond the player's nose renders flat.
            pipeline.shadowDistance = 250f;
            pipeline.shadowCascadeCount = 4;
            pipeline.msaaSampleCount = 4;

            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();

            GraphicsSettings.defaultRenderPipeline = pipeline;

            int originalQuality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(originalQuality, applyExpensiveChanges: false);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Survival] URP configured: {ProjectPaths.PipelineAsset}");
#endif
        }

        [MenuItem("Survival/Setup/2 - Create World Assets", false, 21)]
        public static void CreateWorldAssets()
        {
            ProjectPaths.EnsureFolder(ProjectPaths.Settings);
            ProjectPaths.EnsureFolder(ProjectPaths.Materials);

            RegionDefinition region = LoadOrCreate<RegionDefinition>(ProjectPaths.TestRegionAsset);
            WorldSettings settings = LoadOrCreate<WorldSettings>(ProjectPaths.WorldSettingsAsset);

            // Private serialised fields are set through SerializedObject rather than being made
            // public: the inspector contract stays the same whether a human or this script fills it.
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("_region").objectReferenceValue = region;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            CreateTerrainMaterial();

            AssetDatabase.SaveAssets();
            Debug.Log($"[Survival] World assets ready: {ProjectPaths.WorldSettingsAsset}");
        }

        static Material CreateTerrainMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.TerrainMaterial);
            if (existing != null) return existing;

            Shader shader = Shader.Find(ProjectPaths.TerrainShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[Survival] Shader '{ProjectPaths.TerrainShaderName}' not found. " +
                                 "Falling back to URP/Lit -- terrain will render one flat colour " +
                                 "because URP/Lit ignores vertex colours.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) { name = "M_Terrain" };
            AssetDatabase.CreateAsset(material, ProjectPaths.TerrainMaterial);
            return material;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
