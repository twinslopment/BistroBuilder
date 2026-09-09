using System;
using System.IO;
using BistroBuilder.CameraSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Instalador transaccional e idempotente de 3J. No instala ni repara la
/// contabilidad 3A-3I: exige que sus autoridades existan y añade únicamente
/// la fachada de dashboard y la capa Presentation de Finanzas/Caja.
/// </summary>
public static class BistroBuilderFinance3JInstaller
{
    public const string UiRootName = "BB_3J_FinanceUI";

    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3J - Instalar + validar + autotest",
        false,
        3100)]
    private static void InstallValidateAndTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de instalar 3J.",
                "Aceptar");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Abre y guarda Prototype_Restaurant antes de instalar 3J.",
                "Aceptar");
            return;
        }
        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Guarda la escena antes de instalar 3J.",
                "Aceptar");
            return;
        }

        string scenePath = scene.path;
        string absoluteScenePath = Path.GetFullPath(scenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);
        Undo.SetCurrentGroupName("Instalar 3J Finanzas y Caja");
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            GameObject gameSystems = FindGameSystems(scene);
            if (gameSystems == null)
            {
                throw new InvalidOperationException(
                    "No se encontró GameSystems en la escena activa.");
            }

            BistroBuilderFinanceService finance =
                RequireScene<BistroBuilderFinanceService>(scene);
            BistroBuilderFinancialResultsService results =
                RequireScene<BistroBuilderFinancialResultsService>(scene);
            BistroBuilderFinancialHistoryService history =
                RequireScene<BistroBuilderFinancialHistoryService>(scene);
            BistroBuilderFinancingService financing =
                RequireScene<BistroBuilderFinancingService>(scene);
            BistroBuilderGeneralGameStateService general =
                RequireScene<BistroBuilderGeneralGameStateService>(scene);

            if (!finance.ValidateConfiguration(out string baseError) ||
                !results.ValidateConfiguration(out baseError) ||
                !history.ValidateConfiguration(out baseError) ||
                !financing.ValidateConfiguration(out baseError))
            {
                throw new InvalidOperationException(
                    "La base financiera 3A-3I no está lista para 3J. " + baseError);
            }

            BistroBuilderFinanceDashboardService dashboard =
                GetOrAdd<BistroBuilderFinanceDashboardService>(gameSystems);
            SetReference(dashboard, "financeService", finance);
            SetReference(dashboard, "resultsService", results);
            SetReference(dashboard, "historyService", history);
            SetReference(dashboard, "financingService", financing);
            SetReference(dashboard, "generalGameStateService", general);

            Canvas canvas = FindCanonicalHudCanvas(scene);
            if (canvas == null)
            {
                throw new InvalidOperationException(
                    "No se encontró el Canvas canónico bajo MainHUD.");
            }
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
            }

            RectTransform uiRoot = FindOrCreateUiRoot(canvas.transform);
            NormalizeUiRoot(uiRoot);

            BistroBuilderFinanceRuntimeView view =
                GetOrAdd<BistroBuilderFinanceRuntimeView>(uiRoot.gameObject);
            SetReference(view, "dashboardService", dashboard);
            SetReference(
                view,
                "cameraController",
                FindSingleSceneComponent<BistroBuilderProfessionalCameraController>(scene));
            SetReference(
                view,
                "editInteractionController",
                FindSingleSceneComponent<RestaurantEditInteractionController>(scene));
            SetBoolean(view, "showOpenButton", true);

            BistroBuilderFinanceUiModalCoordinator coordinator =
                GetOrAdd<BistroBuilderFinanceUiModalCoordinator>(uiRoot.gameObject);
            SetReference(coordinator, "financeView", view);

            if (!dashboard.ValidateConfiguration(out string error) ||
                !view.ValidateConfiguration(out error))
            {
                throw new InvalidOperationException(error);
            }

            EditorUtility.SetDirty(dashboard);
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(coordinator);
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "Unity no pudo guardar la escena tras instalar 3J.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool validationOk = BistroBuilderFinance3JValidator.ValidateCurrentScene(
                out int validationPassed,
                out int validationFailed,
                out string validationReport);
            bool testOk = BistroBuilderFinance3JSelfTest.Run(
                out int testPassed,
                out int testFailed,
                out string testReport);

            Debug.Log(validationReport);
            Debug.Log(testReport);

            if (!validationOk || !testOk)
            {
                throw new InvalidOperationException(
                    "La validación automática de 3J no fue limpia. " +
                    "Validación: " + validationPassed + " OK / " +
                    validationFailed + " errores. Autotest: " +
                    testPassed + " OK / " + testFailed + " fallos.");
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder — 3J",
                "3J instalado y probado correctamente." +
                "\n\nValidación: " + validationPassed + " OK / 0 errores" +
                "\nAutotest: " + testPassed + " OK / 0 fallos" +
                "\n\nEl cierre sigue condicionado a la Queen Test 3A-3I y a la prueba runtime 3J.",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreScene(scenePath, absoluteScenePath, backup);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "La instalación 3J falló y la escena fue restaurada.\n\n" +
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

    private static Canvas FindCanonicalHudCanvas(Scene scene)
    {
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int index = 0; index < canvases.Length; index++)
        {
            Canvas canvas = canvases[index];
            if (canvas == null || canvas.gameObject.scene != scene)
            {
                continue;
            }
            Transform parent = canvas.transform.parent;
            if (string.Equals(canvas.name, "Canvas", StringComparison.Ordinal) &&
                parent != null &&
                string.Equals(parent.name, "MainHUD", StringComparison.Ordinal))
            {
                return canvas;
            }
        }
        return null;
    }

    private static RectTransform FindOrCreateUiRoot(Transform canvas)
    {
        Transform child = canvas.Find(UiRootName);
        if (child != null)
        {
            RectTransform existing = child as RectTransform;
            if (existing == null)
            {
                throw new InvalidOperationException(
                    UiRootName + " existe, pero no tiene RectTransform.");
            }
            return existing;
        }

        GameObject created = new GameObject(UiRootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(created, "Crear UI Finanzas 3J");
        RectTransform rect = created.GetComponent<RectTransform>();
        rect.SetParent(canvas, false);
        created.layer = canvas.gameObject.layer;
        return rect;
    }

    private static void NormalizeUiRoot(RectTransform root)
    {
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
        if (root.parent != null)
        {
            root.gameObject.layer = root.parent.gameObject.layer;
        }
        root.SetAsLastSibling();
    }

    private static T RequireScene<T>(Scene scene) where T : Component
    {
        T found = FindSingleSceneComponent<T>(scene);
        if (found == null)
        {
            throw new InvalidOperationException(
                "Falta una única instancia de " + typeof(T).Name + " en la escena.");
        }
        return found;
    }

    private static T FindSingleSceneComponent<T>(Scene scene) where T : Component
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
                return null;
            }
            found = candidate;
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
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad " + propertyName + " en " +
                target.GetType().Name + ".");
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBoolean(
        UnityEngine.Object target,
        string propertyName,
        bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.Boolean)
        {
            throw new InvalidOperationException(
                "No existe el bool " + propertyName + " en " +
                target.GetType().Name + ".");
        }
        property.boolValue = value;
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
