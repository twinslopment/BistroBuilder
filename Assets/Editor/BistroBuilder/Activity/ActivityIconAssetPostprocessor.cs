using System;
using UnityEditor;
using UnityEngine;

public sealed class ActivityIconAssetPostprocessor : AssetPostprocessor
{
    private const string IconRoot =
        "Assets/Resources/BistroBuilder/UI/ActivityIcons/";

    private void OnPreprocessTexture()
    {
        if (!IsActivityIcon(assetPath))
            return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }

    private static bool IsActivityIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !path.StartsWith(IconRoot, StringComparison.Ordinal))
        {
            return false;
        }

        string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        return fileName.StartsWith("BB_Activity_", StringComparison.Ordinal);
    }
}
