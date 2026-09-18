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
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return published;
        }
    }
}