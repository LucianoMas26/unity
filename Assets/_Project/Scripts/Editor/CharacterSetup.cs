using System.Linq;
using Survival.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Survival.EditorTools
{
    /// <summary>
    /// Wires the imported Stylized Wood Monsters character into the player rig.
    /// <para>
    /// The controller that ships with the pack is a gallery piece: it has no parameters at all,
    /// so nothing in code can drive it. This builds a replacement with a one-dimensional blend
    /// tree keyed on normalised speed, which is the minimum a character controller needs.
    /// </para>
    /// </summary>
    public static class CharacterSetup
    {
        const string PackRoot = "Assets/StylizedCore/StylizedWoodMonsters/URP/AnimationGallery";
        const string CharacterPrefab = PackRoot + "/Prefab/Player.prefab";
        const string CharacterModel = PackRoot + "/Models/Player.fbx";
        const string ClipFolder = PackRoot + "/Animations/AnimationsClips/Player";

        const string AnimationFolder = ProjectPaths.Root + "/Art/Animation";
        const string ControllerAsset = AnimationFolder + "/AC_SurvivalPlayer.controller";

        // Blend thresholds are in normalised speed, which PlayerMovement defines as
        // currentSpeed / sprintSpeed. Walking at 5 of a 9.5 sprint lands at 0.53.
        const float WalkThreshold = 0.53f;

        public static bool IsCharacterAvailable => AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefab) != null;

        [MenuItem("Survival/Setup/5 - Rebuild Character Animator", false, 24)]
        public static void RebuildAnimatorMenu()
        {
            AnimatorController controller = BuildAnimatorController();
            if (controller == null) return;

            Selection.activeObject = controller;
            Debug.Log($"[Survival] Animator controller built at {ControllerAsset}.");
        }

        /// <summary>
        /// Creates the animator controller from the pack's clips. Rebuilt from scratch each time
        /// rather than patched: it is generated, so the source of truth is this method.
        /// </summary>
        public static AnimatorController BuildAnimatorController()
        {
            AnimationClip idle = LoadClip("Idle");
            AnimationClip walk = LoadClip("Walking");
            AnimationClip run = LoadClip("Running");

            if (idle == null || walk == null || run == null)
            {
                Debug.LogError($"[Survival] Could not find Idle/Walking/Running clips under {ClipFolder}. " +
                               "Is the Stylized Wood Monsters pack still imported?");
                return null;
            }

            ProjectPaths.EnsureFolder(AnimationFolder);
            AssetDatabase.DeleteAsset(ControllerAsset);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerAsset);
            controller.AddParameter(PlayerAnimation.Parameters.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(PlayerAnimation.Parameters.Grounded, AnimatorControllerParameterType.Bool);
            controller.AddParameter(PlayerAnimation.Parameters.VerticalVelocity, AnimatorControllerParameterType.Float);

            AnimatorState state = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);

            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = PlayerAnimation.Parameters.Speed;
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, WalkThreshold);
            tree.AddChild(run, 1f);

            controller.layers[0].stateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>
        /// Puts the character model under <paramref name="modelParent"/>, scaled to match the
        /// capsule, with an Animator on it. Returns null when the pack is not present, so scene
        /// building still works on a machine that never imported it.
        /// </summary>
        public static GameObject TryInstantiateCharacter(Transform modelParent, float targetHeight)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefab);
            if (prefab == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, modelParent);
            instance.name = "Character";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            ScaleToHeight(instance, targetHeight);

            // Never use ?? or ?. on a UnityEngine.Object. They use C#'s real null, which ignores
            // Unity's overloaded == -- so a missing component comes back as a live-looking
            // reference and the fallback never runs. Only == and != know the difference.
            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.avatar = LoadAvatar();
            animator.runtimeAnimatorController = BuildAnimatorController();
            animator.applyRootMotion = false;   // the code owns movement, not the clips
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            return instance;
        }

        /// <summary>
        /// Measures the model and scales it to the capsule's height. Measured rather than
        /// hardcoded: the character then fits its collider whatever units it was authored in,
        /// and a different model can be dropped in without anyone editing a magic number.
        /// </summary>
        static void ScaleToHeight(GameObject instance, float targetHeight)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            if (bounds.size.y <= 0.0001f) return;

            float scale = targetHeight / bounds.size.y;
            instance.transform.localScale = Vector3.one * scale;

            Debug.Log($"[Survival] Character measured {bounds.size.y:F2} m, scaled by {scale:F3} " +
                      $"to match the {targetHeight:F2} m capsule.");
        }

        static Avatar LoadAvatar() =>
            AssetDatabase.LoadAllAssetsAtPath(CharacterModel).OfType<Avatar>().FirstOrDefault();

        /// <summary>Pulls a named clip out of its FBX, skipping Unity's preview clips.</summary>
        static AnimationClip LoadClip(string clipName)
        {
            string path = $"{ClipFolder}/{clipName}.fbx";

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__"))
                .ToArray();

            if (clips.Length == 0) return null;

            AnimationClip named = clips.FirstOrDefault(clip => clip.name == clipName);
            return named != null ? named : clips[0];
        }
    }
}
