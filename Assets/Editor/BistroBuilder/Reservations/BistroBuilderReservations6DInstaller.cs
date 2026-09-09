using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador transaccional de 6D — persistencia Save/Load de Reservas.
/// </summary>
public static class BistroBuilderReservations6DInstaller
{
    private const string MainScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Reservations/6D - Instalar + validar", false, 642)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder — 6D Reservas", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — 6D Reservas", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
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
            report = "Sal de Play Mode antes de instalar 6D.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar 6D.";
            return false;
        }

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScene);

        try
        {
            bool preOk = BistroBuilderReservations6DSelfTest.Run(
                out int prePassed,
                out int preFailed,
                out string preReport);
            Debug.Log(preReport);
            if (!preOk)
                throw new InvalidOperationException(
                    "Gate 6D previo: " + prePassed + " OK / " + preFailed + " fallos.");

            GameObject gameSystems = FindUniqueGameSystems(scene);
            if (gameSystems == null)
                throw new InvalidOperationException(
                    "No existe exactamente un GameSystems canónico.");

            BistroBuilderSaveGameService saveGame =
                RequireUnique<BistroBuilderSaveGameService>(scene);
            BistroBuilderReservationService reservationService =
                RequireUnique<BistroBuilderReservationService>(scene);
            BistroBuilderReservationServiceIntegration integration =
                RequireUnique<BistroBuilderReservationServiceIntegration>(scene);

            BistroBuilderReservationsSaveSectionProvider provider =
                EnsureUniqueOnHost<BistroBuilderReservationsSaveSectionProvider>(
                    scene,
                    gameSystems);

            Wire(provider, saveGame, reservationService, integration);
            if (!provider.ValidateConfiguration(out string providerError))
                throw new InvalidOperationException(providerError);

            saveGame.RefreshExtensions();
            if (!saveGame.HasProvider(
                    BistroBuilderReservationsSaveSectionProvider.StableSectionId))
                throw new InvalidOperationException(
                    "SaveGame no registró reservations.state.");

            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(saveGame);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException(
                    "Unity no pudo guardar la instalación 6D.");

            BistroBuilderReservations6DValidationResult validation =
                BistroBuilderReservations6DValidator.ValidateCurrentScene();
            bool finalOk = BistroBuilderReservations6DSelfTest.Run(
                out int passed,
                out int failed,
                out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !finalOk)
                throw new InvalidOperationException(
                    "6D no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");

            report = "6D instalado correctamente.\n" +
                     validation.BuildReport() + "\n" +
                     "Autotest: " + passed + " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            File.WriteAllBytes(absoluteScene, sceneBackup);
            AssetDatabase.ImportAsset(
                scene.path,
                ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            report = "La instalación 6D falló y la escena fue restaurada. " +
                     exception.Message;
            Debug.LogException(exception);
            return false;
        }
    }

    private static void Wire(
        BistroBuilderReservationsSaveSectionProvider provider,
        BistroBuilderSaveGameService saveGame,
        BistroBuilderReservationService reservationService,
        BistroBuilderReservationServiceIntegration integration)
    {
        SerializedObject serialized = new SerializedObject(provider);
        SetObject(serialized, "saveGameService", saveGame);
        SetObject(serialized, "reservationService", reservationService);
        SetObject(serialized, "serviceIntegration", integration);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObject(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(
                serialized.targetObject.name + " no expone " + propertyName + ".");
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

    private static T RequireUnique<T>(Scene scene)
        where T : Component
    {
        T[] matches = FindSceneComponents<T>(scene);
        if (matches.Length != 1)
            throw new InvalidOperationException(
                "Se esperaba exactamente un " + typeof(T).Name +
                "; hay " + matches.Length + ".");
        return matches[0];
    }

    private static T EnsureUniqueOnHost<T>(Scene scene, GameObject host)
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

    private static T[] FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] values = root.GetComponentsInChildren<T>(true);
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] != null)
                    result.Add(values[index]);
            }
        }
        return result.ToArray();
    }
}
