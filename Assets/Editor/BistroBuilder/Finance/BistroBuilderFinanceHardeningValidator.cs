using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gate estructural transversal de Finanzas 3A-3I antes de construir 3J.
/// </summary>
public static class BistroBuilderFinanceHardeningValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3 - Validar bloque financiero endurecido",
        false,
        3091)]
    private static void ValidateMenu()
    {
        bool ok = ValidateCurrentScene(
            out int passed,
            out int failed,
            out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — Finanzas",
            "Validación global: " + passed + " OK / " + failed + " errores",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("La validación financiera global endurecida ha fallado.");
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
        builder.AppendLine("=== BISTRO BUILDER — VALIDACIÓN FINANCIERA GLOBAL 3A-3I ===");

        Scene scene = SceneManager.GetActiveScene();
        Check(scene.IsValid() && scene.isLoaded,
            "Escena activa válida", ref passed, ref failed, builder);

        BistroBuilderFinanceService finance = Single<BistroBuilderFinanceService>(
            scene, "3A FinanceService", ref passed, ref failed, builder);
        BistroBuilderSalesRevenueBridge sales = Single<BistroBuilderSalesRevenueBridge>(
            scene, "3B SalesRevenueBridge", ref passed, ref failed, builder);
        BistroBuilderSupplierPurchaseFinanceBridge supplier =
            Single<BistroBuilderSupplierPurchaseFinanceBridge>(
                scene, "3C SupplierFinanceBridge", ref passed, ref failed, builder);
        BistroBuilderProductCostService productCost =
            Single<BistroBuilderProductCostService>(
                scene, "3D ProductCostService", ref passed, ref failed, builder);
        BistroBuilderOperatingExpenseService operating =
            Single<BistroBuilderOperatingExpenseService>(
                scene, "3E OperatingExpenseService", ref passed, ref failed, builder);
        BistroBuilderDiscretionaryFinanceService discretionary =
            Single<BistroBuilderDiscretionaryFinanceService>(
                scene, "3F DiscretionaryFinanceService", ref passed, ref failed, builder);
        BistroBuilderPlaceableFinanceBridge placeable =
            Single<BistroBuilderPlaceableFinanceBridge>(
                scene, "3F PlaceableFinanceBridge", ref passed, ref failed, builder);
        BistroBuilderFinancialResultsService results =
            Single<BistroBuilderFinancialResultsService>(
                scene, "3G FinancialResultsService", ref passed, ref failed, builder);
        BistroBuilderFinancialHistoryService history =
            Single<BistroBuilderFinancialHistoryService>(
                scene, "3H FinancialHistoryService", ref passed, ref failed, builder);
        BistroBuilderFinancingService financing =
            Single<BistroBuilderFinancingService>(
                scene, "3I FinancingService", ref passed, ref failed, builder);
        BistroBuilderInventoryLossFinanceBridge loss =
            Single<BistroBuilderInventoryLossFinanceBridge>(
                scene, "InventoryLossFinanceBridge", ref passed, ref failed, builder);
        BistroBuilderSaveGameService save =
            Single<BistroBuilderSaveGameService>(
                scene, "SaveGameService canónico", ref passed, ref failed, builder);

        Check(finance != null && finance.ValidateConfiguration(out _),
            "3A configuración válida", ref passed, ref failed, builder);
        Check(sales != null && sales.ValidateConfiguration(out _),
            "3B configuración válida", ref passed, ref failed, builder);
        Check(supplier != null && supplier.ValidateConfiguration(out _),
            "3C configuración válida", ref passed, ref failed, builder);
        Check(productCost != null && productCost.ValidateConfiguration(out _),
            "3D configuración válida", ref passed, ref failed, builder);
        Check(operating != null && operating.ValidateConfiguration(out _),
            "3E configuración válida", ref passed, ref failed, builder);
        Check(discretionary != null && discretionary.ValidateConfiguration(out _),
            "3F gasto discrecional válido", ref passed, ref failed, builder);
        Check(placeable != null && placeable.ValidateConfiguration(out _),
            "3F economía de colocables válida", ref passed, ref failed, builder);
        Check(results != null && results.ValidateConfiguration(out _),
            "3G configuración válida", ref passed, ref failed, builder);
        Check(history != null && history.ValidateConfiguration(out _),
            "3H configuración válida", ref passed, ref failed, builder);
        Check(financing != null && financing.ValidateConfiguration(out _),
            "3I configuración endurecida válida", ref passed, ref failed, builder);
        Check(loss != null && loss.ValidateConfiguration(out _),
            "Baja económica de inventario válida", ref passed, ref failed, builder);

        Check(finance != null &&
              BistroBuilderFinanceEngine.TryValidateSnapshot(
                  finance.CreateSnapshot(), out _),
            "finance.runtime íntegro", ref passed, ref failed, builder);
        Check(productCost != null &&
              BistroBuilderProductCostEngine.TryValidateSnapshot(
                  productCost.CreateSnapshot(), out _),
            "finance.product_cost.runtime íntegro",
            ref passed, ref failed, builder);
        Check(financing != null &&
              BistroBuilderFinancingEngine.TryValidateSnapshot(
                  financing.CreateSnapshot(), out _),
            "finance.financing.runtime íntegro",
            ref passed, ref failed, builder);
        Check(financing != null && financing.TryValidateLedgerConsistency(out _),
            "Deuda y ledger son bidireccionalmente coherentes",
            ref passed, ref failed, builder);

        Check(results != null && finance != null &&
              ReferenceEquals(results.FinanceService, finance) &&
              productCost != null &&
              ReferenceEquals(results.ProductCostService, productCost),
            "3G lee exclusivamente autoridades 3A/3D",
            ref passed, ref failed, builder);
        Check(history != null && results != null &&
              ReferenceEquals(history.FinancialResultsService, results),
            "3H deriva exclusivamente de 3G",
            ref passed, ref failed, builder);
        Check(financing != null && operating != null &&
              ReferenceEquals(financing.OperatingExpenseService, operating),
            "3I incluye obligaciones deterministas 3E",
            ref passed, ref failed, builder);
        Check(financing != null && save != null &&
              ReferenceEquals(financing.SaveGameService, save),
            "3I comparte cerrojo Save/Load canónico",
            ref passed, ref failed, builder);
        Check(loss != null && finance != null &&
              ReferenceEquals(loss.FinanceService, finance),
            "Caducidad/merma publica solo en finance.runtime",
            ref passed, ref failed, builder);

        if (save != null)
        {
            save.RefreshExtensions();
            Check(save.HasProvider(BistroBuilderFinanceSnapshot.CurrentSchemaId),
                "Save incluye finance.runtime",
                ref passed, ref failed, builder);
            Check(save.HasProvider(BistroBuilderProductCostSnapshot.CurrentSchemaId),
                "Save incluye finance.product_cost.runtime",
                ref passed, ref failed, builder);
            Check(save.HasProvider(BistroBuilderFinancingSnapshot.CurrentSchemaId),
                "Save incluye finance.financing.runtime",
                ref passed, ref failed, builder);
            Check(save.ValidateConfiguration(out _),
                "SaveGame completo acepta todas las secciones financieras",
                ref passed, ref failed, builder);
        }

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
            "Sin secciones financieras Save duplicadas",
            ref passed, ref failed, builder);

        report = builder.ToString();
        return failed == 0;
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
        Check(count == 1, "Único " + label, ref passed, ref failed, builder);
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
