using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instala exclusivamente las piezas añadidas por el endurecimiento 3A-3I,
/// conserva todos los sistemas existentes y revierte la escena byte a byte
/// si validación o autotest no quedan completamente limpios.
/// </summary>
public static class BistroBuilderFinanceHardeningInstaller
{
    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3 - Endurecer + validar + autotest",
        false,
        3090)]
    private static void InstallValidateAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de endurecer Finanzas.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Abre y guarda Prototype_Restaurant antes de continuar.",
                "Aceptar");
            return;
        }
        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Guarda la escena antes de ejecutar el endurecimiento financiero.",
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
        Undo.SetCurrentGroupName("Endurecer Finanzas 3A-3I");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            BistroBuilderFinanceService finance =
                FindSingle<BistroBuilderFinanceService>(scene);
            BistroBuilderSupplierPurchaseFinanceBridge supplier =
                FindSingle<BistroBuilderSupplierPurchaseFinanceBridge>(scene);
            BistroBuilderFinancialHistoryService history =
                FindSingle<BistroBuilderFinancialHistoryService>(scene);
            BistroBuilderOperatingExpenseService operating =
                FindSingle<BistroBuilderOperatingExpenseService>(scene);
            BistroBuilderGeneralGameStateService general =
                FindSingle<BistroBuilderGeneralGameStateService>(scene);
            GameClock clock = FindSingle<GameClock>(scene);
            BistroBuilderSaveGameService save =
                FindSingle<BistroBuilderSaveGameService>(scene);
            BistroBuilderInventoryService inventory =
                FindSingle<BistroBuilderInventoryService>(scene);
            BistroBuilderRecipeCatalogService recipes =
                FindSingle<BistroBuilderRecipeCatalogService>(scene);
            BistroBuilderFinancingService financing =
                FindSingle<BistroBuilderFinancingService>(scene);

            SetReference(financing, "financeService", finance);
            SetReference(financing, "supplierFinanceBridge", supplier);
            SetReference(financing, "financialHistoryService", history);
            SetReference(financing, "operatingExpenseService", operating);
            SetReference(financing, "generalGameStateService", general);
            SetReference(financing, "gameClock", clock);
            SetReference(financing, "saveGameService", save);

            BistroBuilderInventoryLossFinanceBridge lossBridge =
                GetOrAdd<BistroBuilderInventoryLossFinanceBridge>(gameSystems);
            SetReference(lossBridge, "financeService", finance);
            SetReference(lossBridge, "inventoryService", inventory);
            SetReference(lossBridge, "recipeCatalogService", recipes);
            SetReference(lossBridge, "generalGameStateService", general);
            SetReference(lossBridge, "gameClock", clock);
            SetReference(lossBridge, "saveGameService", save);

            EditorUtility.SetDirty(financing);
            EditorUtility.SetDirty(lossBridge);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena endurecida.");
            }

            save.RefreshExtensions();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool validationOk =
                BistroBuilderFinanceHardeningValidator.ValidateCurrentScene(
                    out int validationPassed,
                    out int validationFailed,
                    out string validationReport);
            bool testsOk = BistroBuilderFinanceHardeningSelfTest.Run(
                out int testPassed,
                out int testFailed,
                out string testReport);

            Debug.Log(validationReport);
            Debug.Log(testReport);

            if (!validationOk || !testsOk)
            {
                throw new InvalidOperationException(
                    "El endurecimiento financiero no quedó limpio. " +
                    "Validación: " + validationPassed + " OK / " +
                    validationFailed + " errores. Autotest global: " +
                    testPassed + " OK / " + testFailed + " fallos.");
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder — Finanzas",
                "Endurecimiento 3A-3I instalado correctamente." +
                "\n\nValidación global: " + validationPassed + " OK / 0 errores" +
                "\nAutotest global: " + testPassed + " OK / 0 fallos" +
                "\n\nSiguiente gate: QUEEN TEST FINANCIERA GLOBAL.",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, backup);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "El endurecimiento financiero falló y la escena fue restaurada." +
                "\n\n" + exception.Message,
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
                string.Equals(
                    roots[index].name,
                    "GameSystems",
                    StringComparison.Ordinal))
            {
                return roots[index];
            }
        }
        return null;
    }

    private static T FindSingle<T>(Scene scene) where T : Component
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

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(target);
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
                "No existe el campo " + fieldName + " en " +
                target.GetType().Name + ".");
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
