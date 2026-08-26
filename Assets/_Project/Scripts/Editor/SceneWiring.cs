using UnityEditor;
using UnityEngine;

namespace Survival.EditorTools
{
    /// <summary>Shared helpers for scenes that are built by script rather than by hand.</summary>
    public static class SceneWiring
    {
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

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(assetPath) };

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);

            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }
    }
}
