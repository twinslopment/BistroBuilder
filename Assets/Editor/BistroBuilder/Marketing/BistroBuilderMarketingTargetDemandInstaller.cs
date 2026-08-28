using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instala de forma idempotente el puente TargetDemand → carta/demanda.
/// La escena se restaura byte a byte ante cualquier fallo posterior.
/// </summary>
public static class BistroBuilderMarketingTargetDemandInstaller
{
    [MenuItem(
        "Tools/Bistro Builder/Marketing/TargetDemand - Instalar + validar",
        false,
        7222)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report))
        {
            Debug.LogError(report);
            EditorUtility.DisplayDialog("Bistro Builder — Marketing", report, "Aceptar");
            return;
        }

        Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Marketing", report, "Aceptar");
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
            report = "Sal de Play Mode antes de instalar TargetDemand.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar.";
            return false;
        }

        bool preOk = BistroBuilderMarketingTargetDemandSelfTest.Run(
            out int prePassed,
            out int preFailed,
            out string preReport);
        Debug.Log(preReport);
        if (!preOk)
        {
            report = "Gate TargetDemand previo falló: " + prePassed +
                     " OK / " + preFailed + " fallos.";
            return false;
        }

        BistroBuilderMarketing7CValidationResult sevenC =
            BistroBuilderMarketing7CValidator.ValidateCurrentScene();
        if (sevenC.Errors > 0)
        {
            report = "TargetDemand requiere 7C válido.\n" +
                     sevenC.BuildReport();
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
            BistroBuilderMarketingDemandIntegrationService demand =
                RequireUnique<BistroBuilderMarketingDemandIntegrationService>(scene);
            BistroBuilderGeneralGameStateService general =
                RequireUnique<BistroBuilderGeneralGameStateService>(scene);
            BistroBuilderMenuSelectionService selection =
                RequireUnique<BistroBuilderMenuSelectionService>(scene);
            BistroBuilderRestaurantMenuService menu =
                RequireUnique<BistroBuilderRestaurantMenuService>(scene);
            BistroBuilderMenuPortfolioService portfolio =
                RequireUnique<BistroBuilderMenuPortfolioService>(scene);
            GameClock clock = RequireUnique<GameClock>(scene);
            TableAssignmentSystem tables =
                RequireUnique<TableAssignmentSystem>(scene);

            if (selection.gameObject != gameSystems)
                throw new InvalidOperationException(
                    "MenuSelectionService no vive en GameSystems.");

            BistroBuilderMarketingMenuSelectionWeightProvider provider =
                EnsureUniqueOnHost<
                    BistroBuilderMarketingMenuSelectionWeightProvider>(
                        scene,
                        gameSystems);

            SerializedObject marketingSerialized =
                new SerializedObject(marketing);
            SetObject(marketingSerialized, "menuService", menu);
            SetObject(marketingSerialized, "menuPortfolioService", portfolio);
            marketingSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject demandSerialized = new SerializedObject(demand);
            SetObject(demandSerialized, "menuPortfolioService", portfolio);
            demandSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject providerSerialized =
                new SerializedObject(provider);
            SetObject(providerSerialized, "marketingService", marketing);
            SetObject(providerSerialized, "generalGameStateService", general);
            SetObject(providerSerialized, "gameClock", clock);
            SetObject(providerSerialized, "tableAssignmentSystem", tables);
            providerSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (!provider.ValidateConfiguration(out string providerError))
                throw new InvalidOperationException(providerError);
            if (!marketing.ValidateConfiguration(out string marketingError))
                throw new InvalidOperationException(marketingError);
            if (!demand.ValidateConfiguration(out string demandError))
                throw new InvalidOperationException(demandError);

            EditorUtility.SetDirty(marketing);
            EditorUtility.SetDirty(demand);
            EditorUtility.SetDirty(provider);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException(
                    "Unity no pudo guardar la instalación TargetDemand.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderMarketingTargetDemandValidationResult validation =
                BistroBuilderMarketingTargetDemandValidator.ValidateCurrentScene();
            bool finalOk = BistroBuilderMarketingTargetDemandSelfTest.Run(
                out int passed,
                out int failed,
                out string selfReport);

            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);

            if (validation.Errors > 0 || !finalOk)
                throw new InvalidOperationException(
                    "TargetDemand no superó gates: " + validation.Errors +
                    " errores / " + failed + " fallos.");

            report =
                "TargetDemand instalado correctamente.\n" +
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
                "La instalación TargetDemand falló y la escena fue restaurada. " +
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
