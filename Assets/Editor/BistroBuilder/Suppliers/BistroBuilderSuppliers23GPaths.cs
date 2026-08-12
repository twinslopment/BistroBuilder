#if UNITY_EDITOR
using System.IO;
using UnityEditor;

internal static class BistroBuilderSuppliers23GPaths
{
    public const string SupplierDatabasePath = "Assets/Resources/BistroBuilder/Suppliers/Authoring/BistroBuilderSupplierAuthoringDatabase.asset";
    public const string LogisticsSettingsPath = "Assets/Resources/BistroBuilder/Suppliers/BistroBuilderSupplierLogisticsPlanningSettings.asset";

    public static void EnsureFolders()
    {
        EnsureAssetFolder("Assets/Resources");
        EnsureAssetFolder("Assets/Resources/BistroBuilder");
        EnsureAssetFolder("Assets/Resources/BistroBuilder/Suppliers");
    }

    private static void EnsureAssetFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;
        string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        string name = Path.GetFileName(assetPath);
        if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
