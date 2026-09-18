using System;
using System.Collections.Generic;
using BistroBuilder.AssetStudioBB;
using BistroBuilder.SmartAssets;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes.Editor
{
    internal static class FurnitureFinishExistingPipelineImporter
    {
        public static bool CanImport(UnityEngine.Object selected)
        {
            return selected is AssetStudioBBVariantSet
                || selected is BistroBuilderSmartAssetVariantSet;
        }

        public static FurnitureFinishProfile Import(
            UnityEngine.Object selected,
            FurnitureFinishLibrary library)
        {
            if (library == null)
                throw new InvalidOperationException("No hay biblioteca de acabados.");

            if (selected is AssetStudioBBVariantSet assetStudioSet)
                return ImportAssetStudio(assetStudioSet, library);

            if (selected is BistroBuilderSmartAssetVariantSet smartSet)
                return ImportSmartAssets(smartSet, library);

            throw new InvalidOperationException(
                "Selecciona un Variant Set de Asset Studio BB o Smart Assets.");
        }

        private static FurnitureFinishProfile ImportSmartAssets(
            BistroBuilderSmartAssetVariantSet set,
            FurnitureFinishLibrary library)
        {
            if (set == null)
                throw new InvalidOperationException("Smart Asset Variant Set nulo.");

            var source = set.SourceModel;
            if (source == null)
            {
                foreach (var variant in set.Variants)
                {
                    if (variant != null && variant.VisualPrefab != null)
                    {
                        source = variant.VisualPrefab;
                        break;
                    }
                }
            }
            if (source == null)
                throw new InvalidOperationException("Smart Assets no contiene geometría fuente.");

            var slots = CollectAllSlots(source);
            if (slots.Count == 0)
                throw new InvalidOperationException("El Smart Asset no contiene Material Slots.");

            var families = new List<FurnitureSurfaceFamily>();
            var variants = new List<FurnitureFinishProfile.VariantDefinition>();
            foreach (var variant in set.Variants)
            {
                if (variant == null || variant.Material == null)
                    continue;

                var family = InferOrOther(
                    variant.Material,
                    $"{variant.Id} {variant.DisplayName}");
                AddUnique(families, family);

                var finish = FurnitureFinishProfileFactory.AddFinish(
                    library,
                    variant.Material,
                    $"{family}_{variant.Id}",
                    variant.DisplayName,
                    family);
                variants.Add(new FurnitureFinishProfile.VariantDefinition(
                    FurnitureFinishAssetUtility.StableId(variant.Id, "variant"),
                    variant.DisplayName,
                    new[]
                    {
                        new FurnitureFinishProfile.ZoneFinishBinding("surface", finish)
                    }));
            }

            if (families.Count == 0)
                families.Add(FurnitureSurfaceFamily.Other);

            var zone = new FurnitureFinishProfile.ZoneDefinition(
                "surface",
                "Superficie",
                families[0],
                families.ToArray(),
                true,
                FurnitureFinishChannel.None,
                slots.ToArray());

            var defaultId = ResolveDefaultSmartVariant(set, variants);
            return FurnitureFinishProfileFactory.CreateOrUpdateProfile(
                set.AssetId,
                source.name,
                source,
                new[] { zone },
                variants.ToArray(),
                defaultId);
        }

        private static FurnitureFinishProfile ImportAssetStudio(
            AssetStudioBBVariantSet set,
            FurnitureFinishLibrary library)
        {
            if (set == null)
                throw new InvalidOperationException("Asset Studio BB Variant Set nulo.");

            var defaultEntry = ResolveDefaultAssetStudioEntry(set);
            if (defaultEntry == null || defaultEntry.VisualPrefab == null)
                throw new InvalidOperationException(
                    "Asset Studio BB no contiene una variante visual utilizable.");

            var source = defaultEntry.VisualPrefab;
            var zoneBuilders = BuildAssetStudioZones(set, defaultEntry);
            if (zoneBuilders.Count == 0)
                throw new InvalidOperationException(
                    "No se pudieron recuperar roles/material slots de Asset Studio BB.");

            var variants = new List<FurnitureFinishProfile.VariantDefinition>();
            foreach (var variant in set.Variants)
            {
                if (variant == null || variant.VisualPrefab == null)
                    continue;

                var bindings = new List<FurnitureFinishProfile.ZoneFinishBinding>();
                foreach (var pair in zoneBuilders)
                {
                    var material = ResolveZoneMaterial(variant.VisualPrefab, pair.Value.Slots);
                    if (material == null)
                        continue;

                    var family = InferOrOther(material, pair.Key);
                    pair.Value.AddFamily(family);

                    var finish = FurnitureFinishProfileFactory.AddFinish(
                        library,
                        material,
                        $"{pair.Key}_{family}_{material.name}",
                        HumanizeMaterial(material, pair.Key),
                        family);
                    bindings.Add(new FurnitureFinishProfile.ZoneFinishBinding(
                        pair.Key,
                        finish));
                }

                variants.Add(new FurnitureFinishProfile.VariantDefinition(
                    FurnitureFinishAssetUtility.StableId(variant.Id, "variant"),
                    variant.DisplayName,
                    bindings.ToArray()));
            }

            var zones = new List<FurnitureFinishProfile.ZoneDefinition>();
            foreach (var pair in zoneBuilders)
            {
                var families = pair.Value.Families.Count > 0
                    ? pair.Value.Families.ToArray()
                    : new[] { FurnitureSurfaceFamily.Other };
                zones.Add(new FurnitureFinishProfile.ZoneDefinition(
                    pair.Key,
                    Humanize(pair.Key),
                    families[0],
                    families,
                    true,
                    FurnitureFinishChannel.None,
                    pair.Value.Slots.ToArray()));
            }
            zones.Sort((left, right) =>
                string.Compare(left.Id, right.Id, StringComparison.Ordinal));

            var defaultId = !string.IsNullOrWhiteSpace(set.DefaultVariantId)
                ? FurnitureFinishAssetUtility.StableId(set.DefaultVariantId, variants[0].Id)
                : variants[0].Id;

            return FurnitureFinishProfileFactory.CreateOrUpdateProfile(
                set.AssetId,
                string.IsNullOrWhiteSpace(set.DisplayName) ? source.name : set.DisplayName,
                source,
                zones.ToArray(),
                variants.ToArray(),
                defaultId);
        }

        private static Dictionary<string, ZoneBuilder> BuildAssetStudioZones(
            AssetStudioBBVariantSet set,
            AssetStudioBBVariantSet.VariantEntry defaultEntry)
        {
            var result = new Dictionary<string, ZoneBuilder>(StringComparer.Ordinal);
            var source = defaultEntry.VisualPrefab;
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    var role = ParseAssetStudioRole(
                        set.AssetId,
                        defaultEntry.Id,
                        materials[index],
                        renderer,
                        index);
                    if (!result.TryGetValue(role, out var builder))
                    {
                        builder = new ZoneBuilder();
                        result.Add(role, builder);
                    }

                    builder.Slots.Add(new FurnitureFinishProfile.SlotBinding(
                        FurnitureFinishAssetUtility.TransformPath(
                            source.transform,
                            renderer.transform),
                        index));
                }
            }
            return result;
        }

        private static string ParseAssetStudioRole(
            string assetId,
            string variantId,
            Material material,
            Renderer renderer,
            int index)
        {
            var name = material != null ? material.name ?? string.Empty : string.Empty;
            var prefix = $"MAT_{assetId}_{variantId}_";
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                var role = name.Substring(prefix.Length);
                var instanceSuffix = role.IndexOf(" (Instance)", StringComparison.Ordinal);
                if (instanceSuffix >= 0)
                    role = role.Substring(0, instanceSuffix);
                return FurnitureFinishAssetUtility.StableId(role, "surface");
            }

            return FurnitureFinishAssetUtility.SemanticRole(material, renderer, index);
        }

        private static Material ResolveZoneMaterial(
            GameObject variantPrefab,
            IReadOnlyList<FurnitureFinishProfile.SlotBinding> slots)
        {
            Material selected = null;
            foreach (var slot in slots)
            {
                var renderer = FurnitureFinishAssetUtility.FindRenderer(
                    variantPrefab,
                    slot.RendererPath);
                if (renderer == null)
                    continue;
                var materials = renderer.sharedMaterials;
                if (slot.MaterialIndex < 0 || slot.MaterialIndex >= materials.Length)
                    continue;

                var material = materials[slot.MaterialIndex];
                if (material == null)
                    continue;
                if (selected == null)
                    selected = material;
                else if (!string.Equals(
                    FurnitureFinishAssetUtility.MaterialFingerprint(selected),
                    FurnitureFinishAssetUtility.MaterialFingerprint(material),
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Una misma zona contiene materiales distintos en '{variantPrefab.name}'. " +
                        "Divide esa zona antes de importar.");
                }
            }
            return selected;
        }

        private static List<FurnitureFinishProfile.SlotBinding> CollectAllSlots(GameObject source)
        {
            var result = new List<FurnitureFinishProfile.SlotBinding>();
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    result.Add(new FurnitureFinishProfile.SlotBinding(
                        FurnitureFinishAssetUtility.TransformPath(
                            source.transform,
                            renderer.transform),
                        index));
                }
            }
            return result;
        }

        private static AssetStudioBBVariantSet.VariantEntry ResolveDefaultAssetStudioEntry(
            AssetStudioBBVariantSet set)
        {
            AssetStudioBBVariantSet.VariantEntry first = null;
            foreach (var variant in set.Variants)
            {
                if (variant == null)
                    continue;
                if (first == null)
                    first = variant;
                if (!string.IsNullOrWhiteSpace(set.DefaultVariantId)
                    && string.Equals(
                        variant.Id,
                        set.DefaultVariantId,
                        StringComparison.Ordinal))
                    return variant;
            }
            return first;
        }

        private static string ResolveDefaultSmartVariant(
            BistroBuilderSmartAssetVariantSet set,
            IReadOnlyList<FurnitureFinishProfile.VariantDefinition> imported)
        {
            if (!string.IsNullOrWhiteSpace(set.DefaultVariantId))
                return FurnitureFinishAssetUtility.StableId(
                    set.DefaultVariantId,
                    imported.Count > 0 ? imported[0].Id : string.Empty);
            return imported.Count > 0 ? imported[0].Id : string.Empty;
        }

        private static FurnitureSurfaceFamily InferOrOther(
            Material material,
            string hint)
        {
            var family = FurnitureFinishAssetUtility.InferFamily(
                material,
                hint,
                out var confident);
            return confident && family != FurnitureSurfaceFamily.Unknown
                ? family
                : FurnitureSurfaceFamily.Other;
        }

        private static string Humanize(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return "Zona";
            var text = id.Replace('_', ' ').Trim();
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private static string HumanizeMaterial(Material material, string role)
        {
            var name = material != null ? material.name : role;
            if (string.IsNullOrWhiteSpace(name))
                return Humanize(role);
            return name.Replace("MAT_", string.Empty).Replace('_', ' ').Trim();
        }

        private static void AddUnique(
            ICollection<FurnitureSurfaceFamily> families,
            FurnitureSurfaceFamily family)
        {
            foreach (var existing in families)
            {
                if (existing == family)
                    return;
            }
            families.Add(family);
        }

        private sealed class ZoneBuilder
        {
            public List<FurnitureFinishProfile.SlotBinding> Slots { get; } =
                new List<FurnitureFinishProfile.SlotBinding>();
            public List<FurnitureSurfaceFamily> Families { get; } =
                new List<FurnitureSurfaceFamily>();

            public void AddFamily(FurnitureSurfaceFamily family)
            {
                AddUnique(Families, family);
            }
        }
    }
}