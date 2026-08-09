using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Prueba funcional de 2.2D en Play Mode.
/// Incluye una prueba automática restaurable y un escenario guiado de carga
/// real con seis grupos / diez clientes para observar reservas y consumo.
/// </summary>
public sealed class BistroBuilderInventoryWarehouse22DFunctionalTestWindow :
    EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/2.2D Definitive Inventory Warehouse UI Functional Test";

    // Contrato público efectivo del inventario v2. Se replica aquí solo en la
    // herramienta Editor porque BistroBuilderInventoryRuntimeIdUtility es
    // internal del assembly runtime y no puede referenciarse desde Editor.
    private const int InventoryMaximumRuntimeIdLength = 160;

    private readonly List<string> passed = new List<string>();
    private readonly List<string> failed = new List<string>();
    private Vector2 scroll;
    private Vector2 liveOrdersScroll;
    private string status =
        "Entra en Play Mode y ejecuta primero la prueba automática.";
    private bool scenarioActive;

    // Estado de la recepción manual controlada 2.2B → 2.2D.
    // Se captura un snapshot completo antes de la recepción y solo se permite
    // ejecutarla con el servicio cerrado. De este modo el rollback no puede
    // rebobinar consumo/reservas de un servicio activo.
    private BistroBuilderInventoryRuntimeSnapshot manualReceiptOriginalInventory;
    private string manualReceiptId = string.Empty;
    private string manualReceiptStatus =
        "Sin recepción manual activa.";
    private long manualReceiptBeforeOnHand;
    private long manualReceiptExpectedAdded;
    private const string ManualReceiptIngredientId = "ingredient_merluza";

    // Buffers reutilizados por el monitor de servicio realista. Evitan que la
    // herramienta de diagnóstico tenga que crear colecciones nuevas en cada
    // repintado del EditorWindow.
    private readonly List<BistroBuilderCanonicalOrder> liveOrderBuffer =
        new List<BistroBuilderCanonicalOrder>(32);

    [MenuItem(MenuPath, false, 393)]
    private static void Open()
    {
        GetWindow<BistroBuilderInventoryWarehouse22DFunctionalTestWindow>(
            "Prueba 2.2D"
        );
    }

    private void OnEnable()
    {
        EditorApplication.update += HandleEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= HandleEditorUpdate;
    }

    private void HandleEditorUpdate()
    {
        if ((scenarioActive || manualReceiptOriginalInventory != null) &&
            EditorApplication.isPlaying)
        {
            Repaint();
        }

        if (!EditorApplication.isPlaying &&
            manualReceiptOriginalInventory != null)
        {
            ClearManualReceiptState();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2.2D — UI definitiva de Inventario / Almacén",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "La prueba automática modifica temporalmente inventario y mínimos, " +
            "pero restaura sus snapshots originales al terminar. Después puedes " +
            "preparar un servicio realista con 6 grupos / 10 clientes.",
            MessageType.Info
        );

        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
        if (GUILayout.Button("Ejecutar prueba funcional automática 2.2D", GUILayout.Height(34f)))
        {
            RunAutomaticTest();
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Preparar servicio realista — 6 grupos / 10 clientes", GUILayout.Height(34f)))
        {
            PrepareRealisticServiceScenario();
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(12f);
        EditorGUILayout.LabelField(
            "Recepción manual controlada 2.2B → 2.2D",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "Ejecuta esta parte en un Play Mode nuevo y con el servicio CERRADO. " +
            "Genera una recepción real de +1 kg de Merluza mediante 2.2B, permite " +
            "observar el repartidor y comprobar Stock / Movimientos / Recepciones, " +
            "y después restaura exactamente el snapshot previo.",
            MessageType.Info
        );

        EditorGUI.BeginDisabledGroup(
            !EditorApplication.isPlaying ||
            manualReceiptOriginalInventory != null
        );
        if (GUILayout.Button(
                "Generar recepción manual — +1 kg Merluza",
                GUILayout.Height(32f)
            ))
        {
            GenerateManualReceipt();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(
            !EditorApplication.isPlaying ||
            manualReceiptOriginalInventory == null
        );
        if (GUILayout.Button(
                "Restaurar recepción manual",
                GUILayout.Height(28f)
            ))
        {
            RestoreManualReceipt();
        }
        EditorGUI.EndDisabledGroup();

        DrawManualReceiptStatus();

        GUILayout.Space(8f);
        EditorGUILayout.HelpBox(status, failed.Count > 0 ? MessageType.Error : MessageType.None);

        if (EditorApplication.isPlaying)
        {
            DrawLiveScenarioStatus();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (passed.Count > 0 || failed.Count > 0)
        {
            EditorGUILayout.LabelField(
                "Correctos: " + passed.Count + "    Fallos: " + failed.Count,
                EditorStyles.boldLabel
            );
            for (int index = 0; index < passed.Count; index++)
            {
                EditorGUILayout.LabelField("✓ " + passed[index], EditorStyles.wordWrappedLabel);
            }
            for (int index = 0; index < failed.Count; index++)
            {
                EditorGUILayout.LabelField("✗ " + failed[index], EditorStyles.wordWrappedLabel);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void RunAutomaticTest()
    {
        passed.Clear();
        failed.Clear();
        scenarioActive = false;

        if (!EditorApplication.isPlaying)
        {
            failed.Add("La prueba requiere Play Mode.");
            status = "Entra en Play Mode.";
            return;
        }

        BistroBuilderInventoryWarehouseService warehouse =
            Object.FindFirstObjectByType<BistroBuilderInventoryWarehouseService>();
        BistroBuilderInventoryWarehouseRuntimeView view =
            Object.FindFirstObjectByType<BistroBuilderInventoryWarehouseRuntimeView>();
        BistroBuilderInventoryService inventory =
            Object.FindFirstObjectByType<BistroBuilderInventoryService>();
        BistroBuilderInventoryPlanningService planning =
            Object.FindFirstObjectByType<BistroBuilderInventoryPlanningService>();
        BistroBuilderGoodsReceivingService receiving =
            Object.FindFirstObjectByType<BistroBuilderGoodsReceivingService>();
        BistroBuilderDishAvailabilityService availability =
            Object.FindFirstObjectByType<BistroBuilderDishAvailabilityService>();

        BistroBuilderInventoryRuntimeSnapshot originalInventory = null;
        BistroBuilderInventoryPolicySaveData originalPolicy = null;
        string error = string.Empty;

        try
        {
            Check(warehouse != null, "Existe la fachada 2.2D en Play Mode.");
            Check(view != null, "Existe la UI definitiva 2.2D en Play Mode.");
            Check(inventory != null && inventory.IsInitialized,
                "El inventario canónico está inicializado.");
            Check(planning != null && planning.IsInitialized,
                "La planificación 2.2C está inicializada.");
            Check(receiving != null,
                "El servicio de recepciones 2.2B está disponible.");

            if (warehouse == null || view == null || inventory == null ||
                planning == null || receiving == null)
            {
                status = "Faltan servicios esenciales; no se continúa.";
                return;
            }

            Check(warehouse.ValidateConfiguration(out error),
                "La fachada 2.2D valida sus dependencias.", error);
            Check(warehouse.ValidateReadModel(out error),
                "La lectura agregada coincide con inventario/planning.", error);

            Check(inventory.TryCaptureRuntimeSnapshot(out originalInventory, out error),
                "Se captura el inventario original para rollback.", error);
            Check(planning.TryCapturePolicySnapshot(out originalPolicy, out error),
                "Se captura inventory.policy original para rollback.", error);

            var all = new List<BistroBuilderInventoryWarehouseIngredientSnapshot>();
            Check(warehouse.CopyIngredientsTo(
                    all,
                    BistroBuilderInventoryWarehouseFilter.All,
                    BistroBuilderInventoryWarehouseSort.Name,
                    string.Empty,
                    out error
                ) && all.Count == inventory.StockEntryCount,
                "La UI obtiene todos los ingredientes reales.", error);

            if (all.Count == 0)
            {
                status = "No hay ingredientes para continuar.";
                return;
            }

            BistroBuilderInventoryWarehouseIngredientSnapshot selected = all[0];
            Check(selected.OnHandCanonicalMilliUnits >= selected.ReservedCanonicalMilliUnits,
                "Stock total/disponible/reservado de la lectura son coherentes.");
            Check(selected.MinimumStockCanonicalMilliUnits >= 0L,
                "El stock mínimo procede de 2.2C.");
            Check(Enum.IsDefined(typeof(BistroBuilderInventoryStockLevelState), selected.StockLevelState),
                "El estado de alerta procede del contrato canónico.");
            Check(selected.NearExpiryAvailableCanonicalMilliUnits <= selected.AvailableCanonicalMilliUnits,
                "La cantidad próxima a caducar no supera el disponible.");
            Check(Enum.IsDefined(typeof(BistroBuilderInventoryForecastState), selected.ForecastState),
                "La previsión procede de 2.2C.");

            Check(view.TryOpenFromInterface(out error),
                "La interfaz se abre desde el HUD sin excepción.", error);
            Check(view.TryValidateVisibleContent(out error),
                "ScrollRect, RectMask2D, filas y controles son visibles.", error);
            Check(view.TrySelectIngredientForTest(selected.IngredientId, out error) &&
                  view.SelectedIngredientId == selected.IngredientId,
                "La selección de ingrediente actualiza el panel de detalle.", error);

            Check(view.TrySetFilterForTest(
                    BistroBuilderInventoryWarehouseFilter.LowStock,
                    out error),
                "El filtro Stock bajo responde.", error);
            Check(view.TrySetFilterForTest(
                    BistroBuilderInventoryWarehouseFilter.NearExpiry,
                    out error),
                "El filtro Próximos a caducar responde.", error);
            Check(view.TrySetFilterForTest(
                    BistroBuilderInventoryWarehouseFilter.All,
                    out error),
                "El filtro Todos restaura el listado.", error);

            Check(view.TrySetSortForTest(
                    BistroBuilderInventoryWarehouseSort.AvailableStock,
                    out error) &&
                  view.TrySetSortForTest(
                    BistroBuilderInventoryWarehouseSort.Status,
                    out error) &&
                  view.TrySetSortForTest(
                    BistroBuilderInventoryWarehouseSort.Expiration,
                    out error) &&
                  view.TrySetSortForTest(
                    BistroBuilderInventoryWarehouseSort.Name,
                    out error),
                "Las cuatro ordenaciones funcionan sin reconstruir autoridades.", error);

            long testMinimum = selected.AvailableCanonicalMilliUnits <
                               BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits
                ? selected.AvailableCanonicalMilliUnits + 1L
                : selected.AvailableCanonicalMilliUnits;
            Check(warehouse.TrySetMinimumStock(
                    selected.IngredientId,
                    testMinimum,
                    out error),
                "Modificar el mínimo usa el comando de 2.2C.", error);

            var lowRows = new List<BistroBuilderInventoryWarehouseIngredientSnapshot>();
            Check(warehouse.CopyIngredientsTo(
                    lowRows,
                    BistroBuilderInventoryWarehouseFilter.LowStock,
                    BistroBuilderInventoryWarehouseSort.Status,
                    string.Empty,
                    out error) && Contains(lowRows, selected.IngredientId),
                "El ingrediente entra inmediatamente en el filtro de stock bajo.", error);

            long adjustment = Math.Max(1L,
                BistroBuilderMeasurementUtility.MilliUnitsPerCanonicalUnit);
            Check(warehouse.TryAdjustStock(
                    selected.IngredientId,
                    adjustment,
                    BistroBuilderInventoryManualAdjustmentReason.InventoryCorrection,
                    "Prueba funcional 2.2D +",
                    out string positiveOperation,
                    out error) && !string.IsNullOrWhiteSpace(positiveOperation),
                "Un ajuste positivo se aplica por la autoridad canónica.", error);

            Check(inventory.TryGetStockSnapshot(
                    selected.IngredientId,
                    out BistroBuilderInventoryStockSnapshot afterPositive) &&
                  afterPositive.OnHandCanonicalMilliUnits ==
                    selected.OnHandCanonicalMilliUnits + adjustment,
                "El ajuste positivo actualiza inmediatamente el stock real.");

            Check(warehouse.TryAdjustStock(
                    selected.IngredientId,
                    -adjustment,
                    BistroBuilderInventoryManualAdjustmentReason.InventoryCorrection,
                    "Prueba funcional 2.2D -",
                    out string negativeOperation,
                    out error) && !string.IsNullOrWhiteSpace(negativeOperation),
                "Un ajuste negativo controlado se aplica correctamente.", error);

            var movements = new List<BistroBuilderInventoryWarehouseMovementSnapshot>();
            Check(warehouse.CopyMovementsTo(movements, 80, false, out error) &&
                  ContainsCorrection(movements, positiveOperation) &&
                  ContainsCorrection(movements, negativeOperation),
                "Los ajustes generan movimientos Correction jugables.", error);

            string receiptId = "receipt_22d_" + Guid.NewGuid().ToString("N");
            var receiptLines = new List<BistroBuilderInventoryQuantityLine>
            {
                new BistroBuilderInventoryQuantityLine(
                    selected.IngredientId,
                    BistroBuilderMeasurementUtility.MilliUnitsPerCanonicalUnit
                )
            };
            Check(receiving.TryReceiveGoods(
                    receiptId,
                    "supplier_22d_test",
                    receiptLines,
                    "Recepción temporal de prueba 2.2D.",
                    out BistroBuilderGoodsReceiptSnapshot receipt,
                    out error) && receipt != null,
                "Una recepción 2.2B refresca la administración 2.2D.", error);

            var receipts = new List<BistroBuilderInventoryWarehouseReceiptSnapshot>();
            Check(warehouse.CopyReceiptsTo(receipts, 80, out error) &&
                  ContainsReceipt(receipts, receiptId),
                "La pestaña Recepciones reconstruye la recepción desde Purchase.", error);

            Check(view.TrySetSectionForTest(
                    BistroBuilderInventoryWarehouseSection.Alerts,
                    out error) &&
                  view.TrySetSectionForTest(
                    BistroBuilderInventoryWarehouseSection.Movements,
                    out error) &&
                  view.TrySetSectionForTest(
                    BistroBuilderInventoryWarehouseSection.Receipts,
                    out error) &&
                  view.TrySetSectionForTest(
                    BistroBuilderInventoryWarehouseSection.Stock,
                    out error),
                "Las cuatro secciones de la interfaz son navegables.", error);

            int poolBefore = view.RowPoolCount;
            int subscriptionBefore = view.SubscriptionGeneration;
            view.Close();
            Check(view.TryOpenFromInterface(out error) &&
                  view.RowPoolCount == poolBefore &&
                  view.SubscriptionGeneration == subscriptionBefore,
                "Cerrar y reabrir no duplica filas ni listeners.", error);

            Check(availability == null || availability.RecalculateAll(out error),
                "La disponibilidad de platos sigue recalculando sobre el inventario modificado.", error);

            Check(inventory.TryCaptureRuntimeSnapshot(
                    out BistroBuilderInventoryRuntimeSnapshot temporaryInventory,
                    out error),
                "El estado con ajustes/recepción se captura para persistencia.", error);
            Check(planning.TryCapturePolicySnapshot(
                    out BistroBuilderInventoryPolicySaveData temporaryPolicy,
                    out error),
                "El mínimo modificado se captura en inventory.policy.", error);

            string inventoryJson = temporaryInventory != null
                ? JsonUtility.ToJson(temporaryInventory, false)
                : string.Empty;
            BistroBuilderInventoryRuntimeSnapshot inventoryRoundTrip =
                !string.IsNullOrWhiteSpace(inventoryJson)
                    ? JsonUtility.FromJson<BistroBuilderInventoryRuntimeSnapshot>(inventoryJson)
                    : null;
            Check(inventoryRoundTrip != null &&
                  inventoryRoundTrip.TryValidateBasic(out error),
                "inventory.canonical v2 realiza round-trip JSON tras 2.2D.", error);

            string policyJson = temporaryPolicy != null
                ? JsonUtility.ToJson(temporaryPolicy, false)
                : string.Empty;
            BistroBuilderInventoryPolicySaveData policyRoundTrip =
                !string.IsNullOrWhiteSpace(policyJson)
                    ? JsonUtility.FromJson<BistroBuilderInventoryPolicySaveData>(policyJson)
                    : null;
            Check(policyRoundTrip != null && policyRoundTrip.TryValidateBasic(out error),
                "inventory.policy v1 realiza round-trip JSON tras 2.2D.", error);

            Check(temporaryInventory != null &&
                  inventory.TryReplaceFromRuntimeSnapshot(
                    temporaryInventory,
                    true,
                    out error) &&
                  planning.TryReplacePolicySnapshot(
                    temporaryPolicy,
                    true,
                    out error),
                "La simulación de carga restaura stock y política sin duplicados.", error);

            Check(warehouse.ValidateReadModel(out error),
                "La lectura 2.2D sigue coherente después de la carga simulada.", error);

            status = failed.Count == 0
                ? "PRUEBA FUNCIONAL 2.2D SUPERADA"
                : "PRUEBA FUNCIONAL 2.2D CON FALLOS";
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
            if (inventory != null && originalInventory != null)
            {
                if (!inventory.TryReplaceFromRuntimeSnapshot(
                        originalInventory,
                        true,
                        out string restoreInventoryError))
                {
                    failed.Add("No se pudo restaurar el inventario original: " +
                               restoreInventoryError);
                }
            }

            if (planning != null && originalPolicy != null)
            {
                if (!planning.TryReplacePolicySnapshot(
                        originalPolicy,
                        true,
                        out string restorePolicyError))
                {
                    failed.Add("No se pudo restaurar la política original: " +
                               restorePolicyError);
                }
            }

            if (view != null)
            {
                view.TrySetFilterForTest(
                    BistroBuilderInventoryWarehouseFilter.All,
                    out _
                );
                view.TrySetSortForTest(
                    BistroBuilderInventoryWarehouseSort.Name,
                    out _
                );
                view.TrySetSectionForTest(
                    BistroBuilderInventoryWarehouseSection.Stock,
                    out _
                );
            }

            if (failed.Count == 0)
            {
                Debug.Log(BuildReport());
            }
            else
            {
                Debug.LogError(BuildReport());
            }
        }
    }

    private void GenerateManualReceipt()
    {
        if (!EditorApplication.isPlaying)
        {
            manualReceiptStatus = "Entra en Play Mode.";
            return;
        }

        if (manualReceiptOriginalInventory != null)
        {
            manualReceiptStatus =
                "Ya existe una recepción manual activa. Restaúrala antes de generar otra.";
            return;
        }

        BistroBuilderInventoryService inventory =
            Object.FindFirstObjectByType<BistroBuilderInventoryService>();
        BistroBuilderGoodsReceivingService receiving =
            Object.FindFirstObjectByType<BistroBuilderGoodsReceivingService>();
        BistroBuilderGoodsReceivingPresentation presentation =
            Object.FindFirstObjectByType<BistroBuilderGoodsReceivingPresentation>();
        BistroBuilderDishAvailabilityService availability =
            Object.FindFirstObjectByType<BistroBuilderDishAvailabilityService>();
        BistroBuilderInventoryWarehouseService warehouse =
            Object.FindFirstObjectByType<BistroBuilderInventoryWarehouseService>();
        RestaurantServiceStateService serviceState =
            Object.FindFirstObjectByType<RestaurantServiceStateService>();

        if (inventory == null || receiving == null || serviceState == null)
        {
            manualReceiptStatus =
                "Faltan inventario, recepción 2.2B o estado de servicio.";
            return;
        }

        if (!serviceState.IsClosed)
        {
            manualReceiptStatus =
                "La recepción manual reversible requiere el servicio CERRADO. " +
                "Sal de Play Mode, vuelve a entrar y no abras el servicio.";
            return;
        }

        if (presentation != null && presentation.IsBusy)
        {
            manualReceiptStatus =
                "El repartidor visual está ocupado. Espera a que termine antes de generar otra recepción.";
            return;
        }

        string error = string.Empty;
        if (!inventory.TryGetStockSnapshot(
                ManualReceiptIngredientId,
                out BistroBuilderInventoryStockSnapshot before
            ))
        {
            manualReceiptStatus =
                "No se encuentra el stock canónico de Merluza.";
            return;
        }

        if (!inventory.TryCaptureRuntimeSnapshot(
                out BistroBuilderInventoryRuntimeSnapshot snapshot,
                out error
            ))
        {
            manualReceiptStatus =
                "No se pudo capturar el snapshot previo: " + error;
            return;
        }

        if (!BistroBuilderMeasurementUtility.TryConvertToCanonicalMilliUnits(
                1d,
                BistroBuilderMeasurementUnit.Kilogram,
                out long oneKilogram,
                out error
            ))
        {
            manualReceiptStatus =
                "No se pudo convertir 1 kg a unidades canónicas: " + error;
            return;
        }

        string receiptId =
            "receipt_22d_manual_" + Guid.NewGuid().ToString("N");
        var lines = new List<BistroBuilderInventoryQuantityLine>
        {
            new BistroBuilderInventoryQuantityLine(
                ManualReceiptIngredientId,
                oneKilogram
            )
        };

        if (!receiving.TryReceiveGoods(
                receiptId,
                "supplier_22d_manual",
                lines,
                "Recepción manual controlada de 2.2D.",
                out BistroBuilderGoodsReceiptSnapshot receipt,
                out error
            ) ||
            receipt == null)
        {
            manualReceiptStatus =
                "La recepción 2.2B falló: " + error;
            return;
        }

        manualReceiptOriginalInventory = snapshot;
        manualReceiptId = receipt.ReceiptId;
        manualReceiptBeforeOnHand = before.OnHandCanonicalMilliUnits;
        manualReceiptExpectedAdded = oneKilogram;

        if (availability != null)
        {
            availability.RecalculateAll(out _);
        }

        bool receiptVisible = false;
        if (warehouse != null)
        {
            var receipts =
                new List<BistroBuilderInventoryWarehouseReceiptSnapshot>();
            if (warehouse.CopyReceiptsTo(receipts, 80, out _))
            {
                receiptVisible = ContainsReceipt(receipts, manualReceiptId);
            }
        }

        manualReceiptStatus =
            "Recepción REAL creada: " + manualReceiptId +
            " · Merluza +1 kg. " +
            (receiptVisible
                ? "Ya aparece en la lectura de RECEPCIONES. "
                : "La lectura de RECEPCIONES se actualizará por eventos. ") +
            "Observa el repartidor y después revisa EXISTENCIAS, MOVIMIENTOS y RECEPCIONES. " +
            "Cuando termines, pulsa RESTAURAR.";
        Repaint();
    }

    private void RestoreManualReceipt()
    {
        if (!EditorApplication.isPlaying)
        {
            manualReceiptStatus = "La restauración requiere Play Mode.";
            return;
        }

        if (manualReceiptOriginalInventory == null)
        {
            manualReceiptStatus =
                "No hay una recepción manual pendiente de restaurar.";
            return;
        }

        BistroBuilderInventoryService inventory =
            Object.FindFirstObjectByType<BistroBuilderInventoryService>();
        BistroBuilderGoodsReceivingPresentation presentation =
            Object.FindFirstObjectByType<BistroBuilderGoodsReceivingPresentation>();
        BistroBuilderDishAvailabilityService availability =
            Object.FindFirstObjectByType<BistroBuilderDishAvailabilityService>();
        BistroBuilderInventoryWarehouseService warehouse =
            Object.FindFirstObjectByType<BistroBuilderInventoryWarehouseService>();
        RestaurantServiceStateService serviceState =
            Object.FindFirstObjectByType<RestaurantServiceStateService>();

        if (inventory == null || serviceState == null)
        {
            manualReceiptStatus =
                "Falta inventario o estado de servicio para restaurar.";
            return;
        }

        if (!serviceState.IsClosed)
        {
            manualReceiptStatus =
                "No se restaura mientras exista un servicio en curso. " +
                "La prueba manual debe realizarse con el servicio cerrado.";
            return;
        }

        if (presentation != null && presentation.IsBusy)
        {
            manualReceiptStatus =
                "Espera a que el repartidor visual termine de salir antes de restaurar.";
            return;
        }

        if (!inventory.TryReplaceFromRuntimeSnapshot(
                manualReceiptOriginalInventory,
                true,
                out string error
            ))
        {
            manualReceiptStatus =
                "No se pudo restaurar el snapshot previo: " + error;
            return;
        }

        if (availability != null)
        {
            availability.RecalculateAll(out _);
        }

        string validation = string.Empty;
        if (warehouse != null &&
            !warehouse.ValidateReadModel(out validation))
        {
            manualReceiptStatus =
                "El snapshot se restauró, pero la lectura 2.2D informa: " +
                validation;
            return;
        }

        ClearManualReceiptState();
        manualReceiptStatus =
            "Recepción manual restaurada. El stock, lotes, ledger, OperationId " +
            "y la fila de RECEPCIONES han vuelto exactamente al snapshot previo.";
        Repaint();
    }

    private void DrawManualReceiptStatus()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.LabelField(
                "Estado: entra en Play Mode para usar la recepción manual.",
                EditorStyles.wordWrappedMiniLabel
            );
            return;
        }

        BistroBuilderGoodsReceivingPresentation presentation =
            Object.FindFirstObjectByType<BistroBuilderGoodsReceivingPresentation>();
        BistroBuilderInventoryService inventory =
            Object.FindFirstObjectByType<BistroBuilderInventoryService>();

        EditorGUILayout.LabelField(
            "Estado: " + manualReceiptStatus,
            EditorStyles.wordWrappedLabel
        );

        if (manualReceiptOriginalInventory == null)
        {
            return;
        }

        string visualState = presentation != null
            ? presentation.CurrentState.ToString()
            : "Sin presentación";
        string visualBusy = presentation != null && presentation.IsBusy
            ? "Sí"
            : "No";

        EditorGUILayout.LabelField(
            "ReceiptId: " + manualReceiptId +
            " · Repartidor: " + visualState +
            " · Visual ocupado: " + visualBusy,
            EditorStyles.wordWrappedMiniLabel
        );

        if (inventory != null &&
            inventory.TryGetStockSnapshot(
                ManualReceiptIngredientId,
                out BistroBuilderInventoryStockSnapshot current
            ))
        {
            double beforeKg =
                BistroBuilderMeasurementUtility
                    .ConvertCanonicalMilliUnitsToDisplayAmount(
                        manualReceiptBeforeOnHand,
                        BistroBuilderMeasurementUnit.Kilogram
                    );
            double currentKg =
                BistroBuilderMeasurementUtility
                    .ConvertCanonicalMilliUnitsToDisplayAmount(
                        current.OnHandCanonicalMilliUnits,
                        BistroBuilderMeasurementUnit.Kilogram
                    );
            double expectedKg =
                BistroBuilderMeasurementUtility
                    .ConvertCanonicalMilliUnitsToDisplayAmount(
                        manualReceiptExpectedAdded,
                        BistroBuilderMeasurementUnit.Kilogram
                    );

            EditorGUILayout.LabelField(
                "Merluza antes: " + beforeKg.ToString("0.###") +
                " kg · Ahora: " + currentKg.ToString("0.###") +
                " kg · Incremento esperado: +" +
                expectedKg.ToString("0.###") + " kg",
                EditorStyles.wordWrappedMiniLabel
            );
        }
    }

    private void ClearManualReceiptState()
    {
        manualReceiptOriginalInventory = null;
        manualReceiptId = string.Empty;
        manualReceiptBeforeOnHand = 0L;
        manualReceiptExpectedAdded = 0L;
    }

    private void PrepareRealisticServiceScenario()
    {
        if (!EditorApplication.isPlaying)
        {
            status = "Entra en Play Mode antes de preparar el escenario.";
            return;
        }

        CustomerGroupSpawner spawner =
            Object.FindFirstObjectByType<CustomerGroupSpawner>();
        RestaurantServiceStateService serviceState =
            Object.FindFirstObjectByType<RestaurantServiceStateService>();
        RestaurantSeatRegistry seatRegistry =
            Object.FindFirstObjectByType<RestaurantSeatRegistry>();
        RestaurantTableRegistry tableRegistry =
            Object.FindFirstObjectByType<RestaurantTableRegistry>();

        if (spawner == null || serviceState == null || seatRegistry == null ||
            tableRegistry == null)
        {
            status = "Faltan spawner, estado de servicio o registros de mesas/sillas.";
            return;
        }

        if (serviceState.AcceptsNewCustomers)
        {
            status = "El servicio ya está abierto. Sal y vuelve a entrar en Play Mode para preparar el escenario desde Closed.";
            return;
        }

        var sizes = new List<int> { 2, 2, 1, 2, 1, 2 };
        var modes = new List<BistroBuilderServiceMode>
        {
            BistroBuilderServiceMode.TableService,
            BistroBuilderServiceMode.TableService,
            BistroBuilderServiceMode.BarService,
            BistroBuilderServiceMode.TableService,
            BistroBuilderServiceMode.WaitingAtBar,
            BistroBuilderServiceMode.TableService
        };

        string error = string.Empty;
        if (!spawner.TryConfigureDiagnosticGroupSizes(sizes, out error) ||
            !spawner.TryConfigureDiagnosticServiceModes(modes, out error))
        {
            status = error;
            return;
        }

        SerializedObject serialized = new SerializedObject(spawner);
        SetSerializedInt(serialized, "numberOfGroups", 6);
        SetSerializedFloat(serialized, "firstSpawnDelay", 0.5f);
        SetSerializedFloat(serialized, "timeBetweenGroups", 2.0f);
        SetSerializedInt(serialized, "minimumGroupSize", 1);
        SetSerializedInt(serialized, "maximumGroupSize", 2);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        int seats = seatRegistry.RegisteredSeatCount;
        int tables = tableRegistry.RegisteredTableCount;

        if (!serviceState.TryOpenService())
        {
            status = "No se pudo abrir el servicio después de configurar la carga realista.";
            return;
        }

        scenarioActive = true;
        status =
            "Escenario iniciado: 6 grupos / 10 clientes. Capacidad actual: " +
            tables + " mesas, " + seats + " sillas. " +
            "Con 8 o más sillas la escena actual es suficiente; el exceso de clientes puede esperar/barra, lo que hace la prueba más realista. " +
            "Abre INVENTARIO durante el servicio y observa Reservado, Disponible, Movimientos y Alertas.";
    }

    private void DrawLiveScenarioStatus()
    {
        CustomerGroup[] groups = Object.FindObjectsByType<CustomerGroup>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
        BistroBuilderInventoryService inventory =
            Object.FindFirstObjectByType<BistroBuilderInventoryService>();
        BistroBuilderInventoryWarehouseRuntimeView view =
            Object.FindFirstObjectByType<BistroBuilderInventoryWarehouseRuntimeView>();
        RestaurantSeatRegistry seats =
            Object.FindFirstObjectByType<RestaurantSeatRegistry>();
        RestaurantTableRegistry tables =
            Object.FindFirstObjectByType<RestaurantTableRegistry>();
        BistroBuilderCanonicalOrderService orders =
            Object.FindFirstObjectByType<BistroBuilderCanonicalOrderService>();
        BistroBuilderDishCatalogService dishCatalog =
            Object.FindFirstObjectByType<BistroBuilderDishCatalogService>();
        BistroBuilderRecipeCatalogService recipeCatalog =
            Object.FindFirstObjectByType<BistroBuilderRecipeCatalogService>();

        EditorGUILayout.LabelField("Monitor de servicio realista", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Grupos activos: " + (groups != null ? groups.Length : 0));
        EditorGUILayout.LabelField("Mesas registradas: " + (tables != null ? tables.RegisteredTableCount : 0));
        EditorGUILayout.LabelField("Sillas registradas: " + (seats != null ? seats.RegisteredSeatCount : 0));
        EditorGUILayout.LabelField("Reservas inventario (runtime): " + (inventory != null ? inventory.ReservationCount : 0));
        EditorGUILayout.LabelField("Movimientos inventario: " + (inventory != null ? inventory.TransactionCount : 0));
        EditorGUILayout.LabelField("Inventario abierto: " + (view != null && view.IsOpen ? "Sí" : "No"));

        GUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Comandas / reservas (desplazable)",
            EditorStyles.boldLabel
        );

        // Mantiene el resumen del servicio siempre visible y confina el
        // detalle potencialmente largo de comandas a un área con scroll
        // propia. Así la herramienta puede permanecer acoplada o en una
        // ventana pequeña junto al Game View sin obligar a maximizarla.
        float availableHeight = Mathf.Max(180f, position.height - 385f);
        float monitorHeight = Mathf.Clamp(availableHeight, 180f, 520f);
        liveOrdersScroll = EditorGUILayout.BeginScrollView(
            liveOrdersScroll,
            false,
            true,
            GUILayout.Height(monitorHeight)
        );
        DrawActiveOrdersAndReservations(
            orders,
            inventory,
            dishCatalog,
            recipeCatalog
        );
        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Monitor exclusivamente diagnóstico para la prueba realista de 2.2D.
    /// No crea ni modifica comandas/reservas: lee las autoridades canónicas
    /// de 367/368 y muestra su relación para que la validación manual pueda
    /// seguir una comanda concreta hasta los ingredientes reservados.
    /// </summary>
    private void DrawActiveOrdersAndReservations(
        BistroBuilderCanonicalOrderService orderService,
        BistroBuilderInventoryService inventory,
        BistroBuilderDishCatalogService dishCatalog,
        BistroBuilderRecipeCatalogService recipeCatalog
    )
    {
        EditorGUILayout.LabelField(
            "Comandas canónicas activas y reservas",
            EditorStyles.boldLabel
        );

        if (orderService == null)
        {
            EditorGUILayout.HelpBox(
                "No se encuentra BistroBuilderCanonicalOrderService.",
                MessageType.Warning
            );
            return;
        }

        liveOrderBuffer.Clear();
        orderService.CopyOrderSnapshotsTo(liveOrderBuffer);

        int activeOrderCount = 0;
        int activeLineCount = 0;
        int linkedActiveReservationCount = 0;

        for (int orderIndex = 0; orderIndex < liveOrderBuffer.Count; orderIndex++)
        {
            BistroBuilderCanonicalOrder order = liveOrderBuffer[orderIndex];
            if (order == null || order.IsTerminal)
            {
                continue;
            }

            activeOrderCount++;
            for (int lineIndex = 0; lineIndex < order.Lines.Count; lineIndex++)
            {
                BistroBuilderCanonicalOrderLine line = order.Lines[lineIndex];
                if (line != null && !line.IsTerminal)
                {
                    activeLineCount++;
                }

                if (line != null &&
                    inventory != null &&
                    TryGetLineReservation(inventory, order, line, out BistroBuilderInventoryReservationSnapshot reservation) &&
                    reservation != null &&
                    reservation.Status == BistroBuilderInventoryReservationStatus.Active)
                {
                    linkedActiveReservationCount++;
                }
            }
        }

        EditorGUILayout.LabelField(
            "Comandas activas: " + activeOrderCount +
            " · Líneas no terminales: " + activeLineCount +
            " · Reservas activas enlazadas: " + linkedActiveReservationCount,
            EditorStyles.wordWrappedLabel
        );

        if (activeOrderCount == 0)
        {
            EditorGUILayout.HelpBox(
                "Todavía no hay comandas canónicas activas. Espera a que un grupo haga su pedido.",
                MessageType.Info
            );
            return;
        }

        BistroBuilderIngredientCatalog ingredientCatalog =
            recipeCatalog != null ? recipeCatalog.IngredientCatalog : null;

        for (int orderIndex = 0; orderIndex < liveOrderBuffer.Count; orderIndex++)
        {
            BistroBuilderCanonicalOrder order = liveOrderBuffer[orderIndex];
            if (order == null || order.IsTerminal)
            {
                continue;
            }

            GUILayout.Space(5f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                "COMANDA " + ShortId(order.OrderId) +
                " · " + order.State +
                " · " + FormatServiceMode(order.ServiceMode),
                EditorStyles.boldLabel
            );
            EditorGUILayout.LabelField(
                "Destino: " + order.ServiceDestinationReferenceId +
                " · Grupo: " + order.CustomerGroupReferenceId +
                " · Servicio: " + order.MealService,
                EditorStyles.wordWrappedMiniLabel
            );

            for (int lineIndex = 0; lineIndex < order.Lines.Count; lineIndex++)
            {
                BistroBuilderCanonicalOrderLine line = order.Lines[lineIndex];
                if (line == null)
                {
                    continue;
                }

                string dishName = ResolveDishName(dishCatalog, line.DishId);
                EditorGUILayout.LabelField(
                    "  • " + dishName +
                    " · " + line.State +
                    " · Pase " + line.CourseIndex +
                    (line.IsShared ? " · Compartido x" + line.ConsumerCustomerIds.Count : string.Empty),
                    EditorStyles.wordWrappedLabel
                );

                if (inventory == null)
                {
                    EditorGUILayout.LabelField(
                        "      Reserva: inventario no disponible.",
                        EditorStyles.miniLabel
                    );
                    continue;
                }

                if (!TryGetLineReservation(
                        inventory,
                        order,
                        line,
                        out BistroBuilderInventoryReservationSnapshot reservation
                    ) || reservation == null)
                {
                    EditorGUILayout.LabelField(
                        "      Reserva: todavía no creada / no localizada.",
                        EditorStyles.miniLabel
                    );
                    continue;
                }

                EditorGUILayout.LabelField(
                    "      Reserva " + ShortId(reservation.ReservationId) +
                    " · " + reservation.Status,
                    reservation.Status == BistroBuilderInventoryReservationStatus.Active
                        ? EditorStyles.boldLabel
                        : EditorStyles.miniLabel
                );

                for (int reservationLineIndex = 0;
                     reservationLineIndex < reservation.Lines.Count;
                     reservationLineIndex++)
                {
                    BistroBuilderInventoryReservationLineSnapshot reservationLine =
                        reservation.Lines[reservationLineIndex];
                    if (reservationLine == null)
                    {
                        continue;
                    }

                    ResolveIngredientPresentation(
                        ingredientCatalog,
                        reservationLine.IngredientId,
                        out string ingredientName,
                        out BistroBuilderMeasurementUnit baseUnit
                    );

                    EditorGUILayout.LabelField(
                        "        - " + ingredientName + ": " +
                        FormatQuantity(
                            reservationLine.CanonicalMilliUnits,
                            baseUnit
                        ),
                        EditorStyles.wordWrappedMiniLabel
                    );
                }
            }

            EditorGUILayout.EndVertical();
        }
    }

    private static bool TryGetLineReservation(
        BistroBuilderInventoryService inventory,
        BistroBuilderCanonicalOrder order,
        BistroBuilderCanonicalOrderLine line,
        out BistroBuilderInventoryReservationSnapshot reservation
    )
    {
        reservation = null;
        if (inventory == null || order == null || line == null)
        {
            return false;
        }

        string reservationId = BuildInventoryReservationId(
            order.OrderId,
            line.LineId
        );
        return inventory.TryGetReservationSnapshot(
            reservationId,
            out reservation
        );
    }

    // Replica únicamente el contrato determinista de identidad de
    // BistroBuilderOrderInventoryLifecycleService para poder consultar una
    // reserva ya existente. No crea reservas ni constituye otra autoridad.
    private static string BuildInventoryReservationId(
        string orderId,
        string lineId
    )
    {
        return BoundRuntimeId(
            "inventory_reservation_" + NormalizeForRuntimeId(orderId) + "_" +
            NormalizeForRuntimeId(lineId)
        );
    }

    private static string BoundRuntimeId(string value)
    {
        string normalized = value != null ? value.Trim() : string.Empty;
        int maximumLength = InventoryMaximumRuntimeIdLength;

        if (normalized.Length <= maximumLength)
        {
            return normalized;
        }

        byte[] hash;
        using (SHA256 algorithm = SHA256.Create())
        {
            hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        }

        var suffix = new StringBuilder(32);
        for (int index = 0; index < 16; index++)
        {
            suffix.Append(hash[index].ToString("x2"));
        }

        int prefixLength = maximumLength - suffix.Length - 1;
        return normalized.Substring(0, prefixLength) + "_" + suffix;
    }

    private static string NormalizeForRuntimeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
        for (int index = 0; index < chars.Length; index++)
        {
            char character = chars[index];
            if (!(char.IsLetterOrDigit(character) || character == '_'))
            {
                chars[index] = '_';
            }
        }

        return new string(chars);
    }

    private static string ResolveDishName(
        BistroBuilderDishCatalogService catalog,
        string dishId
    )
    {
        if (catalog != null &&
            catalog.TryGetDefinition(
                dishId,
                out BistroBuilderDishDefinition definition
            ) &&
            definition != null &&
            !string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            return definition.DisplayName;
        }

        return string.IsNullOrWhiteSpace(dishId) ? "Plato sin ID" : dishId;
    }

    private static void ResolveIngredientPresentation(
        BistroBuilderIngredientCatalog catalog,
        string ingredientId,
        out string displayName,
        out BistroBuilderMeasurementUnit baseUnit
    )
    {
        displayName = string.IsNullOrWhiteSpace(ingredientId)
            ? "Ingrediente sin ID"
            : ingredientId;
        baseUnit = BistroBuilderMeasurementUnit.Unit;

        if (catalog != null &&
            catalog.TryGetDefinition(
                ingredientId,
                out BistroBuilderIngredientDefinition definition
            ) &&
            definition != null)
        {
            displayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.IngredientId
                : definition.DisplayName;
            baseUnit = definition.BaseUnit;
        }
    }

    private static string FormatServiceMode(BistroBuilderServiceMode mode)
    {
        switch (mode)
        {
            case BistroBuilderServiceMode.TableService:
                return "Mesa";
            case BistroBuilderServiceMode.BarService:
                return "Barra";
            case BistroBuilderServiceMode.WaitingAtBar:
                return "Espera en barra";
            default:
                return mode.ToString();
        }
    }

    private static string FormatQuantity(
        long canonicalMilliUnits,
        BistroBuilderMeasurementUnit baseUnit
    )
    {
        double baseAmount = canonicalMilliUnits / 1000d;
        switch (baseUnit)
        {
            case BistroBuilderMeasurementUnit.Gram:
                if (baseAmount >= 1000d)
                {
                    return FormatNumber(baseAmount / 1000d) + " kg";
                }
                return FormatNumber(baseAmount) + " g";

            case BistroBuilderMeasurementUnit.Milliliter:
                if (baseAmount >= 1000d)
                {
                    return FormatNumber(baseAmount / 1000d) + " l";
                }
                return FormatNumber(baseAmount) + " ml";

            case BistroBuilderMeasurementUnit.Unit:
                return FormatNumber(baseAmount) + " ud";

            case BistroBuilderMeasurementUnit.Portion:
                return FormatNumber(baseAmount) + " ración";

            default:
                return FormatNumber(baseAmount) + " " + baseUnit;
        }
    }

    private static string FormatNumber(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 0.0005d)
        {
            return Math.Round(value).ToString("0");
        }

        return value.ToString("0.###");
    }

    private static string ShortId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<sin id>";
        }

        string trimmed = value.Trim();
        return trimmed.Length <= 20
            ? trimmed
            : trimmed.Substring(0, 17) + "...";
    }

    private void Check(bool condition, string message, string detail = "")
    {
        if (condition)
        {
            passed.Add(message);
        }
        else
        {
            failed.Add(
                string.IsNullOrWhiteSpace(detail)
                    ? message
                    : message + " Detalle: " + detail
            );
        }
    }

    private static bool Contains(
        List<BistroBuilderInventoryWarehouseIngredientSnapshot> list,
        string ingredientId
    )
    {
        for (int index = 0; index < list.Count; index++)
        {
            if (list[index].IngredientId == ingredientId)
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsCorrection(
        List<BistroBuilderInventoryWarehouseMovementSnapshot> list,
        string operationId
    )
    {
        for (int index = 0; index < list.Count; index++)
        {
            if (list[index].TransactionType ==
                    BistroBuilderInventoryTransactionType.Correction &&
                list[index].OperationId == operationId)
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsReceipt(
        List<BistroBuilderInventoryWarehouseReceiptSnapshot> list,
        string receiptId
    )
    {
        for (int index = 0; index < list.Count; index++)
        {
            if (list[index] != null && list[index].ReceiptId == receiptId)
            {
                return true;
            }
        }
        return false;
    }

    private string BuildReport()
    {
        var builder = new StringBuilder(12288);
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
        return builder.ToString().TrimEnd();
    }

    private static void SetSerializedInt(
        SerializedObject serialized,
        string name,
        int value
    )
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetSerializedFloat(
        SerializedObject serialized,
        string name,
        float value
    )
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
        {
            property.floatValue = value;
        }
    }
}
