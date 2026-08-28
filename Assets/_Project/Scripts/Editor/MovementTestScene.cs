using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Survival.EditorTools
{
    /// <summary>
    /// A flat, ugly, purpose-built room for judging how the character feels.
    /// <para>
    /// Separate from the terrain scene on purpose. Procedural terrain has no walls, no right
    /// angles and no repeating pattern, which makes it useless for the three things that most
    /// need checking: whether the camera handles obstacles, what the real slope and step limits
    /// are, and how fast the character actually feels. A checkerboard floor answers the last one
    /// by itself -- speed is invisible without something to measure it against.
    /// </para>
    /// </summary>
    public static class MovementTestScene
    {
        const float FloorSize = 200f;

        [MenuItem("Survival/Setup/4 - Build Movement Test Scene", false, 23)]
        public static void Build()
        {
            if (SceneWiring.RefuseDuringPlayMode("Movement Test")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ProjectPaths.EnsureFolder(ProjectPaths.Scenes);
            ProjectPaths.EnsureFolder(ProjectPaths.Materials);

            EnsureCheckerAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loaded after the new scene: see the note in PrototypeSetup.BuildPrototypeScene.
            var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.CheckerMaterial);
            Material obstacleMaterial = SceneWiring.CreateColorMaterial(
                ProjectPaths.Materials + "/M_TestObstacle.mat", new Color(0.55f, 0.50f, 0.45f));
            Material accentMaterial = SceneWiring.CreateColorMaterial(
                ProjectPaths.Materials + "/M_TestAccent.mat", new Color(0.75f, 0.42f, 0.25f));

            PrototypeSetup.CreateSun(new Color(1f, 0.98f, 0.94f));
            ApplyLighting();

            BuildFloor(floorMaterial);

            var obstacles = new GameObject("Obstacles").transform;
            BuildCameraWalls(obstacles, obstacleMaterial);
            BuildPillars(obstacles, obstacleMaterial);
            BuildRamps(obstacles, accentMaterial);
            BuildSteps(obstacles, accentMaterial);
            BuildJumpTargets(obstacles, accentMaterial);
            BuildOverhang(obstacles, obstacleMaterial);

            GameObject player = PlayerRigBuilder.CreatePlayer(new Vector3(0f, 1f, 0f));
            GameObject cameraObject = PlayerRigBuilder.CreateCamera(
                player.transform, new Color(0.45f, 0.52f, 0.58f));
            PlayerRigBuilder.Wire(player, cameraObject.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ProjectPaths.MovementTestScene);
            PrototypeSetup.RegisterSceneInBuildSettings(ProjectPaths.MovementTestScene);

            Selection.activeGameObject = player;
            Debug.Log($"[Survival] Movement test scene built at {ProjectPaths.MovementTestScene}. " +
                      "Checkerboard squares are 2 m across, so speed can be read off the floor.");
        }

        static void BuildFloor(Material material)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = Vector3.one * (FloorSize / 10f); // a Plane is 10 units wide
            if (material != null) floor.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>An open corner and a narrow corridor: the two shapes that break naive camera
        /// collision. The corridor is deliberately tighter than the camera's resting distance.</summary>
        static void BuildCameraWalls(Transform parent, Material material)
        {
            var group = new GameObject("CameraWalls").transform;
            group.SetParent(parent, false);

            Box(group, "WallNorth", new Vector3(-24f, 2.5f, 10f), new Vector3(16f, 5f, 0.6f), material);
            Box(group, "WallWest", new Vector3(-32f, 2.5f, 2f), new Vector3(0.6f, 5f, 16f), material);

            Box(group, "CorridorA", new Vector3(-22f, 2f, -14f), new Vector3(0.6f, 4f, 20f), material);
            Box(group, "CorridorB", new Vector3(-19f, 2f, -14f), new Vector3(0.6f, 4f, 20f), material);
        }

        static void BuildPillars(Transform parent, Material material)
        {
            var group = new GameObject("Pillars").transform;
            group.SetParent(parent, false);

            float[] radii = { 0.4f, 0.9f, 1.6f, 2.6f };
            for (int i = 0; i < radii.Length; i++)
            {
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"Pillar_{radii[i]:0.0}m";
                pillar.transform.SetParent(group, false);
                pillar.transform.localPosition = new Vector3(14f + i * 7f, 3f, 12f);
                pillar.transform.localScale = new Vector3(radii[i] * 2f, 3f, radii[i] * 2f);
                if (material != null) pillar.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        /// <summary>Slopes either side of the CharacterController's 50 degree limit, so the exact
        /// point where the character stops being able to climb is visible rather than guessed.</summary>
        static void BuildRamps(Transform parent, Material material)
        {
            var group = new GameObject("Ramps").transform;
            group.SetParent(parent, false);

            float[] angles = { 15f, 30f, 45f, 55f };
            for (int i = 0; i < angles.Length; i++)
            {
                GameObject ramp = Box(group, $"Ramp_{angles[i]:0}deg",
                                      new Vector3(-4f + i * 9f, 0f, -22f),
                                      new Vector3(6f, 0.5f, 14f), material);
                ramp.transform.localRotation = Quaternion.Euler(-angles[i], 0f, 0f);
            }
        }

        /// <summary>Steps around the 0.4 m step offset. Anything above it should stop the
        /// character dead, and it is worth knowing that limit by feel.</summary>
        static void BuildSteps(Transform parent, Material material)
        {
            var group = new GameObject("Steps").transform;
            group.SetParent(parent, false);

            float[] heights = { 0.2f, 0.35f, 0.5f, 0.7f, 1f };
            for (int i = 0; i < heights.Length; i++)
                Box(group, $"Step_{heights[i]:0.00}m",
                    new Vector3(24f, heights[i] * 0.5f, -6f - i * 4f),
                    new Vector3(6f, heights[i], 3f), material);
        }

        /// <summary>Ledges at, just under, and well over the jump height, to calibrate it.</summary>
        static void BuildJumpTargets(Transform parent, Material material)
        {
            var group = new GameObject("JumpTargets").transform;
            group.SetParent(parent, false);

            float[] heights = { 0.9f, 1.4f, 1.8f, 2.4f };
            for (int i = 0; i < heights.Length; i++)
                Box(group, $"Ledge_{heights[i]:0.0}m",
                    new Vector3(-14f - i * 6f, heights[i] * 0.5f, 24f),
                    new Vector3(5f, heights[i], 5f), material);

            // A gap to clear, for judging air control rather than height.
            Box(group, "GapNear", new Vector3(8f, 1f, 30f), new Vector3(6f, 2f, 6f), material);
            Box(group, "GapFar", new Vector3(8f, 1f, 40f), new Vector3(6f, 2f, 6f), material);
        }

        /// <summary>A roof to walk under. The camera has to duck without shoving the view into
        /// the character's back.</summary>
        static void BuildOverhang(Transform parent, Material material)
        {
            var group = new GameObject("Overhang").transform;
            group.SetParent(parent, false);

            Box(group, "LegA", new Vector3(30f, 1.6f, 26f), new Vector3(1f, 3.2f, 1f), material);
            Box(group, "LegB", new Vector3(38f, 1.6f, 26f), new Vector3(1f, 3.2f, 1f), material);
            Box(group, "Roof", new Vector3(34f, 3.4f, 26f), new Vector3(10f, 0.4f, 8f), material);
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            if (material != null) box.GetComponent<MeshRenderer>().sharedMaterial = material;
            return box;
        }

        static void ApplyLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.58f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.42f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.22f);
            RenderSettings.fog = false; // fog would hide exactly the distance being judged
        }

        /// <summary>
        /// Generates the checkerboard once and leaves it in the project. Two metres per square,
        /// so a character crossing one square per second is doing 2 m/s and you can see it.
        /// </summary>
        static void EnsureCheckerAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.CheckerMaterial) != null) return;

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.CheckerTexture) == null)
            {
                const int Size = 256;
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
                var light = new Color(0.62f, 0.63f, 0.60f);
                var dark = new Color(0.44f, 0.45f, 0.43f);

                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    bool even = ((x / (Size / 2)) + (y / (Size / 2))) % 2 == 0;
                    texture.SetPixel(x, y, even ? light : dark);
                }

                texture.Apply();
                File.WriteAllBytes(
                    Path.Combine(Directory.GetCurrentDirectory(), ProjectPaths.CheckerTexture),
                    texture.EncodeToPNG());
                Object.DestroyImmediate(texture);

                AssetDatabase.ImportAsset(ProjectPaths.CheckerTexture, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = AssetImporter.GetAtPath(ProjectPaths.CheckerTexture) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }

            Material material = SceneWiring.CreateColorMaterial(ProjectPaths.CheckerMaterial, Color.white);
            var checker = AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.CheckerTexture);

            material.SetTexture("_BaseMap", checker);
            material.mainTexture = checker;

            // The texture holds a 2x2 board, and the floor is FloorSize across. Tiling it once per
            // 4 metres puts one square every 2 metres.
            var tiling = new Vector2(FloorSize / 4f, FloorSize / 4f);
            material.SetTextureScale("_BaseMap", tiling);
            material.mainTextureScale = tiling;

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }
    }
}
