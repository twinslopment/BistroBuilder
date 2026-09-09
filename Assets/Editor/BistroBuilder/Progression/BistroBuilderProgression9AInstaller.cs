using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Instalador transaccional e idempotente de 9A.</summary>
public static class BistroBuilderProgression9AInstaller
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string CatalogFolder = "Assets/Data/Progression";
    private const string CatalogPath = CatalogFolder + "/BB_Upgrade_Catalog.asset";

    [MenuItem("Tools/Bistro Builder/Progression/9A - Instalar + validar", false, 9000)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder - Progresión 9A", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder - Progresión 9A", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!TryInstall(out string report)) throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Sal de Play Mode antes de instalar 9A.";
            return false;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || scene.isDirty)
        {
            report = "Abre y guarda Prototype_Restaurant antes de instalar 9A.";
            return false;
        }
        if (!BistroBuilderProgression9ASelfTest.Run(
                out int prePassed, out int preFailed, out string preReport))
        {
            Debug.LogError(preReport);
            report = "El autotest previo 9A falló: " + prePassed +
                " OK / " + preFailed + " fallos.";
            return false;
        }
        Debug.Log(preReport);

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScene);
        BistroBuilderUpgradeCatalog catalog = null;
        string catalogBackupJson = string.Empty;
        bool catalogExisted = false;

        try
        {
            EnsureAssetFolder(CatalogFolder);
            catalog = AssetDatabase.LoadAssetAtPath<BistroBuilderUpgradeCatalog>(CatalogPath);
            catalogExisted = catalog != null;
            if (catalogExisted) catalogBackupJson = EditorJsonUtility.ToJson(catalog);
            else
            {
                catalog = ScriptableObject.CreateInstance<BistroBuilderUpgradeCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.EditorReplaceAll(BistroBuilderProgression9ASeed.Build());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            GameObject host = FindUniqueGameSystems(scene);
            if (host == null)
                throw new InvalidOperationException("No existe exactamente un GameSystems canónico.");

            var finance = RequireUnique<BistroBuilderDiscretionaryFinanceService>(scene);
            var general = RequireUnique<BistroBuilderGeneralGameStateService>(scene);
            var reputation = RequireUnique<BistroBuilderReputationService>(scene);
            var service = EnsureUniqueOnHost<BistroBuilderUpgradeService>(scene, host);

            SetObject(service, "upgradeCatalog", catalog);
            SetObject(service, "discretionaryFinanceService", finance);
            SetObject(service, "generalGameStateService", general);
            SetObject(service, "reputationService", reputation);
            SetStringList(service, "localCapabilityIds", ResolveLocalCapabilities(scene));

            if (!service.ValidateConfiguration(out string serviceError))
                throw new InvalidOperationException("UpgradeService inválido: " + serviceError);

            EditorUtility.SetDirty(service);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!TrySaveSceneWithRetry(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalación 9A.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var validation = BistroBuilderProgression9AValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderProgression9ASelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException("9A no superó gates: " +
                    validation.Errors + " errores / " + failed + " fallos.");

            report = "9A — Fundación de Mejoras y Progresión instalada correctamente.\n" +
                validation.BuildReport() + "\nAutotest: " + passed +
                " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            try
            {
                File.WriteAllBytes(absoluteScene, sceneBackup);
                AssetDatabase.ImportAsset(scene.path, ImportAssetOptions.ForceSynchronousImport);
                if (catalog != null)
                {
                    if (catalogExisted)
                    {
                        EditorJsonUtility.FromJsonOverwrite(catalogBackupJson, catalog);
                        EditorUtility.SetDirty(catalog);
                        AssetDatabase.SaveAssets();
                    }
                    else AssetDatabase.DeleteAsset(CatalogPath);
                }
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            }
            catch (Exception rollbackError) { Debug.LogException(rollbackError); }
            report = "La instalación 9A falló y fue restaurada. " + exception.Message;
            return false;
        }
    }

    private static List<string> ResolveLocalCapabilities(Scene scene)
    {
        var capabilities = new List<string>
        {
            "restaurant.base",
            "facility.dining_room",
            "facility.kitchen"
        };
        if (FindSceneComponents<BistroBuilderBarServiceSpot>(scene).Length > 0)
            capabilities.Add("facility.bar");

        RestaurantArea[] areas = FindSceneComponents<RestaurantArea>(scene);
        for (int i = 0; i < areas.Length; i++)
        {
            string typeId = areas[i]?.Definition != null
                ? BistroBuilderProgressionEngine.NormalizeId(areas[i].Definition.AreaTypeId)
                : string.Empty;
            if (typeId.Contains("terrace") || typeId.Contains("terraza"))
            {
                capabilities.Add("facility.terrace");
                break;
            }
        }
        return capabilities;
    }

    private static bool TrySaveSceneWithRetry(Scene scene)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (EditorSceneManager.SaveScene(scene)) return true;
            Thread.Sleep(250 + attempt * 150);
        }
        return false;
    }

    private static void EnsureAssetFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void SetObject(Component component, string fieldName, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(component);
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property == null) throw new InvalidOperationException(
            component.GetType().Name + " no expone " + fieldName + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStringList(Component component, string fieldName, IReadOnlyList<string> values)
    {
        var serialized = new SerializedObject(component);
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property == null || !property.isArray) throw new InvalidOperationException(
            component.GetType().Name + " no expone la lista " + fieldName + ".");
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).stringValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject FindUniqueGameSystems(Scene scene)
    {
        GameObject found = null; int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            if (transform != null && transform.name == "GameSystems")
            { found = transform.gameObject; count++; }
        return count == 1 ? found : null;
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] matches = FindSceneComponents<T>(scene);
        if (matches.Length != 1) throw new InvalidOperationException(
            "Se esperaba exactamente un " + typeof(T).Name + "; hay " + matches.Length + ".");
        return matches[0];
    }

    private static T EnsureUniqueOnHost<T>(Scene scene, GameObject host) where T : Component
    {
        T[] matches = FindSceneComponents<T>(scene);
        if (matches.Length > 1) throw new InvalidOperationException("Hay varios " + typeof(T).Name + ".");
        T component = matches.Length == 1 ? matches[0] : Undo.AddComponent<T>(host);
        if (component.gameObject != host) throw new InvalidOperationException(
            typeof(T).Name + " no vive en GameSystems.");
        return component;
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < found.Length; i++) if (found[i] != null) result.Add(found[i]);
        }
        return result.ToArray();
    }
}