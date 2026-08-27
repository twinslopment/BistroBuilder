using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador transaccional de 6A — Fundación de Reservas.
/// Añade únicamente ReservationService al GameSystems canónico.
/// </summary>
public static class BistroBuilderReservations6AInstaller
{
    private const string MainScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem(
        "Tools/Bistro Builder/Reservations/6A - Instalar + validar",
        false,
        612)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog(
                "Bistro Builder — 6A Reservas",
                report,
                "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 6A Reservas",
            report,
            "Aceptar");
    }

    /// <summary>
    /// Entrada para validación automática en batchmode.
    /// </summary>
    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(
            MainScenePath,
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
            report = "Sal de Play Mode antes de instalar 6A.";
            return false;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar 6A.";
            return false;
        }

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] backup = File.ReadAllBytes(absoluteScene);

        try
        {
            bool gateOk = BistroBuilderReservations6AFoundationSelfTest.Run(
                out int prePassed,
                out int preFailed,
                out string preReport);
            Debug.Log(preReport);
            if (!gateOk)
            {
                throw new InvalidOperationException(
                    "Gate 6A previo: " + prePassed +
                    " OK / " + preFailed + " fallos.");
            }

            GameObject gameSystems = FindUniqueGameSystems(scene);
            if (gameSystems == null)
                throw new InvalidOperationException(
                    "No existe exactamente un GameSystems canónico.");
            BistroBuilderReservationService service =
                EnsureUnique<BistroBuilderReservationService>(
                    scene,
                    gameSystems);
            if (!service.ValidateConfiguration(out string serviceError))
                throw new InvalidOperationException(serviceError);

            EditorUtility.SetDirty(service);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException(
                    "Unity no pudo guardar la instalación 6A.");

            BistroBuilderReservations6AValidationResult validation =
                BistroBuilderReservations6AValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderReservations6AFoundationSelfTest.Run(
                out int passed,
                out int failed,
                out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);

            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException(
                    "6A no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");
            report =
                "6A instalado correctamente.\n" +
                validation.BuildReport() + "\n" +
                "Autotest: " + passed + " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            File.WriteAllBytes(absoluteScene, backup);
            AssetDatabase.ImportAsset(
                scene.path,
                ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(
                scene.path,
                OpenSceneMode.Single);

            report =
                "La instalación 6A falló y la escena fue restaurada. " +
                exception.Message;
            Debug.LogException(exception);
            return false;
        }
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

    private static T EnsureUnique<T>(
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
