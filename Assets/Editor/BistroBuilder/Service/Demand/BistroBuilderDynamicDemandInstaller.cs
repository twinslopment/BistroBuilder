using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador transaccional del motor universal de demanda base dinámica.
/// </summary>
public static class BistroBuilderDynamicDemandInstaller
{
    [MenuItem("Tools/Bistro Builder/Service/Demanda dinámica - Instalar + validar", false, 8303)]
    private static void InstallFromMenu()
    {
        if (!TryInstall(out string report)) Debug.LogError(report);
        else Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder — Demanda dinámica", report, "Aceptar");
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(BistroBuilderMarketing7APaths.MainScene, OpenSceneMode.Single);
        if (!TryInstall(out string report)) throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool TryInstall(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "Sal de Play Mode antes de instalar Demanda dinámica.";
            return false;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path) || scene.isDirty)
        {
            report = "Abre y guarda la escena principal antes de instalar.";
            return false;
        }
        if (!BistroBuilderDynamicDemandSelfTest.Run(
                out int prePassed, out int preFailed, out string preReport))
        {
            Debug.LogError(preReport);
            report = "Autotest previo falló: " + prePassed + " OK / " +
                     preFailed + " fallos.";
            return false;
        }
        Debug.Log(preReport);

        string absoluteScene = Path.GetFullPath(scene.path);
        byte[] backup = File.ReadAllBytes(absoluteScene);
        try
        {
            GameObject host = FindUniqueNamed(scene, "GameSystems");
            if (host == null) throw new InvalidOperationException(
                "No existe un GameSystems canónico único.");

            BistroBuilderDynamicDemandService dynamic =
                EnsureUniqueOnHost<BistroBuilderDynamicDemandService>(scene, host);
            BistroBuilderGeneralGameStateService general =
                RequireUnique<BistroBuilderGeneralGameStateService>(scene);
            GameClock clock = RequireUnique<GameClock>(scene);
            RestaurantTableRegistry tables = RequireUnique<RestaurantTableRegistry>(scene);
            BistroBuilderBarServiceRegistry bar =
                RequireUnique<BistroBuilderBarServiceRegistry>(scene);
            BistroBuilderReputationService reputation =
                RequireUnique<BistroBuilderReputationService>(scene);
            BistroBuilderReservationService reservations =
                RequireUnique<BistroBuilderReservationService>(scene);
            BistroBuilderRestaurantMenuService menu =
                RequireUnique<BistroBuilderRestaurantMenuService>(scene);
            BistroBuilderDishAvailabilityService availability =
                RequireUnique<BistroBuilderDishAvailabilityService>(scene);
            BistroBuilderMarketingDemandIntegrationService marketing =
                RequireUnique<BistroBuilderMarketingDemandIntegrationService>(scene);

            Assign(dynamic, "generalGameStateService", general);
            Assign(dynamic, "gameClock", clock);
            Assign(dynamic, "tableRegistry", tables);
            Assign(dynamic, "barRegistry", bar);
            Assign(dynamic, "reputationService", reputation);
            Assign(dynamic, "reservationService", reservations);
            Assign(dynamic, "menuService", menu);
            Assign(dynamic, "dishAvailabilityService", availability);
            Assign(marketing, "dynamicDemandService", dynamic);

            if (!dynamic.ValidateConfiguration(out string dynamicError))
                throw new InvalidOperationException(dynamicError);
            if (!marketing.ValidateConfiguration(out string marketingError))
                throw new InvalidOperationException(marketingError);

            EditorUtility.SetDirty(dynamic);
            EditorUtility.SetDirty(marketing);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!TrySaveSceneWithRetry(scene))
                throw new InvalidOperationException("Unity no pudo guardar la instalación tras varios intentos.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BistroBuilderDynamicDemandValidationResult validation =
                BistroBuilderDynamicDemandValidator.ValidateCurrentScene();
            bool selfOk = BistroBuilderDynamicDemandSelfTest.Run(
                out int passed, out int failed, out string selfReport);
            Debug.Log(validation.BuildReport());
            Debug.Log(selfReport);
            if (validation.Errors > 0 || !selfOk)
                throw new InvalidOperationException("Demanda dinámica no superó gates: " +
                    validation.Errors + " errores / " + failed + " fallos.");

            report = "Demanda base dinámica instalada correctamente.\n" +
                     validation.BuildReport() + "\nAutotest: " + passed +
                     " OK / " + failed + " fallos.";
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            try
            {
                File.WriteAllBytes(absoluteScene, backup);
                AssetDatabase.ImportAsset(scene.path, ImportAssetOptions.ForceSynchronousImport);
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            }
            catch (Exception rollbackError) { Debug.LogException(rollbackError); }
            report = "La instalación falló y la escena fue restaurada. " + exception.Message;
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
    private static T EnsureUniqueOnHost<T>(Scene scene, GameObject host) where T : Component
    {
        T[] values = FindScene<T>(scene);
        if (values.Length > 1) throw new InvalidOperationException(
            "Hay varias instancias de " + typeof(T).Name + ".");
        T component = values.Length == 1 ? values[0] : Undo.AddComponent<T>(host);
        if (component.gameObject != host) throw new InvalidOperationException(
            typeof(T).Name + " debe vivir en GameSystems.");
        return component;
    }

    private static T RequireUnique<T>(Scene scene) where T : Component
    {
        T[] values = FindScene<T>(scene);
        if (values.Length != 1) throw new InvalidOperationException(
            "Se esperaba exactamente un " + typeof(T).Name + "; hay " + values.Length + ".");
        return values[0];
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < found.Length; i++) if (found[i] != null) list.Add(found[i]);
        }
        return list.ToArray();
    }

    private static GameObject FindUniqueNamed(Scene scene, string name)
    {
        GameObject found = null; int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name == name) { found = t.gameObject; count++; }
        return count == 1 ? found : null;
    }

    private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException(
            target.GetType().Name + " no contiene " + field + ".");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
