using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Instala 10B sobre Experience Tracking sin crear otra autoridad.</summary>
public static class BistroBuilderAdvancedCustomers10BInstaller
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Customers/10B - Instalar + validar", false, 10010)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder — Clientes 10B", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Clientes 10B", report, "Aceptar");
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
            report = "Sal de Play Mode antes de instalar Clientes 10B.";
            return false;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || scene.isDirty)
        {
            report = "Abre y guarda Prototype_Restaurant antes de instalar 10B.";
            return false;
        }
        if (!BistroBuilderAdvancedCustomers10BSelfTest.Run(
                out int prePassed, out int preFailed, out string preReport))
        {
            Debug.LogError(preReport);
            report = "El autotest previo 10B falló: " + prePassed +
                " OK / " + preFailed + " fallos.";
            return false;
        }
        Debug.Log(preReport);

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScene);
        try
        {
            BistroBuilderAdvancedCustomerProfileService profiles =
                RequireUnique<BistroBuilderAdvancedCustomerProfileService>(scene);
            BistroBuilderCustomerExperienceTrackingService tracking =
                RequireUnique<BistroBuilderCustomerExperienceTrackingService>(scene);

            SetObject(tracking, "advancedCustomerProfileService", profiles);
            if (!tracking.ValidateConfiguration(out string trackingError))
                throw new InvalidOperationException(
                    "Experience Tracking inválido tras 10B: " + trackingError);

            EditorUtility.SetDirty(tracking);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!TrySaveSceneWithRetry(scene))
                throw new InvalidOperationException(
                    "Unity no pudo guardar la instalación 10B.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderAdvancedCustomers10BValidationResult validation =
                BistroBuilderAdvancedCustomers10BValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderAdvancedCustomers10BSelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException(
                    "10B no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");
            report = "10B — Satisfacción individual instalada correctamente.\n" +
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
            report = "La instalación 10B falló y fue restaurada. " +
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

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] matches = FindSceneComponents<T>(scene);
        if (matches.Length != 1)
            throw new InvalidOperationException(
                "Se esperaba exactamente un " + typeof(T).Name +
                "; hay " + matches.Length + ".");
        return matches[0];
    }


    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        if (!scene.IsValid() || !scene.isLoaded)
            return result.ToArray();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < found.Length; index++)
                if (found[index] != null)
                    result.Add(found[index]);
        }

        return result.ToArray();
    }
}
