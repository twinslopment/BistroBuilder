using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderOrderInventoryLifecycleSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Run 368CD Order Inventory Self-Test";

    [MenuItem(MenuPath, false, 342)]
    private static void Run()
    {
        int passed = 0;
        int failed = 0;
        var lines = new List<string>();
        Action<bool, string> check = (condition, text) =>
        {
            if (condition) { passed++; lines.Add("- OK: " + text); }
            else { failed++; lines.Add("- ERROR: " + text); }
        };

        BistroBuilderOrderInventoryLifecycleValidationResult validation =
            BistroBuilderOrderInventoryLifecycleValidator.ValidateCurrentProject();
        check(validation.ErrorCount == 0, "La instalación 368CD supera el validador.");
        check(BistroBuilderOrderInventoryLifecycleService.DivideCeiling(1000, 1) == 1000, "Rendimiento 1 conserva la cantidad.");
        check(BistroBuilderOrderInventoryLifecycleService.DivideCeiling(1000, 4) == 250, "La división exacta por rendimiento es correcta.");
        check(BistroBuilderOrderInventoryLifecycleService.DivideCeiling(1001, 4) == 251, "La división no exacta redondea hacia arriba.");

        string reservationA = BistroBuilderOrderInventoryLifecycleService.BuildReservationId("order_1", "line_1");
        string reservationB = BistroBuilderOrderInventoryLifecycleService.BuildReservationId("order_1", "line_1");
        string reservationC = BistroBuilderOrderInventoryLifecycleService.BuildReservationId("order_1", "line_2");
        check(reservationA == reservationB, "La identidad de reserva es determinista.");
        check(reservationA != reservationC, "Dos líneas no comparten reserva.");
        check(reservationA.StartsWith("inventory_reservation_", StringComparison.Ordinal), "La identidad usa un prefijo estable.");

        string reserveOp = BistroBuilderOrderInventoryLifecycleService.BuildOperationId("reserve", "order_1", "line_1", string.Empty);
        string consumeOp = BistroBuilderOrderInventoryLifecycleService.BuildOperationId("consume", "order_1", "line_1", string.Empty);
        string releaseOp = BistroBuilderOrderInventoryLifecycleService.BuildOperationId("release", "order_1", "line_1", string.Empty);
        check(reserveOp != consumeOp, "Reserva y consumo tienen OperationId distintos.");
        check(consumeOp != releaseOp, "Consumo y liberación tienen OperationId distintos.");
        check(reserveOp == BistroBuilderOrderInventoryLifecycleService.BuildOperationId("reserve", "order_1", "line_1", string.Empty), "OperationId es idempotente.");

        BistroBuilderRecipeCatalogService recipeService = UnityEngine.Object.FindFirstObjectByType<BistroBuilderRecipeCatalogService>();
        check(recipeService != null, "Existe el catálogo runtime de recetas.");
        if (recipeService != null)
        {
            string[] dishIds = {
                "dish_fabada_asturiana", "dish_merluza_plancha", "dish_tarta_queso",
                "dish_agua_mineral", "dish_refresco", "dish_copa_vino",
                "dish_aceitunas_alinadas", "dish_pincho_tortilla"
            };
            for (int i = 0; i < dishIds.Length; i++)
            {
                check(recipeService.TryGetRecipeByDishId(dishIds[i], out BistroBuilderRecipeDefinition recipe),
                    "Existe receta para " + dishIds[i] + ".");
                if (recipe != null)
                {
                    check(recipe.YieldPortions > 0, "El rendimiento de " + dishIds[i] + " es positivo.");
                    check(recipe.Ingredients.Count > 0, "La receta de " + dishIds[i] + " contiene ingredientes.");
                }
            }
        }

        BistroBuilderInventoryService inventory = UnityEngine.Object.FindFirstObjectByType<BistroBuilderInventoryService>();
        check(inventory != null, "Existe el inventario canónico.");
        check(inventory != null && inventory.IsInitialized, "El inventario está inicializado.");
        check(inventory != null && inventory.StockEntryCount == 22, "El inventario conserva 22 ingredientes.");

        string report = "BISTRO BUILDER - AUTOTEST 368CD\nPruebas superadas: " + passed +
                        "\nPruebas fallidas: " + failed + "\n" + string.Join("\n", lines);
        if (failed > 0) Debug.LogError(report); else Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }
}
