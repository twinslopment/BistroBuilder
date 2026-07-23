using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.AssetStudioBB.Editor
{
    /// <summary>
    /// Importación aislada del pipeline antiguo: solo procesa Assets/Art/AssetStudioBB.
    /// </summary>
    internal sealed class AssetStudioBBPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() => 3;

        private void OnPreprocessModel()
        {
            if (!AssetStudioBBPaths.IsManagedModel(assetPath))
                return;
            if (!(assetImporter is ModelImporter importer))
                return;

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
#if UNITY_6000_0_OR_NEWER
            importer.bakeAxisConversion = false;
#endif
        }

        private void OnPreprocessTexture()
        {
            if (!AssetStudioBBPaths.IsManagedTexture(assetPath))
                return;
            if (!(assetImporter is TextureImporter importer))
                return;

            var file = AssetStudioBBPaths.FileName(assetPath).ToLowerInvariant();
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            if (file == "normal.png")
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = file == "basecolor.png";
            }
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!AssetStudioBBPaths.IsManagedModel(assetPath))
                return;
            var colliders = root.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
                Debug.LogError($"[Asset Studio BB] El modelo visual contiene {colliders.Length} colliders.", root);
        }
    }
}
