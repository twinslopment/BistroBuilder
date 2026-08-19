using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gate estructural de 3J. Comprueba que la UI solo proyecta autoridades
/// 3A/3G/3H/3I y que no introduce estado financiero persistente paralelo.
/// </summary>
public static class BistroBuilderFinance3JValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3J - Validar",
        false,
        3101)]
    private static void ValidateMenu()
    {
        bool ok = ValidateCurrentScene(
            out int passed,
            out int failed,
            out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3J",
            "Validación: " + passed + " OK / " + failed + " errores",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("La validación estructural 3J ha fallado.");
        }
    }

    public static bool ValidateCurrentScene(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var builder = new StringBuilder();
        builder.AppendLine("=== BISTRO BUILDER — VALIDACIÓN 3J FINANZAS Y CAJA ===");

        Scene scene = SceneManager.GetActiveScene();
        Check(scene.IsValid() && scene.isLoaded,
            "Escena activa válida", ref passed, ref failed, builder);

        bool hardeningOk = BistroBuilderFinanceHardeningValidator.ValidateCurrentScene(
            out int hardeningPassed,
            out int hardeningFailed,
            out string hardeningReport);
        Check(hardeningOk && hardeningFailed == 0,
            "Base endurecida 3A-3I estructuralmente limpia (" +
            hardeningPassed + " OK)",
            ref passed, ref failed, builder);
        if (!hardeningOk)
        {
            builder.AppendLine(hardeningReport);
        }

        BistroBuilderFinanceService finance = Single<BistroBuilderFinanceService>(
            scene, "FinanceService 3A", ref passed, ref failed, builder);
        BistroBuilderFinancialResultsService results =
            Single<BistroBuilderFinancialResultsService>(
                scene, "FinancialResultsService 3G", ref passed, ref failed, builder);
        BistroBuilderFinancialHistoryService history =
            Single<BistroBuilderFinancialHistoryService>(
                scene, "FinancialHistoryService 3H", ref passed, ref failed, builder);
        BistroBuilderFinancingService financing =
            Single<BistroBuilderFinancingService>(
                scene, "FinancingService 3I", ref passed, ref failed, builder);
        BistroBuilderGeneralGameStateService general =
            Single<BistroBuilderGeneralGameStateService>(
                scene, "calendario canónico", ref passed, ref failed, builder);
        BistroBuilderFinanceDashboardService dashboard =
            Single<BistroBuilderFinanceDashboardService>(
                scene, "FinanceDashboardService 3J", ref passed, ref failed, builder);
        BistroBuilderFinanceRuntimeView view =
            Single<BistroBuilderFinanceRuntimeView>(
                scene, "FinanceRuntimeView 3J", ref passed, ref failed, builder);
        BistroBuilderFinanceUiModalCoordinator coordinator =
            Single<BistroBuilderFinanceUiModalCoordinator>(
                scene, "FinanceUiModalCoordinator 3J", ref passed, ref failed, builder);

        Check(dashboard != null && dashboard.ValidateConfiguration(out _),
            "Dashboard 3J configurado", ref passed, ref failed, builder);
        Check(view != null && view.ValidateConfiguration(out _),
            "Vista 3J configurada", ref passed, ref failed, builder);

        Check(dashboard != null && finance != null &&
              ReferenceEquals(dashboard.FinanceService, finance),
            "3J lee la única caja 3A", ref passed, ref failed, builder);
        Check(dashboard != null && results != null &&
              ReferenceEquals(dashboard.ResultsService, results),
            "3J lee Resultados 3G", ref passed, ref failed, builder);
        Check(dashboard != null && history != null &&
              ReferenceEquals(dashboard.HistoryService, history),
            "3J lee Históricos 3H", ref passed, ref failed, builder);
        Check(dashboard != null && financing != null &&
              ReferenceEquals(dashboard.FinancingService, financing),
            "3J canaliza financiación únicamente por 3I",
            ref passed, ref failed, builder);
        Check(dashboard != null && general != null &&
              ReferenceEquals(dashboard.GeneralGameStateService, general),
            "3J comparte calendario canónico", ref passed, ref failed, builder);
        Check(view != null && dashboard != null &&
              ReferenceEquals(view.DashboardService, dashboard),
            "Vista 3J solo depende de la fachada de dashboard",
            ref passed, ref failed, builder);
        Check(coordinator != null && view != null &&
              ReferenceEquals(coordinator.FinanceView, view),
            "Coordinador modal enlazado a la vista 3J",
            ref passed, ref failed, builder);

        Canvas canvas = FindCanonicalHudCanvas(scene);
        Check(canvas != null,
            "Canvas HUD canónico disponible", ref passed, ref failed, builder);

        RectTransform uiRoot = canvas != null
            ? canvas.transform.Find(BistroBuilderFinance3JInstaller.UiRootName)
                as RectTransform
            : null;
        Check(uiRoot != null,
            "Existe un único root BB_3J_FinanceUI bajo el HUD",
            ref passed, ref failed, builder);
        Check(uiRoot != null && view != null && coordinator != null &&
              ReferenceEquals(view.gameObject, uiRoot.gameObject) &&
              ReferenceEquals(coordinator.gameObject, uiRoot.gameObject),
            "Vista y coordinador viven en el root 3J",
            ref passed, ref failed, builder);

        Check(dashboard != null &&
              string.Equals(dashboard.gameObject.name, "GameSystems", StringComparison.Ordinal),
            "Dashboard 3J vive en GameSystems",
            ref passed, ref failed, builder);

        int financeProviders = CountSectionProviders(
            scene,
            BistroBuilderFinanceSnapshot.CurrentSchemaId);
        int productCostProviders = CountSectionProviders(
            scene,
            BistroBuilderProductCostSnapshot.CurrentSchemaId);
        int financingProviders = CountSectionProviders(
            scene,
            BistroBuilderFinancingSnapshot.CurrentSchemaId);
        Check(financeProviders == 1 &&
              productCostProviders == 1 &&
              financingProviders == 1,
            "3J no duplica las tres secciones Save financieras existentes",
            ref passed, ref failed, builder);

        Check(!HasForbidden3JSaveProvider(scene),
            "3J no introduce ledger, snapshot ni sección Save propia",
            ref passed, ref failed, builder);

        Check(BistroBuilderFinanceRuntimeView.RuntimeRevision == "FINANCE-3J-UI-V1",
            "Revisión runtime 3J identificable",
            ref passed, ref failed, builder);

        report = builder.ToString();
        return failed == 0;
    }

    private static bool HasForbidden3JSaveProvider(Scene scene)
    {
        MonoBehaviour[] all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int index = 0; index < all.Length; index++)
        {
            MonoBehaviour behaviour = all[index];
            if (behaviour == null || behaviour.gameObject.scene != scene ||
                !(behaviour is IBistroBuilderSaveSectionProvider provider))
            {
                continue;
            }

            string typeName = behaviour.GetType().Name.ToLowerInvariant();
            string section = provider.SectionId != null
                ? provider.SectionId.ToLowerInvariant()
                : string.Empty;
            if (typeName.Contains("dashboard") ||
                typeName.Contains("finance3j") ||
                section.Contains("dashboard") ||
                section.Contains("finance.ui") ||
                section.Contains("finance.3j"))
            {
                return true;
            }
        }
        return false;
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

    private static T Single<T>(
        Scene scene,
        string label,
        ref int passed,
        ref int failed,
        StringBuilder builder) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        T found = null;
        int count = 0;
        for (int index = 0; index < all.Length; index++)
        {
            if (all[index] == null || all[index].gameObject.scene != scene)
            {
                continue;
            }
            found = all[index];
            count++;
        }
        Check(count == 1,
            "Único " + label,
            ref passed, ref failed, builder);
        return count == 1 ? found : null;
    }

    private static int CountSectionProviders(Scene scene, string sectionId)
    {
        int count = 0;
        MonoBehaviour[] all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int index = 0; index < all.Length; index++)
        {
            if (all[index] == null || all[index].gameObject.scene != scene)
            {
                continue;
            }
            if (all[index] is IBistroBuilderSaveSectionProvider provider &&
                string.Equals(provider.SectionId, sectionId, StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }

    private static void Check(
        bool condition,
        string label,
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        if (condition)
        {
            passed++;
            builder.AppendLine("[OK] " + label);
        }
        else
        {
            failed++;
            builder.AppendLine("[ERROR] " + label);
        }
    }
}
