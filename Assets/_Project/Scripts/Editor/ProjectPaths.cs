using System.IO;
using UnityEditor;
using UnityEngine;

namespace Survival.EditorTools
{
    /// <summary>Where generated project assets live. One place, so the setup steps agree.</summary>
    public static class ProjectPaths
    {
        public const string Root = "Assets/_Project";
        public const string Settings = Root + "/Settings";
        public const string Scenes = Root + "/Scenes";
        public const string Materials = Root + "/Art/Materials";
        public const string Shaders = Root + "/Art/Shaders";

        public const string PrototypeScene = Scenes + "/Prototype.unity";
        public const string MovementTestScene = Scenes + "/MovementTest.unity";
        public const string CheckerTexture = Materials + "/T_Checker.png";
        public const string CheckerMaterial = Materials + "/M_Checker.mat";
        public const string WorldSettingsAsset = Settings + "/WorldSettings.asset";
        public const string TestRegionAsset = Settings + "/Region_TestRegion.asset";
        public const string PipelineAsset = Settings + "/SurvivalURP.asset";
        public const string RendererAsset = Settings + "/SurvivalURP_Renderer.asset";
        public const string TerrainMaterial = Materials + "/M_Terrain.mat";

        public const string TerrainShaderName = "Survival/Terrain Vertex Color";

        /// <summary>Creates an asset folder and every missing parent above it.</summary>
        public static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;

            string parent = Path.GetDirectoryName(assetPath)?.Replace(Path.DirectorySeparatorChar, '/');
            string leaf = Path.GetFileName(assetPath);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
            {
                Debug.LogError($"[Survival] Cannot create folder from path '{assetPath}'.");
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
