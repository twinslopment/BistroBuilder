using System;
using System.IO;
using BistroBuilder.CameraSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Instalador acumulativo final 3A-3J para la escena vigente del proyecto.
/// Recupera el wiring persistente 3I/3J sin sustituir autoridades 3A-3H y
/// revierte Prototype_Restaurant byte a byte si algún gate final falla.
/// </summary>
public static class BistroBuilderFinanceFinalClosureInstaller
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string ReportPath = "Block3FinanceFinalInstall.txt";

    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3 - CIERRE FINAL 3A-3J / Instalar",
        false,
        3109)]
    private static void RunFromMenu()
    {
        bool ok = Install(out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — Finanzas",
            report,
            "Aceptar");
    }

    public static void RunFromCommandLine()
    {
        bool ok = Install(out string report);
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    public static bool Install(out string report)
    {
        report = string.Empty;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            report = "El cierre financiero debe instalarse fuera de Play Mode.";
            return false;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            report = "No se pudo abrir Prototype_Restaurant.";
            return false;
        }

        string absoluteScenePath = Path.GetFullPath(ScenePath);
        byte[] backup = File.ReadAllBytes(absoluteScenePath);
        try
        {
            GameObject gameSystems = FindUniqueRoot(scene, "GameSystems");
            if (gameSystems == null)
                throw new InvalidOperationException("Falta GameSystems canónico único.");

            BistroBuilderFinanceService finance = Require<BistroBuilderFinanceService>(scene);
            BistroBuilderSupplierPurchaseFinanceBridge supplier =
                Require<BistroBuilderSupplierPurchaseFinanceBridge>(scene);
            BistroBuilderProductCostService productCost =
                Require<BistroBuilderProductCostService>(scene);
            BistroBuilderOperatingExpenseService operating =
                Require<BistroBuilderOperatingExpenseService>(scene);
            BistroBuilderFinancialResultsService results =
                Require<BistroBuilderFinancialResultsService>(scene);
            BistroBuilderFinancialHistoryService history =
                Require<BistroBuilderFinancialHistoryService>(scene);
            BistroBuilderGeneralGameStateService general =
                Require<BistroBuilderGeneralGameStateService>(scene);
            GameClock clock = Require<GameClock>(scene);
            BistroBuilderSaveGameService save =
                Require<BistroBuilderSaveGameService>(scene);
            BistroBuilderStaffService staff =
                Require<BistroBuilderStaffService>(scene);
            BistroBuilderStaffSessionService staffSession =
                Require<BistroBuilderStaffSessionService>(scene);
            BistroBuilderStaffScheduleService schedule =
                Require<BistroBuilderStaffScheduleService>(scene);
            BistroBuilderStaffScheduleSessionBridge scheduleBridge =
                Require<BistroBuilderStaffScheduleSessionBridge>(scene);
            BistroBuilderCanonicalOrderIntegrationService orderIntegration =
                Require<BistroBuilderCanonicalOrderIntegrationService>(scene);

            BistroBuilderStaffPayrollFinanceBridge payrollBridge =
                GetOrAdd<BistroBuilderStaffPayrollFinanceBridge>(gameSystems);
            SetRef(payrollBridge, "operatingExpenseService", operating);
            SetRef(payrollBridge, "financeService", finance);
            SetRef(payrollBridge, "staffService", staff);
            SetRef(payrollBridge, "sessionService", staffSession);
            SetRef(payrollBridge, "scheduleService", schedule);
            SetRef(payrollBridge, "scheduleSessionBridge", scheduleBridge);
            SetRef(payrollBridge, "orderIntegration", orderIntegration);
            SetRef(payrollBridge, "saveGameService", save);
            SetRef(operating, "staffPayrollFinanceBridge", payrollBridge);

            BistroBuilderFinancingService financing =
                GetOrAdd<BistroBuilderFinancingService>(gameSystems);
            SetRef(financing, "financeService", finance);
            SetRef(financing, "supplierFinanceBridge", supplier);
            SetRef(financing, "financialHistoryService", history);
            SetRef(financing, "operatingExpenseService", operating);
            SetRef(financing, "generalGameStateService", general);
            SetRef(financing, "gameClock", clock);
            SetRef(financing, "saveGameService", save);

            BistroBuilderFinancingSaveSectionProvider financingProvider =
                GetOrAdd<BistroBuilderFinancingSaveSectionProvider>(gameSystems);
            SetRef(financingProvider, "financingService", financing);

            BistroBuilderFinanceDashboardService dashboard =
                GetOrAdd<BistroBuilderFinanceDashboardService>(gameSystems);
            SetRef(dashboard, "financeService", finance);
            SetRef(dashboard, "resultsService", results);
            SetRef(dashboard, "historyService", history);
            SetRef(dashboard, "financingService", financing);
            SetRef(dashboard, "generalGameStateService", general);

            Canvas canvas = FindCanonicalHudCanvas(scene);
            if (canvas == null)
                throw new InvalidOperationException(
                    "No se encontró el Canvas canónico bajo MainHUD.");
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();

            RectTransform uiRoot = FindOrCreateFinanceRoot(canvas.transform);
            BistroBuilderFinanceRuntimeView view =
                GetOrAdd<BistroBuilderFinanceRuntimeView>(uiRoot.gameObject);
            SetRef(view, "dashboardService", dashboard);
            SetRef(view, "cameraController",
                Require<BistroBuilderProfessionalCameraController>(scene));
            SetRef(view, "editInteractionController",
                Require<RestaurantEditInteractionController>(scene));
            SetBool(view, "showOpenButton", true);

            BistroBuilderFinanceUiModalCoordinator coordinator =
                GetOrAdd<BistroBuilderFinanceUiModalCoordinator>(uiRoot.gameObject);
            SetRef(coordinator, "financeView", view);

            EditorUtility.SetDirty(payrollBridge);
            EditorUtility.SetDirty(operating);
            EditorUtility.SetDirty(financing);
            EditorUtility.SetDirty(financingProvider);
            EditorUtility.SetDirty(productCost);
            EditorUtility.SetDirty(dashboard);
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(coordinator);
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException(
                    "Unity no pudo guardar Prototype_Restaurant.");

            save.RefreshExtensions();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool preflight = BistroBuilderFinanceFinalClosurePreflight.Run(
                out int passed,
                out int failed,
                out string preflightReport);
            if (!preflight || failed != 0)
                throw new InvalidOperationException(preflightReport);

            report = "CIERRE FINANCIERO 3A-3J INSTALADO\n" +
                passed + " gates acumulativos OK / 0 fallos.";
            return true;
        }
        catch (Exception exception)
        {
            try
            {
                File.WriteAllBytes(absoluteScenePath, backup);
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (Exception rollback)
            {
                report = "Fallo: " + exception.Message +
                    "\nAdemás falló rollback: " + rollback.Message;
                return false;
            }

            report = "Fallo y rollback aplicado: " + exception.Message;
            return false;
        }
    }

    private static GameObject FindUniqueRoot(Scene scene, string name)
    {
        GameObject found = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!string.Equals(root.name, name, StringComparison.Ordinal)) continue;
            if (found != null) return null;
            found = root;
        }
        return found;
    }

    private static T Require<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        T found = null;
        for (int index = 0; index < all.Length; index++)
        {
            T candidate = all[index];
            if (candidate == null || candidate.gameObject.scene != scene) continue;
            if (found != null)
                throw new InvalidOperationException(
                    "Existe más de un " + typeof(T).Name + ".");
            found = candidate;
        }
        if (found == null)
            throw new InvalidOperationException(
                "Falta " + typeof(T).Name + ".");
        return found;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null ? existing : target.AddComponent<T>();
    }

    private static Canvas FindCanonicalHudCanvas(Scene scene)
    {
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        Canvas found = null;
        for (int index = 0; index < canvases.Length; index++)
        {
            Canvas canvas = canvases[index];
            if (canvas == null || canvas.gameObject.scene != scene) continue;
            Transform parent = canvas.transform.parent;
            if (!string.Equals(canvas.name, "Canvas", StringComparison.Ordinal) ||
                parent == null ||
                !string.Equals(parent.name, "MainHUD", StringComparison.Ordinal))
                continue;
            if (found != null)
                throw new InvalidOperationException("Hay más de un Canvas HUD canónico.");
            found = canvas;
        }
        return found;
    }

    private static RectTransform FindOrCreateFinanceRoot(Transform canvas)
    {
        Transform child = canvas.Find(BistroBuilderFinance3JInstaller.UiRootName);
        RectTransform root = child as RectTransform;
        if (child != null && root == null)
            throw new InvalidOperationException("BB_3J_FinanceUI no usa RectTransform.");

        if (root == null)
        {
            GameObject created = new GameObject(
                BistroBuilderFinance3JInstaller.UiRootName,
                typeof(RectTransform));
            root = created.GetComponent<RectTransform>();
            root.SetParent(canvas, false);
        }

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
        root.gameObject.layer = canvas.gameObject.layer;
        root.SetAsLastSibling();
        return root;
    }

    private static void SetRef(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(
                "No existe " + propertyName + " en " + target.GetType().Name + ".");

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(
        UnityEngine.Object target,
        string propertyName,
        bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.propertyType != SerializedPropertyType.Boolean)
            throw new InvalidOperationException(
                "No existe bool " + propertyName + " en " + target.GetType().Name + ".");
        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
