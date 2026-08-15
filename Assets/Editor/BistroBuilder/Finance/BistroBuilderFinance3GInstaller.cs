using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderFinance3GInstaller
{
    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3G - Instalar + validar + autotest",
        false,
        3060)]
    private static void InstallValidateAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 3G.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Abre y guarda Prototype_Restaurant antes de instalar 3G.",
                "Aceptar");
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Guarda la escena antes de instalar 3G.",
                "Aceptar");
            return;
        }

        GameObject gameSystems = FindGameSystems(scene);
        if (gameSystems == null)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "No se encontró GameSystems en la escena activa.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);

        Undo.SetCurrentGroupName("Instalar 3G Resultados por servicio y día");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            BistroBuilderFinanceService finance =
                FindSingleSceneComponent<BistroBuilderFinanceService>(scene);
            BistroBuilderProductCostService productCost =
                FindSingleSceneComponent<BistroBuilderProductCostService>(scene);
            BistroBuilderGeneralGameStateService generalState =
                FindSingleSceneComponent<BistroBuilderGeneralGameStateService>(scene);
            BistroBuilderMenuOfferService menuOffer =
                FindSingleSceneComponent<BistroBuilderMenuOfferService>(scene);
            RestaurantServiceStateService serviceState =
                FindSingleSceneComponent<RestaurantServiceStateService>(scene);

            BistroBuilderFinancialResultsService results =
                GetOrAdd<BistroBuilderFinancialResultsService>(gameSystems);
            SetReference(results, "financeService", finance);
            SetReference(results, "productCostService", productCost);
            SetReference(results, "generalGameStateService", generalState);
            SetReference(results, "menuOfferService", menuOffer);
            SetReference(results, "serviceStateService", serviceState);

            EditorUtility.SetDirty(results);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 3G.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool validationOk = BistroBuilderFinance3GValidator.ValidateCurrentScene(
                out int validationPassed,
                out int validationFailed,
                out string validationReport);
            bool testOk = BistroBuilderFinance3GSelfTest.Run(
                out int testPassed,
                out int testFailed,
                out string testReport);

            Debug.Log(validationReport);
            Debug.Log(testReport);

            if (!validationOk || !testOk)
            {
                throw new InvalidOperationException(
                    "La validación automática de 3G no fue limpia. " +
                    "Validación: " + validationPassed + " OK / " +
                    validationFailed + " errores. Autotest: " +
                    testPassed + " OK / " + testFailed + " fallos.");
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder — 3G",
                "3G instalado y probado correctamente." +
                "\n\nValidación: " + validationPassed + " OK / 0 errores" +
                "\nAutotest: " + testPassed + " OK / 0 fallos",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, backup);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación 3G falló y la escena fue restaurada.\n\n" +
                exception.Message,
                "Aceptar");
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static GameObject FindGameSystems(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            if (roots[index] != null &&
                string.Equals(roots[index].name, "GameSystems", StringComparison.Ordinal))
            {
                return roots[index];
            }
        }
        return null;
    }

    private static T FindSingleSceneComponent<T>(Scene scene)
        where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        T found = null;

        for (int index = 0; index < all.Length; index++)
        {
            T candidate = all[index];
            if (candidate == null || candidate.gameObject.scene != scene)
            {
                continue;
            }

            if (found != null)
            {
                throw new InvalidOperationException(
                    "Existe más de un " + typeof(T).Name + " en la escena.");
            }
            found = candidate;
        }

        if (found == null)
        {
            throw new InvalidOperationException(
                "No se encontró " + typeof(T).Name + " en la escena.");
        }

        return found;
    }

    private static T GetOrAdd<T>(GameObject target)
        where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null
            ? existing
            : Undo.AddComponent<T>(target);
    }

    private static void SetReference(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe el campo " + fieldName + " en " + target.GetType().Name + ".");
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RestoreScene(
        string scenePath,
        string absoluteScenePath,
        byte[] backup)
    {
        try
        {
            File.WriteAllBytes(absoluteScenePath, backup);
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        catch (Exception rollbackError)
        {
            Debug.LogException(rollbackError);
        }
    }
}
