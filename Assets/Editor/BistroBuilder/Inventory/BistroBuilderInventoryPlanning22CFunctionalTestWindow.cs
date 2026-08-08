using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prueba funcional real de 2.2C sobre la escena jugable.
/// Modifica únicamente la política de stock mínimo y restaura el estado
/// original al terminar. No consume, compra ni corrige existencias reales.
/// </summary>
public sealed class BistroBuilderInventoryPlanning22CFunctionalTestWindow :
    EditorWindow
{
    private readonly List<string> passed = new List<string>();
    private readonly List<string> failed = new List<string>();

    private BistroBuilderInventoryPlanningService planning;
    private BistroBuilderInventoryService inventory;
    private BistroBuilderInventoryPolicySaveSectionProvider provider;
    private BistroBuilderInventoryPlanningRuntimeView runtimeView;
    private RestaurantServiceStateService serviceState;

    private string status = "Entra en Play Mode para ejecutar la prueba.";
    private bool running;

    [MenuItem(
        "Tools/Bistro Builder/Inventory/2.2C Minimum Stock, Alerts and Basic Forecast Functional Test",
        false,
        383
    )]
    private static void OpenWindow()
    {
        BistroBuilderInventoryPlanning22CFunctionalTestWindow window =
            GetWindow<BistroBuilderInventoryPlanning22CFunctionalTestWindow>();
        window.titleContent = new GUIContent("BB 2.2C Test");
        window.minSize = new Vector2(720f, 470f);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshReferences();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Bistro Builder 2.2C — Stock mínimo, alertas y previsión básica",
            EditorStyles.boldLabel
        );
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "La prueba configura temporalmente un mínimo sobre un ingrediente " +
            "real, comprueba alertas deduplicadas, validación previa a apertura, " +
            "previsión y persistencia. No modifica las existencias y restaura " +
            "la política original al finalizar.",
            MessageType.Info
        );

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Entra en Play Mode y vuelve a esta ventana.",
                MessageType.Warning
            );
            if (GUILayout.Button("Actualizar referencias", GUILayout.Height(28f)))
            {
                RefreshReferences();
            }
            return;
        }

        if (planning == null || inventory == null || provider == null ||
            runtimeView == null || serviceState == null)
        {
            RefreshReferences();
        }

        bool ready = planning != null && inventory != null && provider != null &&
                     runtimeView != null && serviceState != null;
        if (!ready)
        {
            EditorGUILayout.HelpBox(
                "Faltan componentes 2.2C. Ejecuta primero el instalador.",
                MessageType.Error
            );
            return;
        }

        EditorGUI.BeginDisabledGroup(running);
        if (GUILayout.Button(
                "Ejecutar prueba funcional 2.2C",
                GUILayout.Height(38f)
            ))
        {
            RunFunctionalTest();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Seguimiento", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(status, MessageType.None);

        if (passed.Count > 0 || failed.Count > 0)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Correctos: " + passed.Count + "    Fallos: " + failed.Count,
                EditorStyles.boldLabel
            );
        }
    }

    private void RunFunctionalTest()
    {
        passed.Clear();
        failed.Clear();
        running = true;
        status = "Ejecutando 2.2C...";

        BistroBuilderInventoryPolicySaveData originalPolicy = null;
        RestaurantServiceState originalServiceState =
            RestaurantServiceState.Closed;
        bool serviceStateCaptured = false;
        string selectedIngredientId = string.Empty;
        int activatedForSelected = 0;
        int clearedForSelected = 0;
        int openingEvaluations = 0;
        BistroBuilderInventoryOpeningReadinessSnapshot openingSnapshot = null;

        Action<BistroBuilderInventoryAlertSnapshot> activatedHandler = null;
        Action<BistroBuilderInventoryAlertSnapshot> clearedHandler = null;
        Action<BistroBuilderInventoryOpeningReadinessSnapshot> openingHandler = null;

        try
        {
            RefreshReferences();
            string error = string.Empty;

            Check(
                planning != null && planning.EnsureInitialized(out error) &&
                planning.ValidateConfiguration(out error),
                "La planificación 2.2C está inicializada y configurada."
            );
            Check(
                inventory != null && inventory.ValidateRuntimeState(out error),
                "El inventario canónico permanece coherente antes de la prueba."
            );
            Check(
                provider != null && provider.ValidateConfiguration(out error),
                "inventory.policy está registrado y configurado."
            );

            if (!planning.TryCapturePolicySnapshot(
                    out originalPolicy,
                    out error
                ) || originalPolicy == null)
            {
                throw new InvalidOperationException(
                    "No pudo capturarse la política original. " + error
                );
            }
            Check(true, "Se captura la política original para restaurarla al final.");

            originalServiceState = serviceState.CurrentState;
            serviceStateCaptured = true;

            List<BistroBuilderInventoryPlanningSnapshot> snapshots =
                new List<BistroBuilderInventoryPlanningSnapshot>();
            planning.CopyPlanningSnapshotsTo(snapshots);
            BistroBuilderInventoryPlanningSnapshot selected = default;
            bool found = false;
            for (int index = 0; index < snapshots.Count; index++)
            {
                if (snapshots[index].AvailableCanonicalMilliUnits > 10L)
                {
                    selected = snapshots[index];
                    found = true;
                    break;
                }
            }
            Check(
                found && snapshots.Count == inventory.StockEntryCount,
                "La planificación representa todo el inventario y localiza stock utilizable."
            );
            if (!found)
            {
                throw new InvalidOperationException(
                    "No existe ningún ingrediente con stock disponible para probar 2.2C."
                );
            }
            selectedIngredientId = selected.IngredientId;

            inventory.TryGetStockSnapshot(
                selectedIngredientId,
                out BistroBuilderInventoryStockSnapshot stockBefore
            );
            long inventoryRevisionBefore = inventory.RuntimeRevision;

            planning.TrySetMinimumStock(selectedIngredientId, 0L, out error);
            planning.TryRecalculateAll(out error);

            activatedHandler = alert =>
            {
                if (alert.IngredientId == selectedIngredientId &&
                    (alert.Kind == BistroBuilderInventoryAlertKind.LowStock ||
                     alert.Kind == BistroBuilderInventoryAlertKind.CriticalStock ||
                     alert.Kind == BistroBuilderInventoryAlertKind.OutOfStock))
                {
                    activatedForSelected++;
                }
            };
            clearedHandler = alert =>
            {
                if (alert.IngredientId == selectedIngredientId &&
                    (alert.Kind == BistroBuilderInventoryAlertKind.LowStock ||
                     alert.Kind == BistroBuilderInventoryAlertKind.CriticalStock ||
                     alert.Kind == BistroBuilderInventoryAlertKind.OutOfStock))
                {
                    clearedForSelected++;
                }
            };
            openingHandler = snapshot =>
            {
                openingEvaluations++;
                openingSnapshot = snapshot;
            };
            planning.AlertActivated += activatedHandler;
            planning.AlertCleared += clearedHandler;
            planning.OpeningReadinessEvaluated += openingHandler;

            long lowMinimum = selected.AvailableCanonicalMilliUnits + 1L;
            bool lowSet = planning.TrySetMinimumStock(
                selectedIngredientId,
                lowMinimum,
                out error
            );
            bool lowRead = planning.TryGetPlanningSnapshot(
                selectedIngredientId,
                out BistroBuilderInventoryPlanningSnapshot lowSnapshot
            );
            Check(
                lowSet && lowRead &&
                lowSnapshot.StockLevelState ==
                    BistroBuilderInventoryStockLevelState.Low,
                "Un mínimo superior al disponible activa el estado Bajo."
            );

            Check(
                activatedForSelected == 1,
                "La entrada en stock bajo activa una única alerta."
            );

            planning.TryRecalculateAll(out error);
            planning.TryRecalculateAll(out error);
            Check(
                activatedForSelected == 1,
                "Recalcular sin cambios no duplica la alerta activa."
            );

            List<BistroBuilderInventoryAlertSnapshot> activeAlerts =
                new List<BistroBuilderInventoryAlertSnapshot>();
            planning.CopyActiveAlertsTo(activeAlerts);
            Check(
                CountStockAlerts(activeAlerts, selectedIngredientId) == 1,
                "Solo existe un estado de alerta de stock por ingrediente."
            );

            bool resetMinimum = planning.TrySetMinimumStock(
                selectedIngredientId,
                0L,
                out error
            );
            planning.CopyActiveAlertsTo(activeAlerts);
            Check(
                resetMinimum && clearedForSelected == 1 &&
                CountStockAlerts(activeAlerts, selectedIngredientId) == 0,
                "Recuperar el nivel normal publica recuperación y elimina la alerta."
            );

            long criticalMinimum;
            try
            {
                criticalMinimum = checked(
                    selected.AvailableCanonicalMilliUnits * 3L
                );
            }
            catch (OverflowException)
            {
                criticalMinimum =
                    BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits;
            }
            criticalMinimum = Math.Min(
                criticalMinimum,
                BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits
            );
            if (criticalMinimum <= selected.AvailableCanonicalMilliUnits)
            {
                criticalMinimum = Math.Min(
                    BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits,
                    selected.AvailableCanonicalMilliUnits + 1L
                );
            }

            bool criticalSet = planning.TrySetMinimumStock(
                selectedIngredientId,
                criticalMinimum,
                out error
            );
            bool criticalRead = planning.TryGetPlanningSnapshot(
                selectedIngredientId,
                out BistroBuilderInventoryPlanningSnapshot criticalSnapshot
            );
            Check(
                criticalSet && criticalRead &&
                (criticalSnapshot.StockLevelState ==
                     BistroBuilderInventoryStockLevelState.Critical ||
                 criticalSnapshot.StockLevelState ==
                     BistroBuilderInventoryStockLevelState.Low),
                "La política puede elevar el riesgo sin tocar las existencias."
            );

            inventory.TryGetStockSnapshot(
                selectedIngredientId,
                out BistroBuilderInventoryStockSnapshot stockAfterPolicy
            );
            Check(
                inventory.RuntimeRevision == inventoryRevisionBefore &&
                stockAfterPolicy.OnHandCanonicalMilliUnits ==
                    stockBefore.OnHandCanonicalMilliUnits &&
                stockAfterPolicy.ReservedCanonicalMilliUnits ==
                    stockBefore.ReservedCanonicalMilliUnits &&
                stockAfterPolicy.AvailableCanonicalMilliUnits ==
                    stockBefore.AvailableCanonicalMilliUnits,
                "Configurar mínimos no modifica stock, reservas, lotes ni revisión canónica."
            );

            bool readinessOk = planning.TryEvaluateOpeningReadiness(
                out BistroBuilderInventoryOpeningReadinessSnapshot readiness,
                out error
            );
            Check(
                readinessOk && readiness != null && readiness.HasWarnings,
                "La comprobación previa a apertura devuelve avisos informativos."
            );
            Check(
                serviceState.CurrentState == originalServiceState,
                "Comprobar apertura no cambia por sí solo el estado del restaurante."
            );

            if (originalServiceState == RestaurantServiceState.Closed ||
                originalServiceState == RestaurantServiceState.Preparing)
            {
                bool opened = serviceState.TryOpenService();
                Check(
                    opened && openingEvaluations == 1 &&
                    openingSnapshot != null && openingSnapshot.HasWarnings,
                    "Abrir el servicio dispara exactamente una evaluación previa de inventario."
                );
                serviceState.TryRestoreState(originalServiceState, true);
            }
            else
            {
                Check(
                    true,
                    "El servicio ya estaba activo; la integración previa se conserva sin forzar una transición de prueba."
                );
            }

            Check(
                runtimeView.TryValidateVisibleContent(out error),
                "La UI jugable muestra filas, mínimos, alertas y previsión con RectMask2D."
            );

            bool captured = planning.TryCapturePolicySnapshot(
                out BistroBuilderInventoryPolicySaveData policySnapshot,
                out error
            );
            Check(
                captured && policySnapshot != null &&
                policySnapshot.schemaVersion == 1 &&
                policySnapshot.minimumStocks.Count >= 1,
                "La política configurada se captura en inventory.policy v1."
            );

            string json = captured
                ? JsonUtility.ToJson(policySnapshot, false)
                : string.Empty;
            BistroBuilderInventoryPolicySaveData roundTrip =
                !string.IsNullOrWhiteSpace(json)
                    ? JsonUtility.FromJson<BistroBuilderInventoryPolicySaveData>(json)
                    : null;
            Check(
                roundTrip != null && roundTrip.TryValidateBasic(out error),
                "inventory.policy realiza round-trip JSON válido."
            );

            Check(
                provider.ValidateState(roundTrip, out error),
                "El proveedor universal acepta el payload round-trip de la política."
            );

            bool foundRoundTripMinimum = false;
            if (roundTrip != null && roundTrip.minimumStocks != null)
            {
                for (int index = 0; index < roundTrip.minimumStocks.Count; index++)
                {
                    BistroBuilderInventoryMinimumStockSaveRecord record =
                        roundTrip.minimumStocks[index];
                    if (record != null &&
                        record.ingredientId == selectedIngredientId &&
                        record.minimumCanonicalMilliUnits == criticalMinimum)
                    {
                        foundRoundTripMinimum = true;
                        break;
                    }
                }
            }
            Check(
                foundRoundTripMinimum,
                "El round-trip conserva exactamente el mínimo del ingrediente probado."
            );

            List<BistroBuilderInventoryPlanningSnapshot> finalSnapshots =
                new List<BistroBuilderInventoryPlanningSnapshot>();
            planning.CopyPlanningSnapshotsTo(finalSnapshots);
            bool lotAggregationValid = true;
            for (int index = 0; index < finalSnapshots.Count; index++)
            {
                BistroBuilderInventoryPlanningSnapshot item = finalSnapshots[index];
                if (item.NearExpiryAvailableCanonicalMilliUnits < 0L ||
                    item.NearExpiryAvailableCanonicalMilliUnits >
                        item.AvailableCanonicalMilliUnits)
                {
                    lotAggregationValid = false;
                    break;
                }
            }
            Check(
                lotAggregationValid,
                "La alerta de caducidad agrega lotes sin exponer ni exceder el stock utilizable."
            );

            bool forecastSemanticsValid = true;
            for (int index = 0; index < finalSnapshots.Count; index++)
            {
                BistroBuilderInventoryPlanningSnapshot item = finalSnapshots[index];
                if (item.ForecastState ==
                        BistroBuilderInventoryForecastState.Available &&
                    (item.AverageDailyConsumptionCanonicalMilliUnits <= 0d ||
                     item.CoverageDays < 0d))
                {
                    forecastSemanticsValid = false;
                    break;
                }
            }
            Check(
                forecastSemanticsValid,
                "La previsión solo publica cobertura cuando existe consumo medio válido."
            );

            status = failed.Count == 0
                ? "PRUEBA FUNCIONAL 2.2C SUPERADA"
                : "PRUEBA FUNCIONAL 2.2C CON FALLOS";
        }
        catch (Exception exception)
        {
            failed.Add(
                "Excepción inesperada: " + exception.GetType().Name +
                " - " + exception.Message
            );
            status = "La prueba funcional lanzó una excepción.";
            Debug.LogException(exception);
        }
        finally
        {
            if (planning != null)
            {
                if (activatedHandler != null)
                {
                    planning.AlertActivated -= activatedHandler;
                }
                if (clearedHandler != null)
                {
                    planning.AlertCleared -= clearedHandler;
                }
                if (openingHandler != null)
                {
                    planning.OpeningReadinessEvaluated -= openingHandler;
                }

                if (originalPolicy != null)
                {
                    if (!planning.TryReplacePolicySnapshot(
                            originalPolicy,
                            true,
                            out string restorePolicyError
                        ))
                    {
                        failed.Add(
                            "No se pudo restaurar la política original: " +
                            restorePolicyError
                        );
                    }
                }
            }

            if (serviceState != null && serviceStateCaptured &&
                serviceState.CurrentState != originalServiceState)
            {
                if (!serviceState.TryRestoreState(originalServiceState, true))
                {
                    failed.Add(
                        "No se pudo restaurar el estado original del servicio."
                    );
                }
            }

            running = false;
            LogResult();
        }
    }

    private void Check(bool condition, string message)
    {
        if (condition)
        {
            passed.Add(message);
        }
        else
        {
            failed.Add(message);
        }
    }

    private static int CountStockAlerts(
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

    private void RefreshReferences()
    {
        planning = FindFirstObjectByType<BistroBuilderInventoryPlanningService>();
        inventory = FindFirstObjectByType<BistroBuilderInventoryService>();
        provider =
            FindFirstObjectByType<BistroBuilderInventoryPolicySaveSectionProvider>();
        runtimeView =
            FindFirstObjectByType<BistroBuilderInventoryPlanningRuntimeView>();
        serviceState = FindFirstObjectByType<RestaurantServiceStateService>();
    }

    private void LogResult()
    {
        var builder = new StringBuilder(8192);
        builder.AppendLine(status);
        builder.AppendLine("Correctos: " + passed.Count);
        builder.AppendLine("Fallos: " + failed.Count);
        for (int index = 0; index < passed.Count; index++)
        {
            builder.AppendLine("- OK: " + passed[index]);
        }
        for (int index = 0; index < failed.Count; index++)
        {
            builder.AppendLine("- ERROR: " + failed[index]);
        }

        string report = builder.ToString().TrimEnd();
        if (failed.Count > 0)
        {
            Debug.LogError(report);
        }
        else
        {
            Debug.Log(report);
        }
    }
}
