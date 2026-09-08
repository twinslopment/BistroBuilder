using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Instalador transaccional e idempotente de la inspecciÃ³n individual 10G.</summary>
public static class BistroBuilderAdvancedCustomers10GInstaller
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string PrefabPath = "Assets/Prefabs/Customers/CustomerGroupPrefab.prefab";

    [MenuItem("Tools/Bistro Builder/Customers/10G - Instalar + validar", false, 10060)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder â€” Clientes 10G", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder â€” Clientes 10G", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        if (!BistroBuilderSceneLockGuard.TryEnsureWritable(
                ScenePath, out string lockError))
            throw new InvalidOperationException(
                "BB Scene Lock Guard bloqueó 10G: " + lockError);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!TryInstall(out string report)) throw new InvalidOperationException(report);
        Debug.Log(report);
    }
    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Sal de Play Mode antes de instalar Clientes 10G.";
            return false;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || scene.isDirty)
        {
            report = "Abre y guarda Prototype_Restaurant antes de instalar 10G.";
            return false;
        }
        if (!BistroBuilderAdvancedCustomers10GSelfTest.Run(
                out int prePassed, out int preFailed, out string preReport))
        {
            Debug.LogError(preReport);
            report = "El autotest previo 10G fallÃ³: " + prePassed +
                " OK / " + preFailed + " fallos.";
            return false;
        }
        Debug.Log(preReport);

        if (!BistroBuilderSceneLockGuard.TryEnsureWritable(
                ScenePath, out string sceneLockError))
        {
            report = "BB Scene Lock Guard bloqueó la instalación: " +
                sceneLockError;
            return false;
        }

        string absoluteScene = Path.GetFullPath(ScenePath);
        string absolutePrefab = Path.GetFullPath(PrefabPath);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScene);
        byte[] prefabBackup = File.ReadAllBytes(absolutePrefab);
        try
        {
            InstallScene(scene);
            InstallPrefab();
            if (!TrySaveSceneWithRetry(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalaciÃ³n 10G.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var validation = BistroBuilderAdvancedCustomers10GValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderAdvancedCustomers10GSelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException(
                    "10G no superÃ³ gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");

            report = "10G â€” Ficha individual instalada correctamente.\n" +
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
                File.WriteAllBytes(absolutePrefab, prefabBackup);
                AssetDatabase.ImportAsset(
                    ScenePath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(
                    PrefabPath, ImportAssetOptions.ForceSynchronousImport);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (Exception rollbackError)
            {
                Debug.LogException(rollbackError);
            }
            report = "La instalaciÃ³n 10G fallÃ³ y fue restaurada. " + exception.Message;
            return false;
        }
    }
    private static void InstallScene(Scene scene)
    {
        GameObject host = FindUniqueGameSystems(scene);
        if (host == null)
            throw new InvalidOperationException("No existe exactamente un GameSystems canÃ³nico.");

        var profiles = RequireUnique<BistroBuilderAdvancedCustomerProfileService>(scene);
        var behavior = RequireUnique<BistroBuilderAdvancedCustomerBehaviorService>(scene);
        var history = RequireUnique<BistroBuilderAdvancedCustomerHistoryService>(scene);
        var tracking = RequireUnique<BistroBuilderCustomerExperienceTrackingService>(scene);
        var inspection = EnsureUniqueOnHost<
            BistroBuilderAdvancedCustomerInspectionService>(scene, host);
        var controller = EnsureUniqueOnHost<
            BistroBuilderAdvancedCustomerInspectionController>(scene, host);

        SetObject(inspection, "profileService", profiles);
        SetObject(inspection, "behaviorService", behavior);
        SetObject(inspection, "historyService", history);
        SetObject(inspection, "trackingService", tracking);
        SetObject(controller, "inspectionService", inspection);
        if (!inspection.ValidateConfiguration(out string inspectionError))
            throw new InvalidOperationException("InspectionService invÃ¡lido: " + inspectionError);
        if (!controller.ValidateConfiguration(out string controllerError))
            throw new InvalidOperationException("InspectionController invÃ¡lido: " + controllerError);
        EditorUtility.SetDirty(inspection);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
    }
    private static void InstallPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            CustomerGroup group = root.GetComponent<CustomerGroup>();
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (group == null || renderer == null)
                throw new InvalidOperationException(
                    "CustomerGroupPrefab no conserva CustomerGroup y MeshRenderer canÃ³nicos.");

            var visuals = root.GetComponents<BistroBuilderAdvancedCustomerMemberVisualGroup>();
            if (visuals.Length > 1)
                throw new InvalidOperationException(
                    "CustomerGroupPrefab contiene varios materializadores 10G.");
            var visual = visuals.Length == 1
                ? visuals[0]
                : root.AddComponent<BistroBuilderAdvancedCustomerMemberVisualGroup>();
            SetObject(visual, "customerGroup", group);
            SetObject(visual, "groupPlaceholderRenderer", renderer);
            if (!visual.ValidateConfiguration(out string error))
                throw new InvalidOperationException("VisualGroup invÃ¡lido: " + error);
            EditorUtility.SetDirty(visual);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
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
    private static void SetObject(
        Component component, string fieldName, UnityEngine.Object value)
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
        if (matches.Length != 1)
            throw new InvalidOperationException(
                "Se esperaba exactamente un " + typeof(T).Name +
                "; hay " + matches.Length + ".");
        return matches[0];
    }
    private static T EnsureUniqueOnHost<T>(
        Scene scene, GameObject host) where T : Component
    {
        T[] matches = FindSceneComponents<T>(scene);
        if (matches.Length > 1)
            throw new InvalidOperationException("Hay varios " + typeof(T).Name + ".");
        T component = matches.Length == 1 ? matches[0] : Undo.AddComponent<T>(host);
        if (component.gameObject != host)
            throw new InvalidOperationException(typeof(T).Name + " no vive en GameSystems.");
        return component;
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        if (!scene.IsValid() || !scene.isLoaded) return result.ToArray();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null) result.Add(found[i]);
        }
        return result.ToArray();
    }
}
