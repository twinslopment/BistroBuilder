using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador transaccional e idempotente de 7B — Demanda jugable.
/// Añade únicamente el puente de demanda y cablea autoridades existentes.
/// </summary>
public static class BistroBuilderMarketing7BInstaller
{
    [MenuItem(
        "Tools/Bistro Builder/Marketing/7B - Instalar + validar + autotest",
        false,
        7212)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog(
                "Bistro Builder — 7B Marketing", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 7B Marketing", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            BistroBuilderMarketing7APaths.MainScene,
            OpenSceneMode.Single);
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            throw new InvalidOperationException(report);
        }
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Sal de Play Mode antes de instalar 7B.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar 7B.";
            return false;
        }

        bool preOk = BistroBuilderMarketing7BSelfTest.Run(
            out int prePassed,
            out int preFailed,
            out string preReport);
        Debug.Log(preReport);
        if (!preOk)
        {
            report = "Gate 7B previo falló: " + prePassed +
                     " OK / " + preFailed + " fallos.";
            return false;
        }

        BistroBuilderMarketing7AValidationResult sevenA =
            BistroBuilderMarketing7AValidator.ValidateCurrentScene();
        if (sevenA.Errors > 0)
        {
            report = "7B requiere una instalación 7A válida.\n" +
                     sevenA.BuildReport();
            return false;
        }

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScene);

        try
        {
            GameObject gameSystems = FindUniqueGameSystems(scene);
            if (gameSystems == null)
                throw new InvalidOperationException(
                    "No existe exactamente un GameSystems canónico.");

            BistroBuilderMarketingService marketing =
                RequireUnique<BistroBuilderMarketingService>(scene);
            BistroBuilderGeneralGameStateService general =
                RequireUnique<BistroBuilderGeneralGameStateService>(scene);
            GameClock clock = RequireUnique<GameClock>(scene);
            RestaurantServiceStateService serviceState =
                RequireUnique<RestaurantServiceStateService>(scene);
            CustomerGroupSpawner spawner =
                RequireUnique<CustomerGroupSpawner>(scene);
            BistroBuilderReservationService reservations =
                RequireUnique<BistroBuilderReservationService>(scene);
            BistroBuilderReservationAvailabilityService availability =
                RequireUnique<BistroBuilderReservationAvailabilityService>(scene);

            BistroBuilderMarketingDemandIntegrationService integration =
                EnsureUniqueOnHost<BistroBuilderMarketingDemandIntegrationService>(
                    scene,
                    gameSystems);

            SerializedObject serialized = new SerializedObject(integration);
            SetObject(serialized, "marketingService", marketing);
            SetObject(serialized, "generalGameStateService", general);
            SetObject(serialized, "gameClock", clock);
            SetObject(serialized, "serviceStateService", serviceState);
            SetObject(serialized, "customerGroupSpawner", spawner);
            SetObject(serialized, "reservationService", reservations);
            SetObject(serialized, "reservationAvailabilityService", availability);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (!integration.ValidateConfiguration(out string integrationError))
                throw new InvalidOperationException(integrationError);

            EditorUtility.SetDirty(integration);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException(
                    "Unity no pudo guardar la instalación 7B.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderMarketing7BValidationResult validation =
                BistroBuilderMarketing7BValidator.ValidateCurrentScene();
            bool finalOk = BistroBuilderMarketing7BSelfTest.Run(
                out int passed,
                out int failed,
                out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);

            if (validation.Errors > 0 || !finalOk)
                throw new InvalidOperationException(
                    "7B no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");

            report =
                "7B instalado correctamente.\n" +
                validation.BuildReport() + "\n" +
                "Autotest: " + passed + " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            try
            {
                File.WriteAllBytes(absoluteScene, sceneBackup);
                AssetDatabase.ImportAsset(
                    scene.path,
                    ImportAssetOptions.ForceSynchronousImport);
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            }
            catch (Exception rollbackError)
            {
                Debug.LogException(rollbackError);
            }

            report =
                "La instalación 7B falló y la escena fue restaurada. " +
                exception.Message;
            return false;
        }
    }

    private static void SetObject(
        SerializedObject serialized,
        string fieldName,
        UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property == null)
            throw new InvalidOperationException(
                serialized.targetObject.name + " no expone " + fieldName + ".");
        property.objectReferenceValue = value;
    }

    private static GameObject FindUniqueGameSystems(Scene scene)
    {
        GameObject found = null;
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform != null && transform.name == "GameSystems")
            {
                found = transform.gameObject;
                count++;
            }
        }
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
        GameObject host)
        where T : Component
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
            T[] values = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < values.Length; index++)
                if (values[index] != null) result.Add(values[index]);
        }
        return result.ToArray();
    }
}
