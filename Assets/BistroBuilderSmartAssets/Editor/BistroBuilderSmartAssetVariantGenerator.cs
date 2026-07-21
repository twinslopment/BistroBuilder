using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.SmartAssets.Editor
{
    /// <summary>
    /// Genera y actualiza materiales, prefabs visuales y el conjunto de variantes
    /// sin duplicar la geometría del FBX original.
    ///
    /// Operación idempotente:
    /// - reutiliza materiales existentes;
    /// - sobrescribe prefabs visuales existentes;
    /// - conserva sus rutas y GUID;
    /// - actualiza el VariantSet existente.
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
                throw new ArgumentNullException(nameof(source));
            }

            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            var container = BistroBuilderSmartAssetPaths.ContainerPath(modelPath);
            var generatedRoot = $"{container}/Generated";
            var materialsFolder = $"{generatedRoot}/Materials";
            var prefabsFolder = $"{generatedRoot}/VisualVariants";

            BistroBuilderSmartAssetPaths.EnsureFolder(generatedRoot);
            BistroBuilderSmartAssetPaths.EnsureFolder(materialsFolder);
            BistroBuilderSmartAssetPaths.EnsureFolder(prefabsFolder);

            var entries =
                new List<BistroBuilderSmartAssetVariantSet.VariantEntry>();

            var sourceVariants =
                manifest.variants
                ?? Array.Empty<BistroBuilderSmartAssetManifest.VariantData>();

            AssetDatabase.StartAssetEditing();

            try
            {
                foreach (var variant in sourceVariants)
                {
                    if (string.IsNullOrWhiteSpace(variant.id))
                    {
                        continue;
                    }

                    var materialPath =
                        $"{materialsFolder}/MAT_{manifest.assetId}_{variant.id}.mat";

                    var material = CreateOrUpdateMaterial(
                        materialPath,
                        variant);

                    var prefabPath =
                        $"{prefabsFolder}/{manifest.assetId}__{variant.id}.prefab";

                    var prefab = CreateOrUpdateVisualPrefab(
                        prefabPath,
                        source,
                        material,
                        manifest.assetId,
                        variant.id);

                    entries.Add(
                        new BistroBuilderSmartAssetVariantSet.VariantEntry(
                            variant.id,
                            variant.displayName,
                            material,
                            prefab,
                            Mathf.Max(0.01f, variant.priceMultiplier)));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            var variantSetPath =
                $"{generatedRoot}/{manifest.assetId}_VariantSet.asset";

            var variantSet =
                AssetDatabase.LoadAssetAtPath<
                    BistroBuilderSmartAssetVariantSet>(variantSetPath);

            if (variantSet == null)
            {
                variantSet =
                    ScriptableObject.CreateInstance<
                        BistroBuilderSmartAssetVariantSet>();

                variantSet.name =
                    Path.GetFileNameWithoutExtension(variantSetPath);

                AssetDatabase.CreateAsset(
                    variantSet,
                    variantSetPath);
            }

            variantSet.EditorConfigure(
                manifest.assetId,
                source,
                entries.ToArray());

            EditorUtility.SetDirty(variantSet);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return variantSet;
        }

        /// <summary>
        /// Crea o actualiza el material manteniendo estable el archivo, el GUID
        /// y el nombre interno exigido por Unity.
        /// </summary>
        private static Material CreateOrUpdateMaterial(
            string materialPath,
            BistroBuilderSmartAssetManifest.VariantData variant)
        {
            var material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material == null)
            {
                var shader =
                    Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("HDRP/Lit")
                    ?? Shader.Find("Standard");

                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "No se encontró un shader Lit compatible.");
                }

                material = new Material(shader)
                {
                    // Unity requiere que el nombre del objeto principal coincida
                    // con el nombre del archivo .mat.
                    name = Path.GetFileNameWithoutExtension(materialPath)
                };

                AssetDatabase.CreateAsset(
                    material,
                    materialPath);
            }
            else
            {
                // Corrige materiales creados por la versión 1.0.1 sin cambiar
                // la ruta ni el GUID.
                var expectedName =
                    Path.GetFileNameWithoutExtension(materialPath);

                if (!string.Equals(
                    material.name,
                    expectedName,
                    StringComparison.Ordinal))
                {
                    material.name = expectedName;
                }
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
                    1f - Mathf.Clamp01(variant.roughness));
            }
        }

        private static GameObject CreateOrUpdateVisualPrefab(
            string prefabPath,
            GameObject source,
            Material material,
            string assetId,
            string variantId)
        {
            GameObject instance = null;

            try
            {
                instance =
                    PrefabUtility.InstantiatePrefab(source) as GameObject;

                if (instance == null)
                {
                    instance =
                        UnityEngine.Object.Instantiate(source);
                }

                instance.name =
                    $"{assetId}__{variantId}";

                instance.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);

                instance.transform.localScale =
                    Vector3.one;

                var renderers =
                    instance.GetComponentsInChildren<Renderer>(true);

                if (renderers.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"El modelo '{assetId}' no contiene ningún Renderer.");
                }

                foreach (var renderer in renderers)
                {
                    var sharedMaterials =
                        renderer.sharedMaterials;

                    for (var index = 0;
                         index < sharedMaterials.Length;
                         index++)
                    {
                        sharedMaterials[index] = material;
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
                        $"No se pudo guardar el prefab '{prefabPath}'.");
                }

                return prefab;
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }
    }
}
