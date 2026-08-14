using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderFinance3FValidator
{
    [MenuItem("Tools/Bistro Builder/Finanzas/3F - Validar", false, 3051)]
    public static void RunFromMenu()
    {
        bool ok = ValidateCurrentScene(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3F",
            "Validación: " + passed + " correctos, " + failed + " errores.",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("3F — La validación estructural ha fallado.");
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
        Scene scene = SceneManager.GetActiveScene();

        Check(scene.IsValid() && scene.isLoaded,
            "Escena activa válida.", ref passed, ref failed, builder);

        BistroBuilderFinanceService finance = FindSingle<BistroBuilderFinanceService>(scene);
        BistroBuilderSupplierPurchaseFinanceBridge supplier =
            FindSingle<BistroBuilderSupplierPurchaseFinanceBridge>(scene);
        BistroBuilderDiscretionaryFinanceService discretionary =
            FindSingle<BistroBuilderDiscretionaryFinanceService>(scene);
        BistroBuilderPlaceableFinanceBridge placeables =
            FindSingle<BistroBuilderPlaceableFinanceBridge>(scene);
        BistroBuilderMoneyPopupService popups =
            FindSingle<BistroBuilderMoneyPopupService>(scene);
        RestaurantPlaceableCreationService creation =
            FindSingle<RestaurantPlaceableCreationService>(scene);
        RestaurantPlaceableDeletionService deletion =
            FindSingle<RestaurantPlaceableDeletionService>(scene);
        RestaurantPlacementHistoryService history =
            FindSingle<RestaurantPlacementHistoryService>(scene);

        Check(finance != null,
            "Existe una única autoridad finance.runtime.", ref passed, ref failed, builder);
        Check(supplier != null,
            "Existe 3C para respetar compromisos de proveedor.", ref passed, ref failed, builder);
        Check(discretionary != null,
            "Existe un único contrato de gasto discrecional 3F.", ref passed, ref failed, builder);
        Check(placeables != null,
            "Existe un único puente económico de colocables.", ref passed, ref failed, builder);
        Check(popups != null,
            "Existe un único servicio de popup monetario.", ref passed, ref failed, builder);
        Check(creation != null && deletion != null && history != null,
            "Creación, eliminación e historial canónicos siguen presentes.", ref passed, ref failed, builder);

        if (discretionary != null)
        {
            Check(ReferencesAssigned(
                    discretionary,
                    "financeService",
                    "supplierFinanceBridge",
                    "generalGameStateService",
                    "gameClock"),
                "Gasto discrecional tiene todas sus dependencias.",
                ref passed, ref failed, builder);
        }
        else
        {
            Check(false, "Gasto discrecional tiene todas sus dependencias.",
                ref passed, ref failed, builder);
        }

        if (placeables != null)
        {
            Check(ReferencesAssigned(
                    placeables,
                    "financeService",
                    "discretionaryFinanceService",
                    "generalGameStateService",
                    "gameClock",
                    "creationService",
                    "deletionService",
                    "historyService",
                    "moneyPopupService"),
                "Puente de colocables tiene todas sus dependencias.",
                ref passed, ref failed, builder);
        }
        else
        {
            Check(false, "Puente de colocables tiene todas sus dependencias.",
                ref passed, ref failed, builder);
        }

        Check(popups == null || popups.ValidateConfiguration(out _),
            "Configuración visual del popup es válida.", ref passed, ref failed, builder);

        Check(BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory("expense.marketing.local") &&
              BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory("investment.renovation") &&
              !BistroBuilderDiscretionaryFinancePolicy.IsAllowedCategory("sales.invalid"),
            "Contrato 3F acepta solo familias financieras previstas.",
            ref passed, ref failed, builder);

        Check(BistroBuilderPlaceableFinancePolicy.DefaultResaleBasisPoints == 5000 &&
              BistroBuilderPlaceableFinancePolicy.DefaultDemolitionBasisPoints == 1500,
            "Política base: reventa 50 % y demolición 15 %.",
            ref passed, ref failed, builder);

        Check(FindSingle<BistroBuilderFinanceSaveSectionProvider>(scene) != null &&
              FindSingle<BistroBuilderProductCostSaveSectionProvider>(scene) != null,
            "3F reutiliza persistencia financiera existente; no crea otro ledger.",
            ref passed, ref failed, builder);

        builder.Insert(0,
            "3F — VALIDACIÓN ESTRUCTURAL\nCorrectos: " + passed +
            "  Errores: " + failed + "\n\n");
        report = builder.ToString();
        return failed == 0;
    }

    private static bool ReferencesAssigned(UnityEngine.Object target, params string[] names)
    {
        var serialized = new SerializedObject(target);
        for (int index = 0; index < names.Length; index++)
        {
            SerializedProperty property = serialized.FindProperty(names[index]);
            if (property == null || property.objectReferenceValue == null)
            {
                return false;
            }
        }
        return true;
    }

    private static T FindSingle<T>(Scene scene) where T : Component
    {
        T found = null;
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
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
