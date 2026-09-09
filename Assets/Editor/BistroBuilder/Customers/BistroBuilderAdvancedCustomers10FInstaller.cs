using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Instala la proyección conductual 10F sobre autoridades existentes.</summary>
public static class BistroBuilderAdvancedCustomers10FInstaller
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Customers/10F - Instalar + validar", false, 10050)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder — Clientes 10F", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Clientes 10F", report, "Aceptar");
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
            report = "Sal de Play Mode antes de instalar Clientes 10F.";
            return false;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || scene.isDirty)
        {
            report = "Abre y guarda Prototype_Restaurant antes de instalar 10F.";
            return false;
        }
        if (!BistroBuilderAdvancedCustomers10FSelfTest.Run(
                out int prePassed, out int preFailed, out string preReport))
        {
            Debug.LogError(preReport);
            report = "El autotest previo 10F falló: " + prePassed +
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
                throw new InvalidOperationException("No existe exactamente un GameSystems canónico.");

            var tracking = RequireUnique<BistroBuilderCustomerExperienceTrackingService>(scene);
            var profiles = RequireUnique<BistroBuilderAdvancedCustomerProfileService>(scene);
            var assignment = RequireUnique<TableAssignmentSystem>(scene);
            var service = EnsureUniqueOnHost<BistroBuilderAdvancedCustomerBehaviorService>(
                scene, host);

            SetObject(service, "trackingService", tracking);
            SetObject(service, "profileService", profiles);
            SetObject(service, "tableAssignmentSystem", assignment);
            if (!service.ValidateConfiguration(out string serviceError))
                throw new InvalidOperationException("BehaviorService inválido: " + serviceError);

            EditorUtility.SetDirty(service);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!TrySaveSceneWithRetry(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalación 10F.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var validation = BistroBuilderAdvancedCustomers10FValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderAdvancedCustomers10FSelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException(
                    "10F no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");

            report = "10F — Comportamiento creíble en servicio instalado correctamente.\n" +
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
            report = "La instalación 10F falló y fue restaurada. " + exception.Message;
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
