using UnityEditor;
using UnityEngine;

namespace BistroBuilder.SmartAssets.Editor
{
    /// <summary>
    /// Aplica automáticamente el contrato de importación a los modelos colocables.
    /// </summary>
    internal sealed class BistroBuilderSmartAssetPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (!BistroBuilderSmartAssetPaths.IsManagedModel(assetPath)) return;
            var importer = assetImporter as ModelImporter;
            if (importer == null) return;

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.preserveHierarchy = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.generateSecondaryUV = false;
#if UNITY_6000_0_OR_NEWER
            importer.bakeAxisConversion = false;
#endif
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!BistroBuilderSmartAssetPaths.IsManagedModel(assetPath)) return;
            BistroBuilderSmartAssetManifest manifest;
            var manifestPath = BistroBuilderSmartAssetPaths.ManifestPath(assetPath);
            if (!BistroBuilderSmartAssetManifest.TryLoad(manifestPath, out manifest))
            {
                Debug.LogWarning($"[Smart Assets] Se importó '{assetPath}' sin manifiesto '{manifestPath}'.");
                return;
            }

            var messages = BistroBuilderSmartAssetValidator.Validate(root, manifest);
            foreach (var message in messages)
            {
                var text = $"[Smart Assets] {manifest.assetId}: {message.Text}";
                if (message.Severity == SmartAssetSeverity.Error) Debug.LogError(text, root);
                else if (message.Severity == SmartAssetSeverity.Warning) Debug.LogWarning(text, root);
                else Debug.Log(text, root);
            }
        }
    }
}
