using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderFinance3IValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3I - Validar",
        false,
        3081)]
    private static void ValidateMenu()
    {
        bool ok = ValidateCurrentScene(
            out int passed,
            out int failed,
            out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3I",
            "Validación: " + passed + " OK / " + failed + " errores",
            "Aceptar");
    }

    public static bool ValidateCurrentScene(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var builder = new StringBuilder();
        builder.AppendLine("Bistro Builder — Validación 3I endurecida");

        Scene scene = SceneManager.GetActiveScene();
        Check(scene.IsValid() && scene.isLoaded,
            "Escena activa válida", ref passed, ref failed, builder);

        BistroBuilderFinanceService[] finance =
            FindScene<BistroBuilderFinanceService>(scene);
        BistroBuilderSupplierPurchaseFinanceBridge[] supplier =
            FindScene<BistroBuilderSupplierPurchaseFinanceBridge>(scene);
        BistroBuilderFinancialHistoryService[] history =
            FindScene<BistroBuilderFinancialHistoryService>(scene);
        BistroBuilderOperatingExpenseService[] operating =
            FindScene<BistroBuilderOperatingExpenseService>(scene);
        BistroBuilderGeneralGameStateService[] general =
            FindScene<BistroBuilderGeneralGameStateService>(scene);
        GameClock[] clocks = FindScene<GameClock>(scene);
        BistroBuilderSaveGameService[] saveGames =
            FindScene<BistroBuilderSaveGameService>(scene);
        BistroBuilderFinancingService[] financing =
            FindScene<BistroBuilderFinancingService>(scene);
        BistroBuilderFinancingSaveSectionProvider[] providers =
            FindScene<BistroBuilderFinancingSaveSectionProvider>(scene);
        BistroBuilderInventoryLossFinanceBridge[] lossBridges =
            FindScene<BistroBuilderInventoryLossFinanceBridge>(scene);

        Check(finance.Length == 1,
            "Una autoridad de caja 3A", ref passed, ref failed, builder);
        Check(supplier.Length == 1,
            "Un puente de compromisos 3C", ref passed, ref failed, builder);
        Check(history.Length == 1,
            "Un histórico financiero 3H", ref passed, ref failed, builder);
        Check(operating.Length == 1,
            "Una autoridad de gastos 3E", ref passed, ref failed, builder);
        Check(general.Length == 1,
            "Un calendario canónico", ref passed, ref failed, builder);
        Check(clocks.Length == 1,
            "Un reloj canónico", ref passed, ref failed, builder);
        Check(saveGames.Length == 1,
            "Un SaveGame canónico", ref passed, ref failed, builder);
        Check(financing.Length == 1,
            "Una autoridad de financiación 3I", ref passed, ref failed, builder);
        Check(providers.Length == 1,
            "Un proveedor Save de financiación", ref passed, ref failed, builder);
        Check(lossBridges.Length == 1,
            "Un puente de bajas económicas de inventario", ref passed, ref failed, builder);

        if (financing.Length == 1)
        {
            BistroBuilderFinancingService service = financing[0];
            Check(service.ValidateConfiguration(out _),
                "Configuración 3I válida", ref passed, ref failed, builder);
            Check(finance.Length == 1 &&
                  ReferenceEquals(service.FinanceService, finance[0]),
                "3I usa la caja canónica 3A", ref passed, ref failed, builder);
            Check(supplier.Length == 1 &&
                  ReferenceEquals(service.SupplierFinanceBridge, supplier[0]),
                "3I usa compromisos canónicos 3C", ref passed, ref failed, builder);
            Check(history.Length == 1 &&
                  ReferenceEquals(service.FinancialHistoryService, history[0]),
                "3I usa históricos 3H", ref passed, ref failed, builder);
            Check(operating.Length == 1 &&
                  ReferenceEquals(service.OperatingExpenseService, operating[0]),
                "3I proyecta obligaciones recurrentes 3E", ref passed, ref failed, builder);
            Check(general.Length == 1 &&
                  ReferenceEquals(service.GeneralGameStateService, general[0]),
                "3I comparte calendario canónico", ref passed, ref failed, builder);
            Check(saveGames.Length == 1 &&
                  ReferenceEquals(service.SaveGameService, saveGames[0]),
                "3I respeta el cerrojo Save/Load", ref passed, ref failed, builder);

            var offers = new List<BistroBuilderFinancingOfferDefinition>();
            service.CopyOffers(offers);
            Check(offers.Count == 3,
                "Tres ofertas base de financiación", ref passed, ref failed, builder);
            Check(BistroBuilderFinancingEngine.TryValidateOffers(offers, out _),
                "Ofertas base estructuralmente válidas", ref passed, ref failed, builder);
        }

        if (providers.Length == 1)
        {
            Check(providers[0].ValidateConfiguration(out _),
                "Proveedor Save 3I válido", ref passed, ref failed, builder);
            Check(providers[0].SectionId ==
                      BistroBuilderFinancingSnapshot.CurrentSchemaId &&
                  providers[0].SectionVersion ==
                      BistroBuilderFinancingSnapshot.CurrentSchemaVersion,
                "Contrato Save finance.financing.runtime v1",
                ref passed, ref failed, builder);
        }

        if (lossBridges.Length == 1)
        {
            Check(lossBridges[0].ValidateConfiguration(out _),
                "Bajas económicas de inventario configuradas",
                ref passed, ref failed, builder);
            Check(finance.Length == 1 &&
                  ReferenceEquals(lossBridges[0].FinanceService, finance[0]),
                "Bajas usan finance.runtime canónico",
                ref passed, ref failed, builder);
        }

        int sameSectionProviders = 0;
        MonoBehaviour[] behaviours =
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];
            if (behaviour == null || behaviour.gameObject.scene != scene)
            {
                continue;
            }
            if (behaviour is IBistroBuilderSaveSectionProvider saveProvider &&
                string.Equals(
                    saveProvider.SectionId,
                    BistroBuilderFinancingSnapshot.CurrentSchemaId,
                    StringComparison.Ordinal))
            {
                sameSectionProviders++;
            }
        }
        Check(sameSectionProviders == 1,
            "Sin sección Save financiera duplicada",
            ref passed, ref failed, builder);

        report = builder.ToString();
        return failed == 0;
    }

    private static T[] FindScene<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var result = new List<T>();
        for (int index = 0; index < all.Length; index++)
        {
            if (all[index] != null && all[index].gameObject.scene == scene)
            {
                result.Add(all[index]);
            }
        }
        return result.ToArray();
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
