using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Instala compra persistente de mejoras sobre la fundación 9A.</summary>
public static class BistroBuilderProgression9BInstaller
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Progression/9B - Instalar + validar", false, 9010)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder - Progresión 9B", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder - Progresión 9B", report, "Aceptar");
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
            report = "Sal de Play Mode antes de instalar 9B.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || scene.isDirty)
        {
            report = "Abre y guarda Prototype_Restaurant antes de instalar 9B.";
            return false;
        }

        if (!BistroBuilderProgression9BSelfTest.Run(
                out int prePassed, out int preFailed, out string preReport))
        {
            Debug.LogError(preReport);
            report = "El autotest previo 9B falló: " + prePassed +
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

            var save = RequireUnique<BistroBuilderSaveGameService>(scene);
            var upgrades = RequireUnique<BistroBuilderUpgradeService>(scene);
            var provider = EnsureUniqueOnHost<BistroBuilderUpgradeSaveSectionProvider>(scene, host);

            SerializedObject providerSo = new SerializedObject(provider);
            SetObject(providerSo, "saveGameService", save);
            SetObject(providerSo, "upgradeService", upgrades);
            providerSo.ApplyModifiedPropertiesWithoutUndo();

            save.RefreshExtensions();
            if (!upgrades.ValidateConfiguration(out string upgradeError))
                throw new InvalidOperationException("UpgradeService inválido: " + upgradeError);
            if (!provider.ValidateConfiguration(out string providerError))
                throw new InvalidOperationException("Provider inválido: " + providerError);
            if (!save.HasProvider(BistroBuilderUpgradeSaveSectionProvider.StableSectionId))
                throw new InvalidOperationException("SaveGame no descubrió progression.upgrades.");

            EditorUtility.SetDirty(provider);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!TrySaveSceneWithRetry(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalación 9B.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderProgression9BValidationResult validation =
                BistroBuilderProgression9BValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderProgression9BSelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException("9B no superó gates: " +
                    validation.Errors + " errores / " + failed + " fallos.");

            report = "9B — Compra y Persistencia instalada correctamente.\n" +
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
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            }
            catch (Exception rollbackError) { Debug.LogException(rollbackError); }
            report = "La instalación 9B falló y la escena fue restaurada. " +
                exception.Message;
            return false;
        }
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

    private static void SetObject(SerializedObject serialized, string fieldName,
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
            throw new InvalidOperationException("Se esperaba exactamente un " +
                typeof(T).Name + "; hay " + matches.Length + ".");
        return matches[0];
    }

    private static T EnsureUniqueOnHost<T>(Scene scene, GameObject host) where T : Component
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
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null) result.Add(found[i]);
        }
        return result.ToArray();
    }
}
