using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador canónico final del endurecimiento financiero 3A-3I.
/// Es autosuficiente respecto a la instalación de escena de 3I: reutiliza los
/// componentes si existen y los crea si la escena remota todavía no los tenía.
/// En cualquier fallo restaura Prototype_Restaurant byte a byte.
/// </summary>
public static class BistroBuilderFinanceHardeningFinalInstaller
{
    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3 - ENDURECIMIENTO FINAL + validar + autotest",
        false,
        3089)]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — Finanzas",
                "Sal de Play Mode antes de ejecutar el endurecimiento final.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — Finanzas",
                "Abre y guarda Prototype_Restaurant antes de continuar.",
                "Aceptar");
            return;
        }

        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — Finanzas",
                "Guarda la escena antes de ejecutar el endurecimiento final.",
                "Aceptar");
            return;
        }

        GameObject gameSystems = FindGameSystems(scene);
        if (gameSystems == null)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder — Finanzas",
                "No se encontró GameSystems en la escena activa.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);

        Undo.SetCurrentGroupName("Endurecimiento final Finanzas 3A-3I");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            BistroBuilderFinanceService finance =
                FindSingle<BistroBuilderFinanceService>(scene);
            BistroBuilderSupplierPurchaseFinanceBridge supplier =
                FindSingle<BistroBuilderSupplierPurchaseFinanceBridge>(scene);
            BistroBuilderProductCostService productCost =
                FindSingle<BistroBuilderProductCostService>(scene);
            BistroBuilderOperatingExpenseService operating =
                FindSingle<BistroBuilderOperatingExpenseService>(scene);
            BistroBuilderFinancialHistoryService history =
                FindSingle<BistroBuilderFinancialHistoryService>(scene);
            BistroBuilderGeneralGameStateService general =
                FindSingle<BistroBuilderGeneralGameStateService>(scene);
            GameClock clock = FindSingle<GameClock>(scene);
            BistroBuilderSaveGameService save =
                FindSingle<BistroBuilderSaveGameService>(scene);

            BistroBuilderFinancingService financing =
                GetOrAdd<BistroBuilderFinancingService>(gameSystems);
            SetReference(financing, "financeService", finance);
            SetReference(financing, "supplierFinanceBridge", supplier);
            SetReference(financing, "financialHistoryService", history);
            SetReference(financing, "operatingExpenseService", operating);
            SetReference(financing, "generalGameStateService", general);
            SetReference(financing, "gameClock", clock);
            SetReference(financing, "saveGameService", save);

            BistroBuilderFinancingSaveSectionProvider financingProvider =
                GetOrAdd<BistroBuilderFinancingSaveSectionProvider>(gameSystems);
            SetReference(
                financingProvider,
                "financingService",
                financing);

            EditorUtility.SetDirty(financing);
            EditorUtility.SetDirty(financingProvider);
            EditorUtility.SetDirty(productCost);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar Prototype_Restaurant después del endurecimiento.");
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
                    "El gate final no quedó limpio. Validación: " +
                    validationPassed + " OK / " + validationFailed +
                    " errores. Autotest: " + testPassed + " OK / " +
                    testFailed + " fallos.");
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder — Finanzas",
                "ENDURECIMIENTO FINANCIERO 3A-3I SUPERADO" +
                "\n\nValidación global: " + validationPassed +
                " OK / 0 errores" +
                "\nAutotest global: " + testPassed +
                " OK / 0 fallos" +
                "\n\nAhora ejecuta la Queen Test financiera global endurecida.",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, backup);
            EditorUtility.DisplayDialog(
                "Bistro Builder — Finanzas",
                "El endurecimiento final falló y la escena fue restaurada." +
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
            GameObject root = roots[index];
            if (root != null &&
                string.Equals(root.name, "GameSystems", StringComparison.Ordinal))
            {
                return root;
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
