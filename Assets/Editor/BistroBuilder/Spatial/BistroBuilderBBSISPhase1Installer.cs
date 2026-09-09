using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderBBSISPhase1Installer
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string CatalogFolder = "Assets/Resources/BistroBuilder/Spatial";
    private const string CatalogPath = CatalogFolder + "/BistroBuilderSpatialFamilyCatalog.asset";

    [MenuItem("Bistro Builder/BBSIS/Fase 1/Instalar")]
    public static void Install()
    {
        EnsureFolder(CatalogFolder);
        BistroBuilderSpatialFamilyCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialFamilyCatalog>(CatalogPath);
        if (catalog == null)
        {
            if (File.Exists(Path.GetFullPath(CatalogPath)))
                AssetDatabase.DeleteAsset(CatalogPath);
            catalog = ScriptableObject.CreateInstance<BistroBuilderSpatialFamilyCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.ConfigureSeedForEditor();
        EditorUtility.SetDirty(catalog);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject systems = GameObject.Find("GameSystems");
        if (systems == null)
            throw new InvalidOperationException("BBSIS: falta GameSystems en la escena canónica.");

        BistroBuilderSpatialInteractionService[] existing =
            UnityEngine.Object.FindObjectsByType<BistroBuilderSpatialInteractionService>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existing.Length > 1)
            throw new InvalidOperationException("BBSIS: existen autoridades espaciales duplicadas.");

        BistroBuilderSpatialInteractionService service =
            existing.Length == 1 ? existing[0] : Undo.AddComponent<BistroBuilderSpatialInteractionService>(systems);
        service.ConfigureForEditor(catalog);
        service.RebuildSubjects();
        EditorUtility.SetDirty(service);
        EditorUtility.SetDirty(systems);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Bistro Builder/BBSIS/Fase 1/Instalar y validar")]
    public static void InstallAndValidate()
    {
        Install();
        BistroBuilderBBSISPhase1Validator.Run();
        BistroBuilderBBSISPhase1SelfTest.Run();
    }

    public static void InstallFromCommandLine()
    {
        try
        {
            InstallAndValidate();
            Debug.Log("BBSIS FASE 1 - INSTALACION PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void EnsureFolder(string path)
    {
        string normalized = path.Replace('\\', '/');
        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
