#if SURVIVAL_CESIUM
using CesiumForUnity;
using Survival.CesiumBridge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Survival.EditorTools
{
    /// <summary>
    /// A scene that exists to answer four questions about Cesium, and nothing else.
    /// <para>
    /// Kept entirely separate from RealWorld and Prototype on purpose. Nothing in the existing
    /// pipeline is touched or retired until this scene proves Cesium can carry it. If any of the
    /// four checks fails, the Rosario scene and its 7454 buildings are exactly where they were.
    /// </para>
    /// </summary>
    public static class CesiumEvaluationScene
    {
        const string ScenePath = ProjectPaths.Scenes + "/CesiumEvaluation.unity";

        // The anchor everything else in this project is measured against.
        const double RosarioLatitude = -32.9479;
        const double RosarioLongitude = -60.6297;

        /// <summary>Cesium World Terrain. Asset 1 is available to every ion account.</summary>
        const long WorldTerrainAssetID = 1;

        /// <summary>
        /// Cesium OSM Buildings: 350 million extruded OpenStreetMap footprints, worldwide.
        /// Baked 3D Tiles, so they can be styled and selected but not taken apart -- which is
        /// exactly the limitation that matters for the procedural interiors in the brief.
        /// </summary>
        const long OsmBuildingsAssetID = 96188;

        [MenuItem("Survival/Setup/7 - Build Cesium Evaluation Scene", false, 26)]
        public static void Build()
        {
            if (SceneWiring.RefuseDuringPlayMode("Cesium Evaluation")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ProjectPaths.EnsureFolder(ProjectPaths.Scenes);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PrototypeSetup.CreateSun(new Color(0.98f, 0.96f, 0.92f));
            ApplyLighting();

            // The georeference is the origin of everything. Its lat/lon becomes Unity's (0,0,0).
            var georeferenceObject = new GameObject("CesiumGeoreference");
            var georeference = georeferenceObject.AddComponent<CesiumGeoreference>();
            georeference.latitude = RosarioLatitude;
            georeference.longitude = RosarioLongitude;
            // Cesium terrain heights are measured from the WGS84 ellipsoid, not from sea
            // level, and the two differ by roughly 16 m in Rosario. Starting well above
            // everything removes the guesswork: CesiumGroundHold puts the player down.
            georeference.height = 400.0;

            var tilesetObject = new GameObject("Cesium World Terrain");
            tilesetObject.transform.SetParent(georeferenceObject.transform, false);

            var tileset = tilesetObject.AddComponent<Cesium3DTileset>();
            tileset.tilesetSource = CesiumDataSource.FromCesiumIon;
            tileset.ionAssetID = WorldTerrainAssetID;

            // The whole question. Without collision the tiles are scenery, not ground.
            tileset.createPhysicsMeshes = true;

            // No imagery overlay on purpose: the brief rules out photographic textures as the
            // final look, and leaving it off keeps the quota to geometry alone.

            // Real buildings, so the evaluation shows a city rather than an empty plain. These
            // are pre-extruded geometry: fine to walk around, impossible to open up.
            var buildingsObject = new GameObject("Cesium OSM Buildings");
            buildingsObject.transform.SetParent(georeferenceObject.transform, false);

            var buildings = buildingsObject.AddComponent<Cesium3DTileset>();
            buildings.tilesetSource = CesiumDataSource.FromCesiumIon;
            buildings.ionAssetID = OsmBuildingsAssetID;
            buildings.createPhysicsMeshes = true;

            GameObject player = PlayerRigBuilder.CreatePlayer(new Vector3(0f, 3f, 0f));
            player.transform.SetParent(georeferenceObject.transform, false);

            // A globe anchor keeps the player pinned to a real place as the origin moves under it.
            player.AddComponent<CesiumGlobeAnchor>();

            // Origin shifting is what keeps float precision usable away from the georeference.
            var shift = player.AddComponent<CesiumOriginShift>();
            shift.distance = 500.0;

            // Cesium cannot say where the ground is until a tile arrives, and refinement takes
            // it away again. The hold waits for the tiles to settle and puts the player back
            // every time the surface disappears from under them.
            var hold = player.AddComponent<CesiumGroundHold>();
            SceneWiring.AssignReference(new SerializedObject(hold), "_tileset", tileset);

            GameObject cameraObject = PlayerRigBuilder.CreateCamera(
                player.transform, new Color(0.63f, 0.71f, 0.78f));
            cameraObject.transform.SetParent(georeferenceObject.transform, false);
            PlayerRigBuilder.Wire(player, cameraObject.transform);

            var probeObject = new GameObject("CesiumProbe");
            var probe = probeObject.AddComponent<CesiumProbe>();
            var serialized = new SerializedObject(probe);
            SceneWiring.AssignReference(serialized, "_tileset", tileset);
            SceneWiring.AssignReference(serialized, "_player", player.transform);
            SceneWiring.AssignReference(serialized, "_georeference", georeference);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            PrototypeSetup.RegisterSceneInBuildSettings(ScenePath);

            Selection.activeGameObject = probeObject;
            Debug.Log($"[Survival] Escena de evaluacion de Cesium en {ScenePath}. " +
                      $"Georreferencia {RosarioLatitude}, {RosarioLongitude}. " +
                      "Dale a Play y lee el panel: responde las cuatro preguntas.");
        }

        static void ApplyLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.60f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.43f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.22f);

            // No fog: it would hide whether distant tiles are streaming in at all.
            RenderSettings.fog = false;
        }
    }
}
#endif
