using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.SmartAssets.Editor
{
    /// <summary>
    /// Genera o actualiza materiales, prefabs visuales y el VariantSet.
    ///
    /// La operación es idempotente:
    /// - reutiliza materiales existentes;
    /// - conserva rutas y GUID;
    /// - sobrescribe los prefabs en la misma ruta;
    /// - no duplica la geometría del FBX;
    /// - puede ejecutarse tantas veces como sea necesario.
    /// </summary>
    internal static class BistroBuilderSmartAssetVariantGenerator
    {
        public static BistroBuilderSmartAssetVariantSet Generate(
            string modelPath,
            GameObject source,
            BistroBuilderSmartAssetManifest manifest)
        {
            if (source == null)
            {
                throw new ArgumentNullException(
                    nameof(source));
            }

            if (manifest == null)
            {
                throw new ArgumentNullException(
                    nameof(manifest));
            }

            var validation =
                BistroBuilderSmartAssetValidator.Validate(
                    source,
                    manifest);

            if (BistroBuilderSmartAssetValidator.HasErrors(
                    validation))
            {
                throw new InvalidOperationException(
                    "La generación de variantes está bloqueada " +
                    "porque la ficha contiene errores.");
            }

            var generatedRoot =
                BistroBuilderSmartAssetPaths
                    .GeneratedRoot(modelPath);

            var materialsFolder =
                $"{generatedRoot}/Materials";

            var prefabsFolder =
                $"{generatedRoot}/VisualVariants";

            BistroBuilderSmartAssetPaths.EnsureFolder(
                generatedRoot);

            BistroBuilderSmartAssetPaths.EnsureFolder(
                materialsFolder);

            BistroBuilderSmartAssetPaths.EnsureFolder(
                prefabsFolder);

            var entries =
                new List<
                    BistroBuilderSmartAssetVariantSet
                        .VariantEntry>();

            var sourceVariants =
                manifest.variants
                ?? Array.Empty<
                    BistroBuilderSmartAssetManifest
                        .VariantData>();

            foreach (var variant in sourceVariants)
            {
                if (variant == null
                    || string.IsNullOrWhiteSpace(variant.id))
                {
                    continue;
                }

                var materialPath =
                    $"{materialsFolder}/" +
                    $"MAT_{manifest.assetId}_" +
                    $"{variant.id}.mat";

                var material =
                    CreateOrUpdateMaterial(
                        materialPath,
                        variant);

                var prefabPath =
                    $"{prefabsFolder}/" +
                    $"{manifest.assetId}__" +
                    $"{variant.id}.prefab";

                var prefab =
                    CreateOrUpdateVisualPrefab(
                        prefabPath,
                        source,
                        material,
                        manifest.assetId,
                        variant.id);

                entries.Add(
                    new BistroBuilderSmartAssetVariantSet
                        .VariantEntry(
                            variant.id,
                            variant.displayName,
                            variant.GetBaseColor(),
                            material,
                            prefab,
                            Mathf.Max(
                                0.01f,
                                variant.priceMultiplier)));
            }

            var variantSetPath =
                $"{generatedRoot}/" +
                $"{manifest.assetId}_" +
                $"VariantSet.asset";

            var variantSet =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderSmartAssetVariantSet>(
                        variantSetPath);

            if (variantSet == null)
            {
                variantSet =
                    ScriptableObject.CreateInstance<
                        BistroBuilderSmartAssetVariantSet>();

                variantSet.name =
                    Path.GetFileNameWithoutExtension(
                        variantSetPath);

                AssetDatabase.CreateAsset(
                    variantSet,
                    variantSetPath);
            }

            var defaultVariantId =
                entries.Count > 0
                    ? entries[0].Id
                    : string.Empty;

            variantSet.EditorConfigure(
                manifest.assetId,
                defaultVariantId,
                source,
                entries.ToArray());

            EditorUtility.SetDirty(variantSet);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return variantSet;
        }

        private static Material CreateOrUpdateMaterial(
            string materialPath,
            BistroBuilderSmartAssetManifest.VariantData variant)
        {
            var material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    materialPath);

            var expectedName =
                Path.GetFileNameWithoutExtension(
                    materialPath);

            if (material == null)
            {
                var shader =
                    Shader.Find(
                        "Universal Render Pipeline/Lit")
                    ?? Shader.Find("HDRP/Lit")
                    ?? Shader.Find("Standard");

                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "No se encontró un shader Lit " +
                        "compatible.");
                }

                material =
                    new Material(shader)
                    {
                        name = expectedName,
                        enableInstancing = true
                    };

                AssetDatabase.CreateAsset(
                    material,
                    materialPath);
            }
            else
            {
                // Repara los materiales generados por versiones antiguas
                // sin cambiar su ruta ni su GUID.
                if (!string.Equals(
                        material.name,
                        expectedName,
                        StringComparison.Ordinal))
                {
                    material.name =
                        expectedName;
                }

                material.enableInstancing =
                    true;
            }

            ApplyMaterialProperties(
                material,
                variant);

            EditorUtility.SetDirty(material);

            return material;
        }

        private static void ApplyMaterialProperties(
            Material material,
            BistroBuilderSmartAssetManifest.VariantData variant)
        {
            var baseColor =
                variant.GetBaseColor();

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    baseColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor(
                    "_Color",
                    baseColor);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat(
                    "_Metallic",
                    Mathf.Clamp01(variant.metallic));
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat(
                    "_Smoothness",
                    1f - Mathf.Clamp01(
                        variant.roughness));
            }
        }

        private static GameObject CreateOrUpdateVisualPrefab(
            string prefabPath,
            GameObject source,
            Material material,
            string assetId,
            string variantId)
        {
            GameObject instance =
                null;

            try
            {
                instance =
                    PrefabUtility.InstantiatePrefab(
                        source) as GameObject;

                if (instance == null)
                {
                    instance =
                        UnityEngine.Object.Instantiate(
                            source);
                }

                instance.name =
                    $"{assetId}__{variantId}";

                instance.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);

                instance.transform.localScale =
                    Vector3.one;

                var renderers =
                    instance.GetComponentsInChildren<
                        Renderer>(true);

                if (renderers.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"El modelo '{assetId}' " +
                        "no contiene ningún Renderer.");
                }

                foreach (var renderer in renderers)
                {
                    var sharedMaterials =
                        renderer.sharedMaterials;

                    for (var index = 0;
                         index < sharedMaterials.Length;
                         index++)
                    {
                        sharedMaterials[index] =
                            material;
                    }

                    renderer.sharedMaterials =
                        sharedMaterials;
                }

                var prefab =
                    PrefabUtility.SaveAsPrefabAsset(
                        instance,
                        prefabPath);

                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"No se pudo guardar el prefab " +
                        $"'{prefabPath}'.");
                }

                return prefab;
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        instance);
                }
            }
        }
    }
}
