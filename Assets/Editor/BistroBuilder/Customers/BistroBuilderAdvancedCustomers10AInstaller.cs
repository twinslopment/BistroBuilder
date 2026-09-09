using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Instalador transaccional e idempotente de Clientes avanzados 10A.</summary>
public static class BistroBuilderAdvancedCustomers10AInstaller
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string CatalogFolder = "Assets/Data/Customers";
    private const string CatalogPath =
        CatalogFolder + "/BB_AdvancedCustomer_Profile_Catalog.asset";

    [MenuItem("Tools/Bistro Builder/Customers/10A - Instalar + validar", false, 10000)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder — Clientes 10A", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Clientes 10A", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!TryInstall(out string report))
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Sal de Play Mode antes de instalar Clientes 10A.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || scene.isDirty)
        {
            report = "Abre y guarda Prototype_Restaurant antes de instalar 10A.";
            return false;
        }

        if (!BistroBuilderAdvancedCustomers10ASelfTest.Run(
                out int prePassed, out int preFailed, out string preReport))
        {
            Debug.LogError(preReport);
            report = "El autotest previo 10A falló: " + prePassed +
                " OK / " + preFailed + " fallos.";
            return false;
        }
        Debug.Log(preReport);

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScene);
        BistroBuilderAdvancedCustomerProfileCatalog catalog = null;
        string catalogBackupJson = string.Empty;
        bool catalogExisted = false;

        try
        {
            EnsureAssetFolder(CatalogFolder);
            catalog = AssetDatabase.LoadAssetAtPath<
                BistroBuilderAdvancedCustomerProfileCatalog>(CatalogPath);
            catalogExisted = catalog != null;
            if (catalogExisted)
                catalogBackupJson = EditorJsonUtility.ToJson(catalog);
            else
            {
                catalog = ScriptableObject.CreateInstance<
                    BistroBuilderAdvancedCustomerProfileCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.EditorReplaceAll(BistroBuilderAdvancedCustomers10ASeed.BuildComplete());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            GameObject host = FindUniqueGameSystems(scene);
            if (host == null)
                throw new InvalidOperationException(
                    "No existe exactamente un GameSystems canónico.");

            BistroBuilderGeneralGameStateService general =
                RequireUnique<BistroBuilderGeneralGameStateService>(scene);
            TableAssignmentSystem tables = RequireUnique<TableAssignmentSystem>(scene);
            BistroBuilderAdvancedCustomerProfileService service =
                EnsureUniqueOnHost<BistroBuilderAdvancedCustomerProfileService>(scene, host);

            SetObject(service, "profileCatalog", catalog);
            SetObject(service, "generalGameStateService", general);
            SetObject(service, "tableAssignmentSystem", tables);

            if (!service.ValidateConfiguration(out string serviceError))
                throw new InvalidOperationException(
                    "AdvancedCustomerProfileService inválido: " + serviceError);

            EditorUtility.SetDirty(service);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!TrySaveSceneWithRetry(scene))
                throw new InvalidOperationException(
                    "Unity no pudo guardar la instalación 10A.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderAdvancedCustomers10AValidationResult validation =
                BistroBuilderAdvancedCustomers10AValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderAdvancedCustomers10ASelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException(
                    "10A no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");

            report = "10A — Perfiles, preferencias y paciencia instalada correctamente.\n" +
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
                AssetDatabase.ImportAsset(
                    scene.path, ImportAssetOptions.ForceSynchronousImport);
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
            catch (Exception rollbackError)
            {
                Debug.LogException(rollbackError);
            }
            report = "La instalación 10A falló y fue restaurada. " +
                exception.Message;
            return false;
        }
    }

    private static bool TrySaveSceneWithRetry(Scene scene)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (EditorSceneManager.SaveScene(scene)) return true;
            Thread.Sleep(300 + attempt * 200);
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
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void SetObject(
        Component component,
        string fieldName,
        UnityEngine.Object value)
    {
        var serialized = new SerializedObject(component);
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property == null)
            throw new InvalidOperationException(
                component.GetType().Name + " no expone " + fieldName + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject FindUniqueGameSystems(Scene scene)
    {
        GameObject found = null;
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            if (transform != null && transform.name == "GameSystems")
            { found = transform.gameObject; count++; }
        return count == 1 ? found : null;
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] matches = FindSceneComponents<T>(scene);
        if (matches.Length != 1)
            throw new InvalidOperationException(
                "Se esperaba exactamente un " + typeof(T).Name +
                "; hay " + matches.Length + ".");
        return matches[0];
    }

    private static T EnsureUniqueOnHost<T>(
        Scene scene,
        GameObject host) where T : Component
    {
        T[] matches = FindSceneComponents<T>(scene);
        if (matches.Length > 1)
            throw new InvalidOperationException(
                "Hay varios " + typeof(T).Name + ".");
        T component = matches.Length == 1
            ? matches[0]
            : Undo.AddComponent<T>(host);
        if (component.gameObject != host)
            throw new InvalidOperationException(
                typeof(T).Name + " no vive en GameSystems.");
        return component;
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null) result.Add(found[i]);
        }
        return result.ToArray();
    }
}
