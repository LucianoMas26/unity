using UnityEditor;
using UnityEngine;

namespace Survival.EditorTools
{
    /// <summary>Shared helpers for scenes that are built by script rather than by hand.</summary>
    public static class SceneWiring
    {
        /// <summary>
        /// Refuses to build a scene while the game is running. Unity throws from
        /// EditorSceneManager the moment you try, and the builder dies halfway through -- after
        /// the importer has already run and logged its success, which makes it look like the
        /// whole thing worked. Fail first, and say why.
        /// </summary>
        public static bool RefuseDuringPlayMode(string what)
        {
            if (!EditorApplication.isPlaying) return false;

            Debug.LogError($"[Survival] Sal del modo Play antes de construir '{what}'. " +
                           "Unity no permite reemplazar la escena mientras el juego corre.");
            return true;
        }

        /// <summary>
        /// Assigns a serialised reference and confirms it actually stuck. Unity stores null in
        /// silence when the value's native object has been destroyed, and a scene wired up by
        /// script has no human looking at the Inspector to notice. Fail loudly here rather than
        /// at runtime, three systems away from the cause.
        /// </summary>
        public static void AssignReference(SerializedObject serialized, string propertyPath, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyPath);
            string owner = serialized.targetObject.GetType().Name;

            if (property == null)
            {
                Debug.LogError($"[Survival] {owner} has no serialized field '{propertyPath}'. " +
                               "The field was probably renamed without updating the setup script.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();

            if (value != null && serialized.FindProperty(propertyPath).objectReferenceValue == null)
                Debug.LogError($"[Survival] Could not assign '{propertyPath}' on {owner}. " +
                               $"The value '{value.name}' exists in managed code but its native " +
                               "object is gone -- it was most likely unloaded when the scene changed.");
        }

        /// <summary>Creates an unlit-ish coloured URP material for placeholder geometry.</summary>
        public static Material CreateColorMaterial(string assetPath, Color color, float smoothness = 0.1f)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null) return existing;

            // == rather than ??: Unity's null is not C#'s null. See CharacterSetup for the bite.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(assetPath) };

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);

            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }
    }
}
