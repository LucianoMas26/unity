using Survival.Player;
using UnityEditor;
using UnityEngine;

namespace Survival.EditorTools
{
    /// <summary>
    /// Builds the player and its camera. Shared by every scene that needs them, so the two
    /// scenes cannot drift apart and a fix in one is a fix in both.
    /// </summary>
    public static class PlayerRigBuilder
    {
        public const float CharacterHeight = 1.8f;

        /// <summary>
        /// Player root: collider, locomotion, stamina and brain, with a placeholder body under a
        /// child called Model. The visual is a child on purpose -- swapping in a rigged, animated
        /// character later means replacing that one object and nothing else.
        /// </summary>
        public static GameObject CreatePlayer(Vector3 spawn, Material bodyMaterial = null)
        {
            var player = new GameObject("Player");
            player.transform.position = spawn;

            var controller = player.AddComponent<CharacterController>();
            controller.height = CharacterHeight;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, CharacterHeight * 0.5f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.4f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f; // anything above 0 eats slow movement entirely

            var model = new GameObject("Model");
            model.transform.SetParent(player.transform, false);

            // The imported character if it is there, the capsule if it is not. Scene building has
            // to keep working on a machine that never imported the art pack.
            GameObject character = CharacterSetup.TryInstantiateCharacter(model.transform, CharacterHeight);
            if (character == null) BuildPlaceholderBody(model.transform, bodyMaterial);

            player.AddComponent<PlayerMovement>();
            player.AddComponent<StaminaSystem>();
            player.AddComponent<PlayerController>();

            var animation = player.AddComponent<PlayerAnimation>();
            if (character != null)
                SceneWiring.AssignReference(new SerializedObject(animation), "_animator",
                                            character.GetComponent<Animator>());

            return player;
        }

        /// <summary>
        /// Fallback body. Kept rather than deleted: it is the thing to fall back to when a
        /// character problem needs ruling out, and it costs nothing to keep around.
        /// </summary>
        static void BuildPlaceholderBody(Transform parent, Material bodyMaterial)
        {
            // A capsule primitive is 2 units tall at scale 1.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, CharacterHeight * 0.5f, 0f);
            body.transform.localScale = new Vector3(0.64f, CharacterHeight * 0.5f, 0.64f);
            Object.DestroyImmediate(body.GetComponent<Collider>()); // CharacterController is the collider

            // Without a facing marker it is impossible to tell which way a capsule points.
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "FacingMarker";
            marker.transform.SetParent(body.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.30f, 0.55f);
            marker.transform.localScale = new Vector3(0.35f, 0.22f, 0.6f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            if (bodyMaterial == null) return;

            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
            marker.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
        }

        public static GameObject CreateCamera(Transform target, Color background)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };

            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.nearClipPlane = 0.12f;
            camera.farClipPlane = 1500f;
            camera.fieldOfView = 62f;

            go.AddComponent<AudioListener>();
            go.AddComponent<CameraCollision>();

            var follow = go.AddComponent<PlayerCamera>();
            SceneWiring.AssignReference(new SerializedObject(follow), "_target", target);

            // Development-only framing tool. Compiles out of a release build.
            go.AddComponent<CameraTuner>();

            return go;
        }

        /// <summary>Points the player at its camera. Movement is camera-relative, so without
        /// this the character walks along world axes instead of where you are looking.</summary>
        public static void Wire(GameObject player, Transform cameraTransform)
        {
            var controller = player.GetComponent<PlayerController>();
            var serialized = new SerializedObject(controller);

            SceneWiring.AssignReference(serialized, "_cameraTransform", cameraTransform);
            SceneWiring.AssignReference(serialized, "_stamina", player.GetComponent<StaminaSystem>());
        }
    }
}
