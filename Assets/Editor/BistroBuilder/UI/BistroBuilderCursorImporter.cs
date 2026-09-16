using UnityEditor;

public sealed class BistroBuilderCursorImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/BistroBuilder/UI/Cursors/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Cursor;
        importer.isReadable = true; importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
    }
}
