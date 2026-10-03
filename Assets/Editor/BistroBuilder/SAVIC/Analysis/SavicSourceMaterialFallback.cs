using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicSourceMaterialFallback
    {
        internal static bool NeedsFallback(Material material) => material == null || material.shader == null ||
            !material.shader.isSupported || material.shader.name == "Hidden/InternalErrorShader";

        internal static int CountInvalidSlots(GameObject root)
        {
            int count = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    if (NeedsFallback(material)) count++;
            return count;
        }

        internal static int ReplaceInvalidSlots(GameObject root, Material neutral)
        {
            int count = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0; index < materials.Length; index++)
                {
                    if (!NeedsFallback(materials[index])) continue;
                    if (neutral == null)
                        throw new InvalidOperationException("A source material is missing and no managed neutral material is available.");
                    materials[index] = neutral;
                    changed = true;
                    count++;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
            return count;
        }

        internal static Material GetOrCreateNeutral(string path, string displayName)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (NeedsFallback(existing)) throw new InvalidOperationException("Managed neutral material has no usable shader.");
                return existing;
            }
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Neutral material path is occupied by another asset.");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("URP Lit shader is unavailable for a neutral material.");
            Material material = new Material(shader) { name = displayName };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.72f, 0.73f, 0.74f, 1f));
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
