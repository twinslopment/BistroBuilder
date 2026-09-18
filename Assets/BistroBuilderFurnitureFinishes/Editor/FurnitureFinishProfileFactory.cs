using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes.Editor
{
    internal static class FurnitureFinishProfileFactory
    {
        private const string ProfileFolder = "Assets/Data/FurnitureFinishes/Profiles";
        private const string LibraryFolder = "Assets/Data/FurnitureFinishes";
        private const string DefaultLibraryPath = LibraryFolder + "/FurnitureFinishLibrary.asset";

        public static FurnitureFinishProfile CreateFromSelection(UnityEngine.Object selected)
        {
            var source = selected as GameObject;
            if (source == null)
                throw new InvalidOperationException("Selecciona un FBX o prefab de mobiliario.");

            var assetPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new InvalidOperationException("La selección debe ser un asset guardado en el proyecto.");

            var renderers = source.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("El asset no contiene renderers.");

            var groups = new Dictionary<string, ZoneBuilder>(StringComparer.Ordinal);
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    var role = FurnitureFinishAssetUtility.SemanticRole(materials[materialIndex], renderer, materialIndex);
                    if (!groups.TryGetValue(role, out var builder))
                    {
                        var family = FurnitureFinishAssetUtility.InferFamily(role, out var confident);
                        builder = new ZoneBuilder(role, family, confident);
                        groups.Add(role, builder);
                    }

                    builder.Slots.Add(new FurnitureFinishProfile.SlotBinding(
                        FurnitureFinishAssetUtility.TransformPath(source.transform, renderer.transform),
                        materialIndex));
                }
            }

            var zones = new List<FurnitureFinishProfile.ZoneDefinition>();
            foreach (var pair in groups)
            {
                zones.Add(new FurnitureFinishProfile.ZoneDefinition(
                    pair.Key,
                    Humanize(pair.Key),
                    pair.Value.Family,
                    pair.Value.Confirmed,
                    FurnitureFinishChannel.None,
                    pair.Value.Slots.ToArray()));
            }
            zones.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));

            var furnitureId = FurnitureFinishAssetUtility.StableId(source.name, "furniture");
            return CreateOrUpdateProfile(
                furnitureId,
                source.name,
                source,
                zones.ToArray(),
                Array.Empty<FurnitureFinishProfile.VariantDefinition>(),
                string.Empty);
        }

        public static FurnitureFinishLibrary LoadOrCreateDefaultLibrary()
        {
            FurnitureFinishAssetUtility.EnsureFolder(LibraryFolder);
            var library = AssetDatabase.LoadAssetAtPath<FurnitureFinishLibrary>(DefaultLibraryPath);
            if (library != null)
                return library;

            library = ScriptableObject.CreateInstance<FurnitureFinishLibrary>();
            library.EditorConfigure(Array.Empty<FurnitureFinishDefinition>());
            AssetDatabase.CreateAsset(library, DefaultLibraryPath);
            AssetDatabase.SaveAssets();
            return library;
        }

        public static FurnitureFinishProfile CreateOrUpdateProfile(
            string furnitureId,
            string displayName,
            GameObject source,
            FurnitureFinishProfile.ZoneDefinition[] zones,
            FurnitureFinishProfile.VariantDefinition[] variants,
            string defaultVariantId)
        {
            if (source == null)
                throw new InvalidOperationException("El perfil necesita un asset fuente.");

            FurnitureFinishAssetUtility.EnsureFolder(ProfileFolder);
            var stableId = FurnitureFinishAssetUtility.StableId(furnitureId, source.name);
            var profilePath = $"{ProfileFolder}/{stableId}_FurnitureFinishProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<FurnitureFinishProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<FurnitureFinishProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            profile.EditorConfigure(
                stableId,
                string.IsNullOrWhiteSpace(displayName) ? source.name : displayName,
                source,
                zones ?? Array.Empty<FurnitureFinishProfile.ZoneDefinition>(),
                variants ?? Array.Empty<FurnitureFinishProfile.VariantDefinition>(),
                defaultVariantId ?? string.Empty);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(profile);
            return profile;
        }

        public static FurnitureFinishDefinition FindReusableFinish(
            FurnitureFinishLibrary library,
            Material material,
            FurnitureSurfaceFamily family)
        {
            if (library == null || material == null)
                return null;

            var fingerprint = FurnitureFinishAssetUtility.MaterialFingerprint(material);
            foreach (var finish in library.Finishes)
            {
                if (finish == null || finish.Material == null)
                    continue;
                if (finish.Material == material)
                    return finish;
                if (finish.Family == family
                    && string.Equals(
                        FurnitureFinishAssetUtility.MaterialFingerprint(finish.Material),
                        fingerprint,
                        StringComparison.Ordinal))
                    return finish;
            }
            return null;
        }

        public static FurnitureFinishDefinition AddFinish(
            FurnitureFinishLibrary library,
            Material material,
            string id,
            string displayName,
            FurnitureSurfaceFamily family)
        {
            if (library == null)
                throw new InvalidOperationException("No hay biblioteca de acabados.");
            if (material == null)
                throw new InvalidOperationException("Selecciona un material válido.");

            var reusable = FindReusableFinish(library, material, family);
            if (reusable != null)
                return reusable;

            var finishFolder = LibraryFolder + "/Library";
            FurnitureFinishAssetUtility.EnsureFolder(finishFolder);
            var stableId = FurnitureFinishAssetUtility.StableId(id, material.name);
            var path = $"{finishFolder}/{stableId}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<FurnitureFinishDefinition>(path);
            if (definition != null
                && definition.Material != null
                && !string.Equals(
                    FurnitureFinishAssetUtility.MaterialFingerprint(definition.Material),
                    FurnitureFinishAssetUtility.MaterialFingerprint(material),
                    StringComparison.Ordinal))
            {
                stableId = FurnitureFinishAssetUtility.StableId(
                    $"{stableId}_{material.name}",
                    stableId + "_variant");
                path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{finishFolder}/{stableId}.asset");
                definition = null;
            }

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<FurnitureFinishDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.EditorConfigure(
                stableId,
                string.IsNullOrWhiteSpace(displayName) ? material.name : displayName,
                family,
                material,
                FurnitureFinishAssetUtility.DetectProvidedChannels(material),
                Array.Empty<string>());
            EditorUtility.SetDirty(definition);

            var entries = new List<FurnitureFinishDefinition>(library.Finishes);
            if (!entries.Contains(definition))
                entries.Add(definition);
            library.EditorConfigure(entries.ToArray());
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return definition;
        }

        private static string Humanize(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return "Zona";
            var text = id.Replace('_', ' ').Trim();
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private sealed class ZoneBuilder
        {
            public FurnitureSurfaceFamily Family { get; }
            public bool Confirmed { get; }
            public List<FurnitureFinishProfile.SlotBinding> Slots { get; } =
                new List<FurnitureFinishProfile.SlotBinding>();

            public ZoneBuilder(FurnitureSurfaceFamily family)
            {
                Family = family;
            }

            public ZoneBuilder(string id, FurnitureSurfaceFamily family, bool confirmed)
            {
                Family = family;
                Confirmed = confirmed;
            }
        }
    }
}