#if UNITY_EDITOR
using System.IO;
using UnityEditor;

internal static class BistroBuilderSuppliers23DPaths
{
    public const string SupplierDatabasePath =
        "Assets/Resources/BistroBuilder/Suppliers/Authoring/BistroBuilderSupplierAuthoringDatabase.asset";
    public const string IngredientDatabasePath =
        "Assets/Resources/BistroBuilder/Suppliers/Authoring/BistroBuilderIngredientAuthoringDatabase.asset";
    public const string MarketSettingsPath =
        "Assets/Resources/BistroBuilder/Suppliers/BistroBuilderSupplierMarketSettings.asset";
    public const string CommercialSettingsPath =
        "Assets/Resources/BistroBuilder/Suppliers/BistroBuilderSupplierCommercialIntelligenceSettings.asset";

    public static void EnsureFolders()
    {
        EnsureAssetFolder("Assets/Resources");
        EnsureAssetFolder("Assets/Resources/BistroBuilder");
        EnsureAssetFolder("Assets/Resources/BistroBuilder/Suppliers");
    }

    private static void EnsureAssetFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        string name = Path.GetFileName(assetPath);
        if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureAssetFolder(parent);
        }
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
