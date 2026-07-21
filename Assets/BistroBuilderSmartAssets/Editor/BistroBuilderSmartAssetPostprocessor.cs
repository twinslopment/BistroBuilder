using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.SmartAssets.Editor
{
    /// <summary>
    /// Aplica automáticamente el contrato de importación a los FBX
    /// situados dentro de Assets/Art/Blender/Placeables/.
    /// </summary>
    internal sealed class BistroBuilderSmartAssetPostprocessor
        : AssetPostprocessor
    {
        private const int ImporterVersion = 2;

        public override uint GetVersion()
        {
            return ImporterVersion;
        }

        private void OnPreprocessModel()
        {
            if (!BistroBuilderSmartAssetPaths.IsManagedModel(
                    assetPath))
            {
                return;
            }

            if (!(assetImporter is ModelImporter importer))
            {
                return;
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType =
                ModelImporterAnimationType.None;
            importer.preserveHierarchy = true;
            importer.meshCompression =
                ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.importNormals =
                ModelImporterNormals.Import;
            importer.importTangents =
                ModelImporterTangents.CalculateMikk;
            importer.generateSecondaryUV = false;

#if UNITY_6000_0_OR_NEWER
            importer.bakeAxisConversion = false;
#endif
        }

        private void OnPostprocessModel(
            GameObject importedRoot)
        {
            if (!BistroBuilderSmartAssetPaths.IsManagedModel(
                    assetPath))
            {
                return;
            }

            var manifestPath =
                BistroBuilderSmartAssetPaths.ManifestPath(
                    assetPath);

            if (!BistroBuilderSmartAssetManifest.TryLoad(
                    manifestPath,
                    out var manifest))
            {
                Debug.LogWarning(
                    $"[Smart Assets] Se importó '{assetPath}' " +
                    $"sin manifiesto '{manifestPath}'.");

                return;
            }

            var messages =
                BistroBuilderSmartAssetValidator.Validate(
                    importedRoot,
                    manifest);

            LogValidationResult(
                importedRoot,
                manifest,
                messages);
        }

        private static void LogValidationResult(
            GameObject contextObject,
            BistroBuilderSmartAssetManifest manifest,
            IReadOnlyList<SmartAssetMessage> messages)
        {
            var errors =
                0;

            var warnings =
                0;

            foreach (var message in messages)
            {
                var formatted =
                    $"[Smart Assets] {manifest.assetId}: " +
                    $"{message.Text}";

                switch (message.Severity)
                {
                    case SmartAssetSeverity.Error:
                        errors++;
                        Debug.LogError(
                            formatted,
                            contextObject);
                        break;

                    case SmartAssetSeverity.Warning:
                        warnings++;
                        Debug.LogWarning(
                            formatted,
                            contextObject);
                        break;
                }
            }

            if (errors == 0 && warnings == 0)
            {
                Debug.Log(
                    $"[Smart Assets] {manifest.assetId}: " +
                    $"validación superada; " +
                    $"{manifest.geometry.meshCount} malla(s), " +
                    $"{manifest.dimensions.targetWidthM:F3} × " +
                    $"{manifest.dimensions.targetDepthM:F3} × " +
                    $"{manifest.dimensions.targetHeightM:F3} m, " +
                    $"{manifest.variants?.Length ?? 0} variante(s).",
                    contextObject);
            }
        }
    }

    /// <summary>
    /// Cuando Blender escribe primero el FBX y después el manifiesto,
    /// Unity puede importar inicialmente el modelo sin ficha. Esta clase
    /// detecta la llegada del manifiesto y reimporta automáticamente el
    /// FBX hermano, eliminando la necesidad de hacerlo a mano.
    /// </summary>
    internal sealed class BistroBuilderSmartAssetManifestPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> PendingReimports =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var importedPath in importedAssets)
            {
                if (!BistroBuilderSmartAssetPaths.IsManifest(
                        importedPath))
                {
                    continue;
                }

                var modelPath =
                    BistroBuilderSmartAssetPaths
                        .ModelPathFromManifest(importedPath);

                if (!BistroBuilderSmartAssetPaths.IsManagedModel(
                        modelPath))
                {
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(
                        modelPath) == null)
                {
                    continue;
                }

                if (!PendingReimports.Add(modelPath))
                {
                    continue;
                }

                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        AssetDatabase.ImportAsset(
                            modelPath,
                            ImportAssetOptions.ForceUpdate);
                    }
                    finally
                    {
                        PendingReimports.Remove(modelPath);
                    }
                };
            }
        }
    }
}
