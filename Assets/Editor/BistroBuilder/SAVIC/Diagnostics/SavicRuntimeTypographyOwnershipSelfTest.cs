using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicRuntimeTypographyOwnershipSelfTest
    {
        public static void RunFromCommandLine()
        {
            const string path = "Assets/Resources/BistroBuilder/UI/Typography/Recoleta-SDF.asset";
            var source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            Require(source != null && source.atlasTextures.Length > 0 && source.material != null, "Actual title font is unavailable.");
            string hash = AssetDatabase.GetAssetDependencyHash(path).ToString();
            var material = source.material;
            var texture = source.atlasTextures[0];
            TMP_FontAsset clone = null;
            try
            {
                clone = BistroBuilderTypography.CloneForRuntime(source);
                Require(clone != source && !EditorUtility.IsPersistent(clone) && clone.material != material &&
                    !EditorUtility.IsPersistent(clone.material) && clone.atlasTexture != texture &&
                    clone.material.mainTexture == clone.atlasTexture && clone.characterTable.Count == source.characterTable.Count,
                    "Runtime font does not own its atlas/material or changed the glyph table.");
                for (int i = 0; i < source.atlasTextures.Length; i++)
                    Require(clone.atlasTextures[i] != source.atlasTextures[i] && !EditorUtility.IsPersistent(clone.atlasTextures[i]) &&
                        clone.atlasTextures[i].width == source.atlasTextures[i].width && clone.atlasTextures[i].height == source.atlasTextures[i].height,
                        "A runtime atlas still belongs to the persistent font.");
                Color[] originalPixels = Snapshot(texture), copiedPixels = Snapshot(clone.atlasTexture);
                for (int i = 0; i < originalPixels.Length; i++)
                    Require(Vector4.Distance(originalPixels[i], copiedPixels[i]) < 0.0001f, "Runtime atlas pixels changed while cloning.");
                var ownedMaterial = clone.material;
                var ownedTexture = clone.atlasTexture;
                Object.DestroyImmediate(clone); clone = null;
                Require(ownedMaterial == null && ownedTexture == null && material != null && texture != null &&
                    EditorUtility.IsPersistent(material) && EditorUtility.IsPersistent(texture) &&
                    AssetDatabase.GetAssetDependencyHash(path).ToString() == hash,
                    "Runtime destruction lost persistent font resources or left owned resources behind.");
                Debug.Log("[SAVIC] RUNTIME TYPOGRAPHY OWNERSHIP SELF-TEST - PASS: real persistent title font, independent atlas/material, matching GPU pixels/glyphs, owned cleanup and unchanged source.");
            }
            finally { if (clone != null) Object.DestroyImmediate(clone); }
        }
        private static Color[] Snapshot(Texture source)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(32, 32, 0, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(source, target); RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); readback.Apply(); return readback.GetPixels();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(readback); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
