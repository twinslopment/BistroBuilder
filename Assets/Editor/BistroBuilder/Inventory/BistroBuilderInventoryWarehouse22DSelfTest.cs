using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest de 2.2D. Combina regresiones estructurales con pruebas puras de
/// filtros, ordenación y semántica jugable sin modificar el inventario real.
/// </summary>
public static class BistroBuilderInventoryWarehouse22DSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Run 2.2D Definitive Inventory Warehouse UI Self-Test";

    private sealed class TestResult
    {
        public readonly List<string> Passed = new List<string>();
        public readonly List<string> Failed = new List<string>();

        public void Check(bool condition, string message)
        {
            if (condition)
            {
                Passed.Add(message);
            }
            else
            {
                Failed.Add(message);
            }
        }

        public string BuildReport()
        {
            var builder = new StringBuilder(12288);
            builder.AppendLine("BISTRO BUILDER - AUTOTEST 2.2D");
            builder.AppendLine("Pruebas superadas: " + Passed.Count);
            builder.AppendLine("Pruebas fallidas: " + Failed.Count);
            for (int index = 0; index < Passed.Count; index++)
            {
                builder.AppendLine("- OK: " + Passed[index]);
            }
            for (int index = 0; index < Failed.Count; index++)
            {
                builder.AppendLine("- ERROR: " + Failed[index]);
            }
            return builder.ToString().TrimEnd();
        }
    }

    [MenuItem(MenuPath, false, 392)]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de ejecutar el autotest 2.2D.",
                "Aceptar"
            );
            return;
        }

        var result = new TestResult();
        try
        {
            BistroBuilderInventoryWarehouse22DValidationResult validation =
                BistroBuilderInventoryWarehouse22DValidator.ValidateCurrentProject();
            result.Check(validation.ErrorCount == 0,
                "La instalación 2.2D supera el validador estructural.");
            result.Check(validation.WarningCount == 0,
                "La instalación 2.2D no deja advertencias estructurales.");

            result.Check(
                BistroBuilderInventoryLots22AValidator.ValidateCurrentProject().ErrorCount == 0,
                "Regresión 2.2A permanece válida.");
            result.Check(
                BistroBuilderGoodsReceiving22BValidator.ValidateCurrentProject().ErrorCount == 0,
                "Regresión 2.2B permanece válida.");
            result.Check(
                BistroBuilderInventoryPlanning22CValidator.ValidateCurrentProject().ErrorCount == 0,
                "Regresión 2.2C permanece válida.");

            BistroBuilderInventoryWarehouseIngredientSnapshot normal = Make(
                "ingredient_a", "Aceite", 8000L,
                BistroBuilderInventoryStockLevelState.Normal, 10, 0L
            );
            BistroBuilderInventoryWarehouseIngredientSnapshot low = Make(
                "ingredient_b", "Berenjena", 4000L,
                BistroBuilderInventoryStockLevelState.Low, 8, 0L
            );
            BistroBuilderInventoryWarehouseIngredientSnapshot critical = Make(
                "ingredient_c", "Cebolla", 1000L,
                BistroBuilderInventoryStockLevelState.Critical, 7, 500L
            );
            BistroBuilderInventoryWarehouseIngredientSnapshot empty = Make(
                "ingredient_d", "Dorada", 0L,
                BistroBuilderInventoryStockLevelState.OutOfStock, 0, 0L
            );

            result.Check(
                BistroBuilderInventoryWarehouseQueryUtility.MatchesFilter(
                    normal, BistroBuilderInventoryWarehouseFilter.All),
                "El filtro Todos incluye un ingrediente normal.");
            result.Check(
                BistroBuilderInventoryWarehouseQueryUtility.MatchesFilter(
                    low, BistroBuilderInventoryWarehouseFilter.LowStock),
                "Stock bajo aparece en el filtro de reposición.");
            result.Check(
                BistroBuilderInventoryWarehouseQueryUtility.MatchesFilter(
                    critical, BistroBuilderInventoryWarehouseFilter.LowStock),
                "Stock crítico también aparece en Stock bajo.");
            result.Check(
                !BistroBuilderInventoryWarehouseQueryUtility.MatchesFilter(
                    normal, BistroBuilderInventoryWarehouseFilter.LowStock),
                "Un ingrediente normal no contamina el filtro Stock bajo.");
            result.Check(
                BistroBuilderInventoryWarehouseQueryUtility.MatchesFilter(
                    empty,
                    BistroBuilderInventoryWarehouseFilter.CriticalOrOutOfStock),
                "Agotado aparece en Críticos/agotados.");
            result.Check(
                BistroBuilderInventoryWarehouseQueryUtility.MatchesFilter(
                    critical, BistroBuilderInventoryWarehouseFilter.NearExpiry),
                "Una cantidad próxima a caducar aparece en su filtro.");
            result.Check(
                !BistroBuilderInventoryWarehouseQueryUtility.MatchesFilter(
                    normal, BistroBuilderInventoryWarehouseFilter.NearExpiry),
                "Un ingrediente sin riesgo de caducidad no aparece en ese filtro.");

            var list = new List<BistroBuilderInventoryWarehouseIngredientSnapshot>
            {
                critical, normal, empty, low
            };
            list.Sort((a, b) =>
                BistroBuilderInventoryWarehouseQueryUtility.Compare(
                    a, b, BistroBuilderInventoryWarehouseSort.Name));
            result.Check(list[0].DisplayName == "Aceite" &&
                         list[3].DisplayName == "Dorada",
                "Ordenar por nombre es estable y alfabético.");

            list = new List<BistroBuilderInventoryWarehouseIngredientSnapshot>
            {
                normal, critical, low, empty
            };
            list.Sort((a, b) =>
                BistroBuilderInventoryWarehouseQueryUtility.Compare(
                    a, b, BistroBuilderInventoryWarehouseSort.AvailableStock));
            result.Check(list[0].AvailableCanonicalMilliUnits == 0L &&
                         list[3].AvailableCanonicalMilliUnits == 8000L,
                "Ordenar por stock coloca primero la menor disponibilidad.");

            list.Sort((a, b) =>
                BistroBuilderInventoryWarehouseQueryUtility.Compare(
                    a, b, BistroBuilderInventoryWarehouseSort.Status));
            result.Check(list[0].StockLevelState ==
                         BistroBuilderInventoryStockLevelState.OutOfStock,
                "Ordenar por estado prioriza primero el riesgo más alto.");

            list.Sort((a, b) =>
                BistroBuilderInventoryWarehouseQueryUtility.Compare(
                    a, b, BistroBuilderInventoryWarehouseSort.Expiration));
            result.Check(list[0].NextExpirationDayIndex == 7 &&
                         list[3].NextExpirationDayIndex == 0,
                "Ordenar por caducidad prioriza la fecha relevante más próxima.");

            result.Check(
                !BistroBuilderInventoryWarehouseQueryUtility.IsPlayerFacingMovement(
                    BistroBuilderInventoryTransactionType.Reservation),
                "Las reservas internas se ocultan del historial jugable por defecto.");
            result.Check(
                !BistroBuilderInventoryWarehouseQueryUtility.IsPlayerFacingMovement(
                    BistroBuilderInventoryTransactionType.ReservationRelease),
                "Las liberaciones internas se ocultan del historial jugable por defecto.");
            result.Check(
                BistroBuilderInventoryWarehouseQueryUtility.IsPlayerFacingMovement(
                    BistroBuilderInventoryTransactionType.Purchase),
                "Las recepciones sí son movimientos jugables.");
            result.Check(
                BistroBuilderInventoryWarehouseQueryUtility.IsPlayerFacingMovement(
                    BistroBuilderInventoryTransactionType.Consumption),
                "El consumo de cocina sí es movimiento jugable.");
            result.Check(
                BistroBuilderInventoryWarehouseQueryUtility.IsPlayerFacingMovement(
                    BistroBuilderInventoryTransactionType.Correction),
                "Los ajustes manuales sí son movimientos jugables.");

            result.Check(critical.IsNearExpiry,
                "La lectura agregada conserva la señal NearExpiry de 2.2A/2.2C.");
            result.Check(critical.DaysUntilNextExpiration == 2,
                "Los días hasta caducidad se derivan del DayIndex sin horas.");

            Type serviceType = typeof(BistroBuilderInventoryWarehouseService);
            result.Check(serviceType.GetMethod("TryAdjustStock") != null,
                "Application expone ajuste manual controlado.");
            result.Check(serviceType.GetMethod("TrySetMinimumStock") != null,
                "Application expone modificación del mínimo 2.2C.");
            result.Check(serviceType.GetMethod("CopyMovementsTo") != null,
                "Application expone historial jugable de movimientos.");
            result.Check(serviceType.GetMethod("CopyReceiptsTo") != null,
                "Application expone recepciones agrupadas de 2.2B.");
            result.Check(serviceType.GetMethod("CopyIngredientsTo") != null,
                "Application expone consulta filtrada/ordenada de ingredientes.");
            result.Check(serviceType.GetEvent("DataChanged") != null,
                "La fachada publica cambios por evento.");

            Type viewType = typeof(BistroBuilderInventoryWarehouseRuntimeView);
            result.Check(viewType.GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly) == null,
                "La UI 2.2D no refresca mediante Update por frame.");
            result.Check(viewType.GetMethod("TryValidateVisibleContent") != null,
                "La UI expone validación funcional de contenido visible.");
            result.Check(viewType.GetMethod("TrySetFilterForTest") != null &&
                         viewType.GetMethod("TrySetSortForTest") != null &&
                         viewType.GetMethod("TrySetSectionForTest") != null,
                "Filtros, ordenación y secciones pueden verificarse en Play Mode.");

            result.Check(
                Enum.GetValues(typeof(BistroBuilderInventoryManualAdjustmentReason)).Length == 4,
                "Los motivos de ajuste se mantienen simples y trazables.");
            result.Check(
                Enum.GetValues(typeof(BistroBuilderInventoryWarehouseSection)).Length == 4,
                "La navegación jugable mantiene cuatro secciones claras.");
            result.Check(BistroBuilderInventoryRuntimeSnapshot.CurrentSchemaVersion == 2,
                "2.2D no modifica inventory.canonical v2.");
            result.Check(BistroBuilderInventoryPolicySaveData.CurrentSchemaVersion == 1,
                "2.2D no modifica inventory.policy v1.");

            string report = result.BuildReport();
            if (result.Failed.Count > 0)
            {
                Debug.LogError(report);
            }
            else
            {
                Debug.Log(report);
            }
            EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "El autotest 2.2D lanzó una excepción:\n\n" + exception.Message,
                "Aceptar"
            );
        }
    }

    private static BistroBuilderInventoryWarehouseIngredientSnapshot Make(
        string id,
        string name,
        long available,
        BistroBuilderInventoryStockLevelState state,
        int expirationDay,
        long nearExpiry
    )
    {
        return new BistroBuilderInventoryWarehouseIngredientSnapshot(
            id,
            name,
            BistroBuilderMeasurementUnit.Gram,
            available,
            0L,
            available,
            5000L,
            state,
            nearExpiry > 0L
                ? BistroBuilderInventoryFreshnessState.NearExpiry
                : BistroBuilderInventoryFreshnessState.Good,
            5,
            expirationDay,
            nearExpiry,
            BistroBuilderInventoryForecastState.Available,
            4,
            1000d,
            available / 1000d,
            0L,
            0L,
            0L,
            string.Empty,
            1L,
            1L
        );
    }
}
