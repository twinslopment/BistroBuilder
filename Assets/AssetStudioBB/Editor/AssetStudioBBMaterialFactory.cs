using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.AssetStudioBB.Editor
{
    internal static class AssetStudioBBMaterialFactory
    {
        public static Material CreateOrUpdate(
            string materialPath,
            string manifestPath,
            AssetStudioBBManifest.PresetData preset,
            AssetStudioBBManifest.RoleAssignment assignment)
        {
            if (preset == null)
                throw new InvalidOperationException($"No existe el preset '{assignment.preset}'.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("HDRP/Lit")
                    ?? Shader.Find("Standard");
                if (shader == null)
                    throw new InvalidOperationException("No existe un shader Lit compatible.");
                material = new Material(shader)
                {
                    name = AssetStudioBBPaths.FileNameWithoutExtension(materialPath),
                    enableInstancing = true,
                };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.name = AssetStudioBBPaths.FileNameWithoutExtension(materialPath);
            material.enableInstancing = true;
            var tint = assignment.TintColor();
            var baseTexture = LoadTexture(manifestPath, preset.textures.baseColor);
            var normalTexture = LoadTexture(manifestPath, preset.textures.normal);
            var maskTexture = LoadTexture(manifestPath, preset.textures.mask);
            var aoTexture = LoadTexture(manifestPath, preset.textures.ao);
            var scale = new Vector2(Mathf.Max(0.01f, preset.textureScale), Mathf.Max(0.01f, preset.textureScale));

            SetColor(material, tint);
            SetTexture(material, "_BaseMap", "_MainTex", baseTexture, scale);
            SetTexture(material, "_BumpMap", "_BumpMap", normalTexture, scale);
            if (normalTexture != null)
            {
                material.EnableKeyword("_NORMALMAP");
                if (material.HasProperty("_BumpScale"))
                    material.SetFloat("_BumpScale", Mathf.Max(0f, preset.normalScale));
            }

            SetTexture(material, "_MetallicGlossMap", "_MetallicGlossMap", maskTexture, scale);
            if (maskTexture != null)
            {
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 1f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f);
            }
            else
            {
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", Mathf.Clamp01(preset.metallic));
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f - Mathf.Clamp01(preset.roughness));
            }

            SetTexture(material, "_OcclusionMap", "_OcclusionMap", aoTexture, scale);
            if (aoTexture != null)
            {
                material.EnableKeyword("_OCCLUSIONMAP");
                if (material.HasProperty("_OcclusionStrength")) material.SetFloat("_OcclusionStrength", 1f);
            }

            if (string.Equals(preset.surfaceType, "transparent", StringComparison.OrdinalIgnoreCase))
                ConfigureTransparent(material, tint, preset);
            else
                ConfigureOpaque(material);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D LoadTexture(string manifestPath, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative))
                return null;
            var path = AssetStudioBBPaths.AbsoluteAssetPath(manifestPath, relative);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static void SetTexture(Material material, string preferredProperty, string fallbackProperty, Texture texture, Vector2 scale)
        {
            var property = material.HasProperty(preferredProperty) ? preferredProperty : fallbackProperty;
            if (!material.HasProperty(property))
                return;
            material.SetTexture(property, texture);
            material.SetTextureScale(property, scale);
        }

        private static void ConfigureOpaque(Material material)
        {
            material.renderQueue = -1;
            material.SetOverrideTag("RenderType", "Opaque");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHABLEND_ON");
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
        }

        private static void ConfigureTransparent(Material material, Color tint, AssetStudioBBManifest.PresetData preset)
        {
            tint.a = Mathf.Clamp01(preset.alpha > 0f ? preset.alpha : tint.a);
            SetColor(material, tint);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        }
    }
}
