using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes.Editor
{
    internal static class FurnitureFinishPublisher
    {
        private const string PublishedRoot = "Assets/Generated/FurnitureFinishes/Published";

        public static FurnitureFinishPublishedSet Publish(FurnitureFinishProfile profile)
        {
            var validation = FurnitureFinishValidator.ValidateForPublish(profile);
            if (FurnitureFinishValidator.HasErrors(validation))
                throw new InvalidOperationException("La publicación está bloqueada por errores de validación.");

            EnsureMissingThumbnails(profile);

            var folder = $"{PublishedRoot}/{profile.FurnitureId}";
            FurnitureFinishAssetUtility.EnsureFolder(folder);
            var path = $"{folder}/{profile.FurnitureId}_FurnitureFinishSet.asset";
            var published = AssetDatabase.LoadAssetAtPath<FurnitureFinishPublishedSet>(path);
            if (published == null)
            {
                published = ScriptableObject.CreateInstance<FurnitureFinishPublishedSet>();
                AssetDatabase.CreateAsset(published, path);
            }

            var variants = new List<FurnitureFinishPublishedSet.PublishedVariant>();
            foreach (var variant in profile.Variants)
            {
                if (variant == null)
                    continue;
                var bindings = new List<FurnitureFinishPublishedSet.PublishedBinding>();
                foreach (var zone in profile.Zones)
                {
                    if (zone == null)
                        continue;
                    var finish = variant.FindFinish(zone.Id);

                    foreach (var slot in zone.Slots)
                    {
                        var renderer = FurnitureFinishAssetUtility.FindRenderer(
                            profile.SourceAsset,
                            slot.RendererPath);
                        var materials = renderer != null
                            ? renderer.sharedMaterials
                            : Array.Empty<Material>();
                        var sourceMaterial = slot.MaterialIndex >= 0
                            && slot.MaterialIndex < materials.Length
                                ? materials[slot.MaterialIndex]
                                : null;
                        var effective = finish != null
                            ? finish.Material
                            : sourceMaterial;

                        bindings.Add(new FurnitureFinishPublishedSet.PublishedBinding(
                            slot.RendererPath,
                            slot.MaterialIndex,
                            effective));
                    }
                }

                variants.Add(new FurnitureFinishPublishedSet.PublishedVariant(
                    variant.Id,
                    variant.DisplayName,
                    variant.Thumbnail,
                    bindings.ToArray()));
            }

            var defaultId = string.IsNullOrWhiteSpace(profile.DefaultVariantId)
                ? variants[0].Id
                : profile.DefaultVariantId;
            published.EditorConfigure(
                profile.FurnitureId,
                profile.SourceAsset,
                defaultId,
                variants.ToArray());
            EditorUtility.SetDirty(published);
            UpdateRegistry(profile.FurnitureId, published);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return published;
        }

        private static void EnsureMissingThumbnails(FurnitureFinishProfile profile)
        {
            if (profile == null)
                return;

            var dirty = false;
            foreach (var variant in profile.Variants)
            {
                if (variant == null || variant.Thumbnail != null)
                    continue;
                variant.EditorSetThumbnail(
                    FurnitureFinishThumbnailGenerator.Generate(profile, variant));
                dirty = true;
            }

            if (dirty)
                EditorUtility.SetDirty(profile);
        }

        private static void UpdateRegistry(
            string furnitureId,
            FurnitureFinishPublishedSet published)
        {
            const string registryFolder = "Assets/Generated/FurnitureFinishes";
            const string registryPath =
                registryFolder + "/FurnitureFinishRegistry.asset";

            FurnitureFinishAssetUtility.EnsureFolder(registryFolder);
            var registry = AssetDatabase.LoadAssetAtPath<FurnitureFinishRegistry>(
                registryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<FurnitureFinishRegistry>();
                registry.EditorConfigure(
                    Array.Empty<FurnitureFinishRegistry.Entry>());
                AssetDatabase.CreateAsset(registry, registryPath);
            }

            var entries = new List<FurnitureFinishRegistry.Entry>();
            var replaced = false;
            foreach (var entry in registry.Entries)
            {
                if (entry != null
                    && string.Equals(
                        entry.FurnitureId,
                        furnitureId,
                        StringComparison.Ordinal))
                {
                    entries.Add(
                        new FurnitureFinishRegistry.Entry(
                            furnitureId,
                            published));
                    replaced = true;
                }
                else if (entry != null)
                {
                    entries.Add(entry);
                }
            }

            if (!replaced)
            {
                entries.Add(
                    new FurnitureFinishRegistry.Entry(
                        furnitureId,
                        published));
            }

            registry.EditorConfigure(entries.ToArray());
            EditorUtility.SetDirty(registry);
        }
    }
}