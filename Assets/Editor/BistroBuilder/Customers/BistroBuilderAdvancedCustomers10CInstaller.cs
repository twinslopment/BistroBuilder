using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Instalador transaccional e idempotente de 10C.</summary>
public static class BistroBuilderAdvancedCustomers10CInstaller
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Customers/10C - Instalar + validar", false, 10020)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder — Clientes 10C", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Clientes 10C", report, "Aceptar");
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
            report = "Sal de Play Mode antes de instalar Clientes 10C.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || scene.isDirty)
        {
            report = "Abre y guarda Prototype_Restaurant antes de instalar 10C.";
            return false;
        }

        if (!BistroBuilderAdvancedCustomers10CSelfTest.Run(
                out int prePassed, out int preFailed, out string preReport))
        {
            Debug.LogError(preReport);
            report = "El autotest previo 10C falló: " + prePassed +
                " OK / " + preFailed + " fallos.";
            return false;
        }
        Debug.Log(preReport);

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScene);

        try
        {
            GameObject host = FindUniqueGameSystems(scene);
            if (host == null)
                throw new InvalidOperationException(
                    "No existe exactamente un GameSystems canónico.");

            BistroBuilderSaveGameService save =
                RequireUnique<BistroBuilderSaveGameService>(scene);
            BistroBuilderCustomerExperienceTrackingService tracking =
                RequireUnique<BistroBuilderCustomerExperienceTrackingService>(scene);
            BistroBuilderGuestRelationsService relations =
                RequireUnique<BistroBuilderGuestRelationsService>(scene);

            BistroBuilderAdvancedCustomerHistoryService history =
                EnsureUniqueOnHost<BistroBuilderAdvancedCustomerHistoryService>(scene, host);
            BistroBuilderAdvancedCustomerHistorySaveSectionProvider provider =
                EnsureUniqueOnHost<BistroBuilderAdvancedCustomerHistorySaveSectionProvider>(
                    scene, host);

            SetObject(history, "experienceTrackingService", tracking);
            SetObject(history, "guestRelationsService", relations);
            SetObject(provider, "saveGameService", save);
            SetObject(provider, "historyService", history);

            save.RefreshExtensions();
            if (!history.ValidateConfiguration(out string historyError))
                throw new InvalidOperationException(
                    "AdvancedCustomerHistoryService inválido: " + historyError);
            if (!provider.ValidateConfiguration(out string providerError))
                throw new InvalidOperationException(
                    "Persistencia 10C inválida: " + providerError);
            if (!save.HasProvider(
                    BistroBuilderAdvancedCustomerHistorySaveSectionProvider.StableSectionId))
                throw new InvalidOperationException(
                    "SaveGame no descubrió advanced_customers.state.");

            EditorUtility.SetDirty(history);
            EditorUtility.SetDirty(provider);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!TrySaveSceneWithRetry(scene))
                throw new InvalidOperationException(
                    "Unity no pudo guardar la instalación 10C.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderAdvancedCustomers10CValidationResult validation =
                BistroBuilderAdvancedCustomers10CValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderAdvancedCustomers10CSelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException(
                    "10C no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");

            report = "10C — Historial, habituales y VIP instalado correctamente.\n" +
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
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            }
            catch (Exception rollbackError)
            {
                Debug.LogException(rollbackError);
            }

            report = "La instalación 10C falló y fue restaurada. " +
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
        T component = matches.Length == 1 ? matches[0] : Undo.AddComponent<T>(host);
        if (component.gameObject != host)
            throw new InvalidOperationException(
                typeof(T).Name + " no vive en GameSystems.");
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
