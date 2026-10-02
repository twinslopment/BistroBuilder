#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>Imports only the dedicated StaffPortraits asset library as UI sprites.</summary>
public sealed class BistroBuilderStaffPortraitImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(
                "Assets/Resources/BistroBuilder/UI/StaffPortraits/",
                StringComparison.Ordinal)) return;
        if (!(assetImporter is TextureImporter importer)) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 512;
        importer.filterMode = FilterMode.Bilinear;
    }
}
#endif
