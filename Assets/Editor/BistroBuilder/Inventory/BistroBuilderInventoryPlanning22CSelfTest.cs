using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Autotest aislado de 2.2C. Verifica matemática, persistencia de política,
/// transiciones de alerta y evaluación previa a apertura sin modificar el
/// inventario real de la escena.
/// </summary>
public static class BistroBuilderInventoryPlanning22CSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Run 2.2C Minimum Stock, Alerts and Basic Forecast Self-Test";

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
            var builder = new StringBuilder(8192);
            builder.AppendLine("BISTRO BUILDER - AUTOTEST 2.2C");
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

    [MenuItem(MenuPath, false, 382)]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de ejecutar el autotest 2.2C.",
                "Aceptar"
            );
            return;
        }

        var result = new TestResult();
        GameObject root = null;

        try
        {
            BistroBuilderInventoryPlanning22CValidationResult validation =
                BistroBuilderInventoryPlanning22CValidator
                    .ValidateCurrentProject();
            result.Check(
                validation.ErrorCount == 0,
                "La instalación 2.2C supera el validador estructural."
            );
            result.Check(
                validation.WarningCount == 0,
                "La instalación 2.2C no deja advertencias estructurales."
            );

            result.Check(
                BistroBuilderInventoryPlanningMath.EvaluateStockLevel(
                    1000L,
                    500L,
                    0.5d
                ) == BistroBuilderInventoryStockLevelState.Normal,
                "Stock por encima del mínimo se clasifica como Normal."
            );
            result.Check(
                BistroBuilderInventoryPlanningMath.EvaluateStockLevel(
                    750L,
                    1000L,
                    0.5d
                ) == BistroBuilderInventoryStockLevelState.Low,
                "Stock por debajo del mínimo se clasifica como Bajo."
            );
            result.Check(
                BistroBuilderInventoryPlanningMath.EvaluateStockLevel(
                    400L,
                    1000L,
                    0.5d
                ) == BistroBuilderInventoryStockLevelState.Critical,
                "Stock por debajo del umbral crítico se clasifica como Crítico."
            );
            result.Check(
                BistroBuilderInventoryPlanningMath.EvaluateStockLevel(
                    0L,
                    1000L,
                    0.5d
                ) == BistroBuilderInventoryStockLevelState.OutOfStock,
                "Disponible cero se clasifica como Sin stock."
            );

            BistroBuilderInventoryForecastState insufficient =
                BistroBuilderInventoryPlanningMath.CalculateForecast(
                    5000L,
                    1000L,
                    1,
                    2,
                    out int insufficientDays,
                    out double insufficientAverage,
                    out double insufficientCoverage
                );
            result.Check(
                insufficient ==
                    BistroBuilderInventoryForecastState.InsufficientHistory &&
                insufficientDays == 1 && insufficientAverage == 0d &&
                insufficientCoverage < 0d,
                "La previsión no inventa cifras sin historial suficiente."
            );

            BistroBuilderInventoryForecastState noConsumption =
                BistroBuilderInventoryPlanningMath.CalculateForecast(
                    5000L,
                    0L,
                    4,
                    2,
                    out int noConsumptionDays,
                    out double noConsumptionAverage,
                    out double noConsumptionCoverage
                );
            result.Check(
                noConsumption ==
                    BistroBuilderInventoryForecastState.NoConsumption &&
                noConsumptionDays == 4 && noConsumptionAverage == 0d &&
                noConsumptionCoverage < 0d,
                "Historial sin consumo se distingue de historial insuficiente."
            );

            BistroBuilderInventoryForecastState availableForecast =
                BistroBuilderInventoryPlanningMath.CalculateForecast(
                    6000L,
                    4000L,
                    4,
                    2,
                    out int historyDays,
                    out double averageDaily,
                    out double coverageDays
                );
            result.Check(
                availableForecast == BistroBuilderInventoryForecastState.Available &&
                historyDays == 4 && Math.Abs(averageDaily - 1000d) < 0.001d &&
                Math.Abs(coverageDays - 6d) < 0.001d,
                "La cobertura usa consumo medio real por días de partida."
            );

            string alertKeyA = BistroBuilderInventoryPlanningMath.BuildAlertKey(
                "ingredient_test",
                BistroBuilderInventoryAlertKind.LowStock
            );
            string alertKeyB = BistroBuilderInventoryPlanningMath.BuildAlertKey(
                "ingredient_test",
                BistroBuilderInventoryAlertKind.LowStock
            );
            result.Check(
                alertKeyA == alertKeyB &&
                BistroBuilderMenuIdUtility.IsValidStableId(alertKeyA),
                "Las alertas usan claves estables y deterministas."
            );

            var validPolicy = new BistroBuilderInventoryPolicySaveData();
            validPolicy.minimumStocks.Add(
                new BistroBuilderInventoryMinimumStockSaveRecord
                {
                    ingredientId = "ingredient_test",
                    minimumCanonicalMilliUnits = 1000L
                }
            );
            result.Check(
                validPolicy.TryValidateBasic(out string policyError),
                "inventory.policy valida mínimos no negativos con IDs estables."
            );

            var duplicatePolicy = new BistroBuilderInventoryPolicySaveData();
            duplicatePolicy.minimumStocks.Add(
                new BistroBuilderInventoryMinimumStockSaveRecord
                {
                    ingredientId = "ingredient_test",
                    minimumCanonicalMilliUnits = 100L
                }
            );
            duplicatePolicy.minimumStocks.Add(
                new BistroBuilderInventoryMinimumStockSaveRecord
                {
                    ingredientId = "ingredient_test",
                    minimumCanonicalMilliUnits = 200L
                }
            );
            result.Check(
                !duplicatePolicy.TryValidateBasic(out policyError),
                "inventory.policy rechaza ingredientes duplicados."
            );

            Scene scene = SceneManager.GetActiveScene();
            GameObject gameSystems =
                BistroBuilderIngredientsRecipesEditorUtility.FindGameSystems(
                    scene
                );
            BistroBuilderInventoryService installedInventory = gameSystems != null
                ? gameSystems.GetComponent<BistroBuilderInventoryService>()
                : null;
            BistroBuilderRecipeCatalogService recipes = gameSystems != null
                ? gameSystems.GetComponent<BistroBuilderRecipeCatalogService>()
                : null;
            BistroBuilderGeneralGameStateService general = gameSystems != null
                ? gameSystems.GetComponent<BistroBuilderGeneralGameStateService>()
                : null;

            root = new GameObject("BB_22C_SelfTest");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);

            BistroBuilderInventoryService inventory =
                root.AddComponent<BistroBuilderInventoryService>();
            SetReference(inventory, "recipeCatalogService", recipes);
            SetReference(
                inventory,
                "openingStockProfile",
                installedInventory != null
                    ? installedInventory.OpeningStockProfile
                    : null
            );
            SetReference(inventory, "generalGameStateService", general);
            SetBoolean(inventory, "logInitialization", false);

            RestaurantServiceStateService serviceState =
                root.AddComponent<RestaurantServiceStateService>();

            string error = string.Empty;
            bool inventoryReady = inventory.TryInitialize(out error);
            result.Check(
                inventoryReady && inventory.ValidateRuntimeState(out error),
                "Un inventario aislado arranca coherente sobre 2.2A/2.2B."
            );

            BistroBuilderInventoryPlanningService planning =
                root.AddComponent<BistroBuilderInventoryPlanningService>();
            SetReference(planning, "inventoryService", inventory);
            SetReference(planning, "recipeCatalogService", recipes);
            SetReference(planning, "generalGameStateService", general);
            SetReference(planning, "serviceStateService", serviceState);
            SetBoolean(planning, "logOpeningWarnings", false);

            bool planningReady = planning.TryInitialize(out error);
            result.Check(
                planningReady && planning.IngredientCount == inventory.StockEntryCount,
                "Planificación cubre exactamente todos los ingredientes canónicos."
            );

            List<BistroBuilderInventoryPlanningSnapshot> snapshots =
                new List<BistroBuilderInventoryPlanningSnapshot>();
            planning.CopyPlanningSnapshotsTo(snapshots);
            BistroBuilderInventoryPlanningSnapshot selected = default;
            bool foundPositive = false;
            for (int index = 0; index < snapshots.Count; index++)
            {
                if (snapshots[index].AvailableCanonicalMilliUnits > 10L)
                {
                    selected = snapshots[index];
                    foundPositive = true;
                    break;
                }
            }
            result.Check(
                foundPositive && selected.MinimumStockCanonicalMilliUnits == 0L,
                "Los mínimos arrancan desactivados y no inventan política del jugador."
            );

            long lowMinimum = foundPositive
                ? selected.AvailableCanonicalMilliUnits + 1L
                : 1L;
            BistroBuilderInventoryPlanningSnapshot lowSnapshot = default;
            bool lowConfigured = foundPositive;
            if (lowConfigured)
            {
                lowConfigured = planning.TrySetMinimumStock(
                    selected.IngredientId,
                    lowMinimum,
                    out error
                );
            }
            if (lowConfigured)
            {
                lowConfigured = planning.TryGetPlanningSnapshot(
                    selected.IngredientId,
                    out lowSnapshot
                );
            }
            result.Check(
                lowConfigured &&
                lowSnapshot.StockLevelState ==
                    BistroBuilderInventoryStockLevelState.Low,
                "Un mínimo configurable activa el estado Bajo sin modificar stock físico."
            );

            List<BistroBuilderInventoryAlertSnapshot> activeAlerts =
                new List<BistroBuilderInventoryAlertSnapshot>();
            planning.CopyActiveAlertsTo(activeAlerts);
            int lowAlertCount = CountAlert(
                activeAlerts,
                selected.IngredientId,
                BistroBuilderInventoryAlertKind.LowStock
            );
            long policyRevisionBeforeRecalculation = planning.PolicyRevision;
            planning.TryRecalculateAll(out error);
            planning.CopyActiveAlertsTo(activeAlerts);
            result.Check(
                lowAlertCount == 1 &&
                CountAlert(
                    activeAlerts,
                    selected.IngredientId,
                    BistroBuilderInventoryAlertKind.LowStock
                ) == 1 &&
                planning.PolicyRevision == policyRevisionBeforeRecalculation,
                "Recalcular deduplica alertas sin alterar la revisión de política."
            );

            bool cleared = planning.TrySetMinimumStock(
                selected.IngredientId,
                0L,
                out error
            );
            planning.CopyActiveAlertsTo(activeAlerts);
            result.Check(
                cleared &&
                CountAnyStockAlert(activeAlerts, selected.IngredientId) == 0,
                "Recuperar el nivel normal elimina la alerta de stock."
            );

            long persistedMinimum = foundPositive
                ? Math.Max(1L, selected.AvailableCanonicalMilliUnits / 2L)
                : 1L;
            planning.TrySetMinimumStock(
                selected.IngredientId,
                persistedMinimum,
                out error
            );
            bool captured = planning.TryCapturePolicySnapshot(
                out BistroBuilderInventoryPolicySaveData capturedPolicy,
                out error
            );
            result.Check(
                captured && capturedPolicy != null &&
                capturedPolicy.policyRevision == planning.PolicyRevision &&
                capturedPolicy.minimumStocks.Count == 1 &&
                capturedPolicy.minimumStocks[0].ingredientId ==
                    selected.IngredientId &&
                capturedPolicy.minimumStocks[0].minimumCanonicalMilliUnits ==
                    persistedMinimum,
                "La persistencia guarda solo mínimos explícitamente configurados."
            );

            planning.TrySetMinimumStock(
                selected.IngredientId,
                lowMinimum,
                out error
            );
            long restoredMinimum = 0L;
            bool replaced = planning.TryReplacePolicySnapshot(
                capturedPolicy,
                true,
                out error
            );
            if (replaced)
            {
                replaced = planning.TryGetMinimumStock(
                    selected.IngredientId,
                    out restoredMinimum
                );
            }
            result.Check(
                replaced && restoredMinimum == persistedMinimum,
                "El round-trip de inventory.policy restaura exactamente el mínimo."
            );

            planning.TrySetMinimumStock(
                selected.IngredientId,
                lowMinimum,
                out error
            );
            bool readinessOk = planning.TryEvaluateOpeningReadiness(
                out BistroBuilderInventoryOpeningReadinessSnapshot readiness,
                out error
            );
            result.Check(
                readinessOk && readiness != null && readiness.HasWarnings &&
                readiness.LowStockCount + readiness.CriticalStockCount +
                    readiness.OutOfStockCount > 0,
                "La comprobación previa a apertura detecta stock insuficiente como aviso."
            );

            result.Check(
                serviceState.CurrentState == RestaurantServiceState.Closed,
                "La evaluación consultiva no abre ni bloquea por sí sola el servicio."
            );
        }
        catch (Exception exception)
        {
            result.Failed.Add(
                "Excepción inesperada: " + exception.GetType().Name +
                " - " + exception.Message
            );
            Debug.LogException(exception);
        }
        finally
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

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

    private static int CountAlert(
        List<BistroBuilderInventoryAlertSnapshot> alerts,
        string ingredientId,
        BistroBuilderInventoryAlertKind kind
    )
    {
        int count = 0;
        for (int index = 0; index < alerts.Count; index++)
        {
            if (alerts[index].IngredientId == ingredientId &&
                alerts[index].Kind == kind)
            {
                count++;
            }
        }
        return count;
    }

    private static int CountAnyStockAlert(
        List<BistroBuilderInventoryAlertSnapshot> alerts,
        string ingredientId
    )
    {
        int count = 0;
        for (int index = 0; index < alerts.Count; index++)
        {
            if (alerts[index].IngredientId != ingredientId)
            {
                continue;
            }

            if (alerts[index].Kind == BistroBuilderInventoryAlertKind.LowStock ||
                alerts[index].Kind ==
                    BistroBuilderInventoryAlertKind.CriticalStock ||
                alerts[index].Kind ==
                    BistroBuilderInventoryAlertKind.OutOfStock)
            {
                count++;
            }
        }
        return count;
    }

    private static void SetReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene " + propertyName + "."
            );
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBoolean(
        UnityEngine.Object target,
        string propertyName,
        bool value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene " + propertyName + "."
            );
        }
        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
