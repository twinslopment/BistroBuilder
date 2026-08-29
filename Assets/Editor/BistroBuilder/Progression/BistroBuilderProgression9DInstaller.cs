using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderProgression9DInstaller
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string UpgradeCatalogPath =
        "Assets/Data/Progression/BB_Upgrade_Catalog.asset";

    [MenuItem("Tools/Bistro Builder/Progression/9D - Instalar + validar", false, 9030)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder - Progresión 9D", report, "Aceptar");
            return;
        }
        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder - Progresión 9D", report, "Aceptar");
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
            report = "Sal de Play Mode antes de instalar 9D.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || scene.isDirty)
        {
            report = "Abre y guarda Prototype_Restaurant antes de instalar 9D.";
            return false;
        }

        if (!BistroBuilderProgression9DSelfTest.Run(
                out int prePassed, out int preFailed, out string preReport))
        {
            Debug.LogError(preReport);
            report = "El autotest previo 9D falló: " + prePassed +
                " OK / " + preFailed + " fallos.";
            return false;
        }
        Debug.Log(preReport);

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] sceneBackup = File.ReadAllBytes(absoluteScene);
        BistroBuilderUpgradeCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderUpgradeCatalog>(UpgradeCatalogPath);
        if (catalog == null)
        {
            report = "9D no encuentra BB_Upgrade_Catalog.asset de 9A.";
            return false;
        }
        string catalogBackup = EditorJsonUtility.ToJson(catalog);

        try
        {
            catalog.EditorReplaceAll(BistroBuilderProgression9ASeed.Build());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            GameObject host = FindUniqueGameSystems(scene);
            if (host == null)
                throw new InvalidOperationException("No existe exactamente un GameSystems canónico.");

            BistroBuilderUpgradeService upgrades = RequireUnique<BistroBuilderUpgradeService>(scene);
            BistroBuilderCustomerExperienceTrackingService tracking =
                RequireUnique<BistroBuilderCustomerExperienceTrackingService>(scene);
            BistroBuilderOrderLineExecutionService execution =
                RequireUnique<BistroBuilderOrderLineExecutionService>(scene);
            BistroBuilderUpgradeEffectsService effects =
                EnsureUniqueOnHost<BistroBuilderUpgradeEffectsService>(scene, host);

            SetObject(effects, "upgradeService", upgrades);
            SetObject(tracking, "upgradeEffectsService", effects);

            if (!effects.ValidateConfiguration(out string effectsError))
                throw new InvalidOperationException("UpgradeEffectsService inválido: " + effectsError);
            if (!tracking.ValidateConfiguration(out string trackingError))
                throw new InvalidOperationException("ExperienceTracking inválido: " + trackingError);
            if (!execution.ValidateConfiguration(out string executionError))
                throw new InvalidOperationException("OrderLineExecution no acepta 9D: " + executionError);

            EditorUtility.SetDirty(effects);
            EditorUtility.SetDirty(tracking);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!TrySaveSceneWithRetry(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalación 9D.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderProgression9DValidationResult validation =
                BistroBuilderProgression9DValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderProgression9DSelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException("9D no superó gates: " +
                    validation.Errors + " errores / " + failed + " fallos.");

            report = "9D — Efectos jugables instalada correctamente.\n" +
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
                EditorJsonUtility.FromJsonOverwrite(catalogBackup, catalog);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            }
            catch (Exception rollbackError) { Debug.LogException(rollbackError); }
            report = "La instalación 9D falló y fue restaurada. " + exception.Message;
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

    private static void SetObject(Component component, string fieldName,
        UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(component);
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property == null)
            throw new InvalidOperationException(
                component.name + " no expone " + fieldName + ".");
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
