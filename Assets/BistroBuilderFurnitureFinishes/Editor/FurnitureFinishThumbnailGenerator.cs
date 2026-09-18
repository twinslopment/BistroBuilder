using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes.Editor
{
    internal static class FurnitureFinishThumbnailGenerator
    {
        private const string ThumbnailRoot =
            "Assets/Generated/FurnitureFinishes/Thumbnails";
        private const int ThumbnailSize = 384;

        public static int GenerateAll(FurnitureFinishProfile profile)
        {
            if (profile == null || profile.SourceAsset == null)
                throw new InvalidOperationException("El perfil no tiene asset fuente.");

            var generated = 0;
            foreach (var variant in profile.Variants)
            {
                if (variant == null)
                    continue;

                var thumbnail = Generate(profile, variant);
                variant.EditorSetThumbnail(thumbnail);
                generated++;
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return generated;
        }

        public static Texture2D Generate(
            FurnitureFinishProfile profile,
            FurnitureFinishProfile.VariantDefinition variant)
        {
            if (profile == null || profile.SourceAsset == null || variant == null)
                throw new InvalidOperationException("Perfil o variante inválidos.");

            var folder = $"{ThumbnailRoot}/{profile.FurnitureId}";
            FurnitureFinishAssetUtility.EnsureFolder(folder);
            var path = $"{folder}/{variant.Id}.png";

            var preview = new PreviewRenderUtility();
            try
            {
                preview.cameraFieldOfView = 30f;
                preview.lights[0].intensity = 1.15f;
                preview.lights[0].transform.rotation =
                    Quaternion.Euler(40f, 35f, 0f);
                preview.lights[1].intensity = 0.55f;
                preview.lights[1].transform.rotation =
                    Quaternion.Euler(330f, 215f, 0f);
                preview.ambientColor = new Color(0.34f, 0.34f, 0.34f, 1f);

                var instance = UnityEngine.Object.Instantiate(profile.SourceAsset);
                try
                {
                    instance.hideFlags = HideFlags.HideAndDontSave;
                    instance.transform.SetPositionAndRotation(
                        Vector3.zero,
                        Quaternion.identity);
                    ApplyVariant(instance, profile, variant);
                    preview.AddSingleGO(instance);

                    var bounds = CalculateBounds(instance);
                    ConfigureCamera(preview, bounds);

                    preview.BeginStaticPreview(
                        new Rect(0f, 0f, ThumbnailSize, ThumbnailSize));
                    preview.camera.Render();
                    var texture = preview.EndStaticPreview();
                    if (texture == null)
                        throw new InvalidOperationException(
                            $"No se pudo renderizar la miniatura de '{variant.Id}'.");

                    try
                    {
                        var absolutePath = Path.GetFullPath(path);
                        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            finally
            {
                preview.Cleanup();
            }

            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.maxTextureSize = ThumbnailSize;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void ApplyVariant(
            GameObject root,
            FurnitureFinishProfile profile,
            FurnitureFinishProfile.VariantDefinition variant)
        {
            foreach (var zone in profile.Zones)
            {
                if (zone == null)
                    continue;

                var finish = variant.FindFinish(zone.Id);
                if (finish == null || finish.Material == null)
                    continue;

                foreach (var slot in zone.Slots)
                {
                    var renderer = FurnitureFinishAssetUtility.FindRenderer(
                        root,
                        slot.RendererPath);
                    if (renderer == null)
                        continue;

                    var materials = renderer.sharedMaterials;
                    if (slot.MaterialIndex < 0
                        || slot.MaterialIndex >= materials.Length)
                        continue;

                    materials[slot.MaterialIndex] = finish.Material;
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException(
                    "El asset no contiene renderers para miniatura.");

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static void ConfigureCamera(
            PreviewRenderUtility preview,
            Bounds bounds)
        {
            var rotation = Quaternion.Euler(18f, 135f, 0f);
            var radius = Mathf.Max(0.15f, bounds.extents.magnitude);
            var distance = Mathf.Max(radius * 2.8f, 0.6f);
            preview.camera.transform.position =
                bounds.center - rotation * Vector3.forward * distance;
            preview.camera.transform.rotation = rotation;
            preview.camera.nearClipPlane = Mathf.Max(
                0.01f,
                distance - radius * 2.3f);
            preview.camera.farClipPlane = distance + radius * 4f;
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor =
                new Color(0.16f, 0.17f, 0.18f, 1f);
        }
    }
}