#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class BistroBuilderSuppliers23A2Paths
{
    public const string RootFolder = "Assets/Resources/BistroBuilder/Suppliers/Authoring";
    public const string SupplierDatabasePath = RootFolder + "/BistroBuilderSupplierAuthoringDatabase.asset";
    public const string IngredientDatabasePath = RootFolder + "/BistroBuilderIngredientAuthoringDatabase.asset";

    public static void EnsureFolders()
    {
        EnsureAssetFolder("Assets/Resources");
        EnsureAssetFolder("Assets/Resources/BistroBuilder");
        EnsureAssetFolder("Assets/Resources/BistroBuilder/Suppliers");
        EnsureAssetFolder(RootFolder);
    }

    public static BistroBuilderSupplierAuthoringDatabase LoadSupplierDatabase()
    {
        return AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(SupplierDatabasePath);
    }

    public static BistroBuilderIngredientAuthoringDatabase LoadIngredientDatabase()
    {
        return AssetDatabase.LoadAssetAtPath<BistroBuilderIngredientAuthoringDatabase>(IngredientDatabasePath);
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
