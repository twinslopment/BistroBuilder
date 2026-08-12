#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class BistroBuilderSuppliers23IPaths
{
    public const string SettingsFolder = "Assets/Resources/BistroBuilder/Suppliers";
    public const string SettingsAssetPath = SettingsFolder + "/BistroBuilderSupplierProgressionSettings.asset";
    public const string SupplierDatabasePath = "Assets/Resources/BistroBuilder/Suppliers/Authoring/BistroBuilderSupplierAuthoringDatabase.asset";

    public static void EnsureFolders()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/BistroBuilder");
        EnsureFolder(SettingsFolder);
    }

    public static BistroBuilderSupplierProgressionSettings LoadSettings()
    {
        return AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierProgressionSettings>(SettingsAssetPath);
    }

    public static BistroBuilderSupplierAuthoringDatabase LoadSuppliers()
    {
        return AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(SupplierDatabasePath);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
