using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BistroBuilder.AssetStudioBB;

namespace BistroBuilder.AssetStudioBB.Editor
{
    internal static class AssetStudioBBVariantGenerator
    {
        private const string RolePrefix = "ASBB_role_";

        public static AssetStudioBBVariantSet Generate(
            string manifestPath,
            AssetStudioBBManifest manifest)
        {
            var validation = AssetStudioBBValidator.Validate(manifestPath, manifest);
            if (AssetStudioBBValidator.HasErrors(validation))
                throw new InvalidOperationException("La generación está bloqueada por errores de validación.");

            var assetRoot = AssetStudioBBPaths.AssetRootFromManifest(manifestPath);
            var generatedRoot = AssetStudioBBPaths.Combine(assetRoot, "Generated");
            var materialsFolder = AssetStudioBBPaths.Combine(generatedRoot, "Materials");
            var prefabsFolder = AssetStudioBBPaths.Combine(generatedRoot, "VisualVariants");
            AssetStudioBBPaths.EnsureFolder(generatedRoot);
            AssetStudioBBPaths.EnsureFolder(materialsFolder);
            AssetStudioBBPaths.EnsureFolder(prefabsFolder);

            var lodModels = LoadLodModels(manifestPath, manifest);
            var entries = new List<AssetStudioBBVariantSet.VariantEntry>();
            foreach (var variant in manifest.variants ?? Array.Empty<AssetStudioBBManifest.VariantData>())
            {
                if (variant == null || string.IsNullOrWhiteSpace(variant.id))
                    continue;

                var roleMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);
                foreach (var assignment in variant.roles ?? Array.Empty<AssetStudioBBManifest.RoleAssignment>())
                {
                    if (assignment == null || string.IsNullOrWhiteSpace(assignment.role) || assignment.preset == "none")
                        continue;
                    var preset = manifest.FindPreset(assignment.preset);
                    var materialPath = AssetStudioBBPaths.Combine(
                        materialsFolder,
                        $"MAT_{manifest.assetId}_{variant.id}_{assignment.role}.mat");
                    roleMaterials[assignment.role] = AssetStudioBBMaterialFactory.CreateOrUpdate(
                        materialPath,
                        manifestPath,
                        preset,
                        assignment);
                }

                var prefabPath = AssetStudioBBPaths.Combine(
                    prefabsFolder,
                    $"{manifest.assetId}__{variant.id}.prefab");
                var prefab = CreateVariantPrefab(prefabPath, manifest.assetId, variant.id, lodModels, roleMaterials);
                entries.Add(new AssetStudioBBVariantSet.VariantEntry(
                    variant.id,
                    variant.displayName,
                    variant.priceMultiplier,
                    prefab));
            }

            var setPath = AssetStudioBBPaths.Combine(generatedRoot, $"{manifest.assetId}_AssetStudioBB.asset");
            var set = AssetDatabase.LoadAssetAtPath<AssetStudioBBVariantSet>(setPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<AssetStudioBBVariantSet>();
                set.name = AssetStudioBBPaths.FileNameWithoutExtension(setPath);
                AssetDatabase.CreateAsset(set, setPath);
            }

            var defaultVariant = entries.Count > 0 ? entries[0].Id : string.Empty;
            set.EditorConfigure(
                manifest.assetId,
                manifest.displayName,
                manifest.category,
                manifest.family,
                manifest.subtype,
                new Vector3(
                    manifest.dimensions.targetWidthM,
                    manifest.dimensions.targetHeightM,
                    manifest.dimensions.targetDepthM),
                defaultVariant,
                entries.ToArray());
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return set;
        }

        private static GameObject[] LoadLodModels(string manifestPath, AssetStudioBBManifest manifest)
        {
            var result = new List<GameObject>();
            AddModel(result, manifestPath, manifest.models.lod0);
            AddModel(result, manifestPath, manifest.models.lod1);
            AddModel(result, manifestPath, manifest.models.lod2);
            if (result.Count == 0)
                throw new InvalidOperationException("No hay modelos LOD importables.");
            return result.ToArray();
        }

        private static void AddModel(ICollection<GameObject> result, string manifestPath, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative))
                return;
            var path = AssetStudioBBPaths.AbsoluteAssetPath(manifestPath, relative);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model != null)
                result.Add(model);
        }

        private static GameObject CreateVariantPrefab(
            string prefabPath,
            string assetId,
            string variantId,
            IReadOnlyList<GameObject> lodModels,
            IReadOnlyDictionary<string, Material> roleMaterials)
        {
            var root = new GameObject($"{assetId}__{variantId}");
            try
            {
                var lods = new List<LOD>();
                var thresholds = new[] { 0.58f, 0.24f, 0.08f };
                for (var index = 0; index < lodModels.Count; index++)
                {
                    var instance = PrefabUtility.InstantiatePrefab(lodModels[index]) as GameObject
                        ?? UnityEngine.Object.Instantiate(lodModels[index]);
                    instance.name = $"LOD{index}";
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    RemoveColliders(instance);
                    AssignRoleMaterials(instance, roleMaterials);
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    lods.Add(new LOD(thresholds[Mathf.Min(index, thresholds.Length - 1)], renderers));
                }

                if (lods.Count > 1)
                {
                    var group = root.AddComponent<LODGroup>();
                    group.fadeMode = LODFadeMode.CrossFade;
                    group.animateCrossFading = false;
                    group.SetLODs(lods.ToArray());
                    group.RecalculateBounds();
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                    throw new InvalidOperationException($"No se pudo guardar {prefabPath}.");
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void RemoveColliders(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
        }

        private static void AssignRoleMaterials(
            GameObject root,
            IReadOnlyDictionary<string, Material> roleMaterials)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    var sourceName = materials[index] != null ? materials[index].name : string.Empty;
                    foreach (var pair in roleMaterials)
                    {
                        if (sourceName.StartsWith(RolePrefix + pair.Key, StringComparison.Ordinal))
                        {
                            materials[index] = pair.Value;
                            break;
                        }
                    }
                }
                renderer.sharedMaterials = materials;
            }
        }
    }
}
