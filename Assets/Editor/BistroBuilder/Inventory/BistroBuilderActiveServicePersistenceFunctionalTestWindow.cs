using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prueba funcional fuerte 368EF sobre un servicio real WaitingAtBar.
///
/// Genera un cliente, bloquea las mesas con un estado persistible, libera una
/// cuando la comanda está en preparación y exige que quede reservada para la
/// transición barra→mesa.
/// Guarda durante el servicio activo, altera mundo, reloj e inventario, carga
/// desde ese mismo servicio y comprueba identidades, posiciones, comanda,
/// cocina, sesión de barra, reserva de mesa, stock y disponibilidad.
/// Todos los ajustes de diagnóstico viven solo en Play Mode.
/// </summary>
public sealed class BistroBuilderActiveServicePersistenceFunctionalTestWindow :
    EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/" +
        "368EF Real Active Service Save Test";

    private const double TimeoutSeconds = 180d;

    private enum TestPhase
    {
        Idle = 0,
        WaitingForPreparingOrder = 1,
        Saving = 2,
        Loading = 3,
        DeletingAfterSuccess = 4,
        DeletingAfterFailure = 5,
        Completed = 6,
        Failed = 7
    }

    private TestPhase phase;
    private string report =
        "Entra en Play Mode con el servicio cerrado y ejecuta la prueba.";
    private MessageType reportType = MessageType.Info;
    private Vector2 scroll;
    private double startedAt;
    private bool subscribed;
    private string pendingFailure = string.Empty;

    private BistroBuilderSaveGameService saveService;
    private RestaurantServiceStateService serviceStateService;
    private CustomerGroupSpawner spawner;
    private TableAssignmentSystem tableAssignmentSystem;
    private RestaurantTableRegistry tableRegistry;
    private BistroBuilderBarServiceSystem barServiceSystem;
    private OrderSystem orderSystem;
    private BistroBuilderCanonicalOrderService canonicalOrderService;
    private BistroBuilderCanonicalOrderIntegrationService orderIntegration;
    private BistroBuilderOrderInventoryLifecycleService lifecycleService;
    private BistroBuilderInventoryService inventoryService;
    private BistroBuilderDishAvailabilityService availabilityService;
    private KitchenSystem kitchen;
    private GameClock gameClock;

    private int diagnosticSlot;
    private string runToken = string.Empty;

    private int savedGroupId;
    private CustomerGroupState savedGroupState;
    private BistroBuilderServiceMode savedRequestedMode;
    private BistroBuilderServiceMode savedCurrentMode;
    private string savedBarSpotId = string.Empty;
    private Vector3 savedGroupPosition;
    private int savedPendingTableId;
    private RestaurantTable tableToRelease;
    private bool tableReleased;

    private int savedWaiterId;
    private Vector3 savedWaiterPosition;

    private int savedLegacyOrderId;
    private string savedCanonicalOrderId = string.Empty;
    private OrderState savedLegacyOrderState;
    private string savedLineId = string.Empty;
    private string savedDishId = string.Empty;
    private BistroBuilderCanonicalOrderLineState savedLineState;
    private string savedReservationId = string.Empty;
    private BistroBuilderInventoryReservationStatus savedReservationStatus;

    private string savedIngredientId = string.Empty;
    private BistroBuilderInventoryStockSnapshot savedStock;
    private BistroBuilderDishAvailabilitySnapshot savedAvailability;
    private RestaurantServiceState savedServiceState;
    private BistroBuilderMealServiceAvailability savedMealService;
    private int savedNextGroupId;
    private int savedNextOrderId;
    private float savedKitchenRemaining;
    private BistroBuilderCustomerSpawnerRuntimeSaveRecord savedSpawnerState;
    private float savedClockSpeed;
    private bool savedClockPaused;

    [MenuItem(MenuPath, false, 354)]
    private static void Open()
    {
        BistroBuilderActiveServicePersistenceFunctionalTestWindow window =
            GetWindow<
                BistroBuilderActiveServicePersistenceFunctionalTestWindow
            >("BB 368EF Active");
        window.minSize = new Vector2(700f, 500f);
        window.Show();
    }

    private void OnDisable()
    {
        UnsubscribeFromSaveService();
    }

    private void Update()
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        if (phase == TestPhase.WaitingForPreparingOrder)
        {
            if (EditorApplication.timeSinceStartup - startedAt >
                TimeoutSeconds)
            {
                FailAndCleanup(
                    "La prueba no alcanzó una comanda real en Preparing " +
                    "antes del timeout."
                );
            }
            else
            {
                TryCaptureRealCheckpointAndSave();
            }
        }

        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "368EF — Guardado real durante servicio activo",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "Genera un cliente WaitingAtBar, reserva una mesa mientras su " +
            "comanda está en preparación y guarda/carga con el servicio " +
            "abierto. La prueba " +
            "usa un slot libre 980-989 y lo elimina al terminar. No salgas " +
            "de Play Mode hasta ver SUPERADA o FALLIDA.",
            MessageType.Info
        );

        bool canRun = EditorApplication.isPlaying &&
                      (phase == TestPhase.Idle ||
                       phase == TestPhase.Completed ||
                       phase == TestPhase.Failed);

        using (new EditorGUI.DisabledScope(!canRun))
        {
            if (GUILayout.Button(
                    "Ejecutar guardado real de servicio activo 368EF",
                    GUILayout.Height(36f)
                ))
            {
                BeginTest();
            }
        }

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Entra en Play Mode con el servicio en Closed.",
                MessageType.Warning
            );
        }

        EditorGUILayout.Space(8f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(report, reportType);
        EditorGUILayout.EndScrollView();
    }

    private void BeginTest()
    {
        ResetRunState();

        if (!ResolveDependencies(out string error))
        {
            SetImmediateFailure(error);
            return;
        }

        if (!serviceStateService.IsClosed)
        {
            SetImmediateFailure(
                "La prueba debe comenzar con el servicio cerrado."
            );
            return;
        }

        saveService.RefreshExtensions();
        if (!saveService.HasProvider(
                BistroBuilderInventorySaveSectionProvider.StableSectionId
            ) ||
            !saveService.HasProvider(
                BistroBuilderActiveServiceSaveSectionProvider.StableSectionId
            ))
        {
            SetImmediateFailure(
                "No están registrados inventory.canonical y service.runtime."
            );
            return;
        }

        diagnosticSlot = FindFreeDiagnosticSlot();
        if (diagnosticSlot < 1)
        {
            SetImmediateFailure(
                "Los slots diagnósticos 980-989 están ocupados."
            );
            return;
        }

        try
        {
            ConfigureRealServiceDiagnostic();
        }
        catch (Exception exception)
        {
            SetImmediateFailure(exception.Message);
            return;
        }

        SubscribeToSaveService();
        phase = TestPhase.WaitingForPreparingOrder;
        startedAt = EditorApplication.timeSinceStartup;
        reportType = MessageType.Info;
        report =
            "Servicio abierto. Esperando una comanda WaitingAtBar real en " +
            "Preparing y una mesa reservada para congelar el checkpoint...";

        if (!serviceStateService.TryOpenService())
        {
            FailAndCleanup(
                "RestaurantServiceStateService rechazó la apertura."
            );
        }
    }

    private void ConfigureRealServiceDiagnostic()
    {
        SerializedObject spawnerSerialized = new SerializedObject(spawner);
        RequireProperty(spawnerSerialized, "numberOfGroups").intValue = 2;
        RequireProperty(spawnerSerialized, "firstSpawnDelay").floatValue =
            0.1f;
        RequireProperty(spawnerSerialized, "timeBetweenGroups").floatValue =
            120f;
        RequireProperty(spawnerSerialized, "minimumGroupSize").intValue = 1;
        RequireProperty(spawnerSerialized, "maximumGroupSize").intValue = 1;
        spawnerSerialized.ApplyModifiedPropertiesWithoutUndo();

        if (!spawner.TryConfigureDiagnosticGroupSizes(
                new[] { 1, 1 },
                out string sizeError
            ))
        {
            throw new InvalidOperationException(sizeError);
        }

        if (!spawner.TryConfigureDiagnosticServiceModes(
                new[]
                {
                    BistroBuilderServiceMode.WaitingAtBar,
                    BistroBuilderServiceMode.TableService
                },
                out string modeError
            ))
        {
            throw new InvalidOperationException(modeError);
        }

        SerializedObject barSerialized =
            new SerializedObject(barServiceSystem);
        RequireProperty(barSerialized, "orderTakingDuration").floatValue =
            0.2f;
        RequireProperty(barSerialized, "consumptionDuration").floatValue =
            1f;
        RequireProperty(barSerialized, "billDeliveryDuration").floatValue =
            0.2f;
        RequireProperty(barSerialized, "paymentDuration").floatValue = 0.2f;
        RequireProperty(barSerialized, "maximumItemsPerBarOrder").intValue = 1;
        barSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject kitchenSerialized = new SerializedObject(kitchen);
        RequireProperty(
            kitchenSerialized,
            "preparationDurationScale"
        ).floatValue = 1f;
        RequireProperty(
            kitchenSerialized,
            "minimumPreparationDuration"
        ).floatValue = 20f;
        RequireProperty(
            kitchenSerialized,
            "maximumPreparationDuration"
        ).floatValue = 20f;
        kitchenSerialized.ApplyModifiedPropertiesWithoutUndo();

        // Las mesas de diagnóstico se bloquean como Dirty, que es el único
        // estado no disponible válido sin grupo. Se suspenden las dos posibles
        // autoridades de limpieza para que no las liberen durante la prueba.
        TableCleaningAssignmentSystem legacyCleaning =
            FindFirstObjectByType<TableCleaningAssignmentSystem>();
        if (legacyCleaning != null)
        {
            legacyCleaning.enabled = false;
        }

        WaiterTaskCoordinator taskCoordinator =
            FindFirstObjectByType<WaiterTaskCoordinator>();
        if (taskCoordinator != null)
        {
            SerializedObject taskCoordinatorSerialized =
                new SerializedObject(taskCoordinator);
            RequireProperty(
                taskCoordinatorSerialized,
                "manageCleaningTasks"
            ).boolValue = false;
            taskCoordinatorSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        RestaurantTable[] tables = FindObjectsByType<RestaurantTable>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID
        );
        if (tables.Length == 0)
        {
            throw new InvalidOperationException(
                "La prueba necesita al menos una mesa operativa."
            );
        }

        Array.Sort(tables, (left, right) =>
            (left != null ? left.TableId : int.MaxValue).CompareTo(
                right != null ? right.TableId : int.MaxValue
            )
        );
        tableToRelease = tables[0];
        for (int index = 0; index < tables.Length; index++)
        {
            if (tables[index] != null)
            {
                tables[index].SetState(TableState.Dirty);
            }
        }

        if (!orderIntegration.TrySetCurrentMealService(
                BistroBuilderMealServiceAvailability.Dinner,
                out string mealServiceError
            ))
        {
            throw new InvalidOperationException(mealServiceError);
        }

        gameClock.SetPaused(false);
        gameClock.SetSpeedMultiplier(2f);
    }

    private void TryCaptureRealCheckpointAndSave()
    {
        CustomerGroup group = FindDiagnosticGroup();
        if (group == null || !group.HasAssignedBarSpot)
        {
            return;
        }

        RestaurantOrder order =
            orderSystem.GetActiveOrderForBarSpot(group.AssignedBarSpot);
        if (order == null || order.CurrentState != OrderState.Preparing ||
            order.AssignedWaiter == null)
        {
            return;
        }

        if (!canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder canonicalOrder
            ) ||
            canonicalOrder == null || canonicalOrder.Lines.Count == 0)
        {
            return;
        }

        BistroBuilderCanonicalOrderLine line = canonicalOrder.Lines[0];
        if (line == null ||
            !lifecycleService.TryGetReservationId(
                order,
                line.LineId,
                out string reservationId
            ) ||
            !inventoryService.TryGetReservationSnapshot(
                reservationId,
                out BistroBuilderInventoryReservationSnapshot reservation
            ) ||
            reservation == null || reservation.Lines.Count == 0 ||
            reservation.Status !=
                BistroBuilderInventoryReservationStatus.Consumed ||
            kitchen.ActiveOrder == null ||
            !string.Equals(
                kitchen.ActiveOrder.CanonicalOrderId,
                order.CanonicalOrderId,
                StringComparison.Ordinal
            ) ||
            kitchen.ActiveRemainingPreparationSeconds <= 0f)
        {
            return;
        }

        if (!tableReleased)
        {
            tableToRelease.SetState(TableState.Free);
            tableAssignmentSystem.RequestReevaluation();
            tableReleased = true;
            report =
                "Comanda WaitingAtBar en Preparing. Mesa " +
                tableToRelease.TableId +
                " liberada; esperando su reserva lógica antes de guardar...";
            Repaint();
            return;
        }

        if (!tableAssignmentSystem.TryGetPendingBarTransitionTable(
                group,
                out RestaurantTable pendingTable
            ) || pendingTable == null ||
            pendingTable.TableId != tableToRelease.TableId)
        {
            return;
        }

        string ingredientId = reservation.Lines[0].IngredientId;
        string error = string.Empty;

        if (!inventoryService.TryGetStockSnapshot(
                ingredientId,
                out BistroBuilderInventoryStockSnapshot stock
            ))
        {
            FailAndCleanup(
                "No se pudo capturar el balance del checkpoint funcional."
            );
            return;
        }

        if (!availabilityService.RecalculateAll(out error) ||
            !availabilityService.TryGetSnapshot(
                line.DishId,
                out BistroBuilderDishAvailabilitySnapshot availability
            ))
        {
            FailAndCleanup(
                "No se pudo capturar la disponibilidad funcional: " +
                (string.IsNullOrWhiteSpace(error)
                    ? "fotografía no disponible."
                    : error)
            );
            return;
        }

        if (!spawner.TryCaptureRuntimeSpawnState(
                out BistroBuilderCustomerSpawnerRuntimeSaveRecord spawnState,
                out error
            ) ||
            spawnState == null ||
            !spawnState.scheduleInitialized ||
            spawnState.scheduleCompleted ||
            spawnState.pendingArrivals.Count != 1 ||
            spawnState.secondsUntilNextArrival <= 0f)
        {
            FailAndCleanup(
                "El checkpoint no conserva exactamente una llegada futura: " +
                (string.IsNullOrWhiteSpace(error)
                    ? "calendario inesperado."
                    : error)
            );
            return;
        }

        savedGroupId = group.GroupId;
        savedGroupState = group.CurrentState;
        savedRequestedMode = group.RequestedServiceMode;
        savedCurrentMode = group.CurrentServiceMode;
        savedBarSpotId = group.AssignedBarSpot.BarSpotId;
        savedGroupPosition = group.transform.position;
        savedPendingTableId = pendingTable.TableId;

        savedWaiterId = order.AssignedWaiter.WaiterId;
        savedWaiterPosition = order.AssignedWaiter.transform.position;

        savedLegacyOrderId = order.OrderId;
        savedCanonicalOrderId = order.CanonicalOrderId;
        savedLegacyOrderState = order.CurrentState;
        savedLineId = line.LineId;
        savedDishId = line.DishId;
        savedLineState = line.State;
        savedReservationId = reservationId;
        savedReservationStatus = reservation.Status;
        savedIngredientId = ingredientId;
        savedStock = stock;
        savedAvailability = availability;
        savedServiceState = serviceStateService.CurrentState;
        savedMealService = orderIntegration.CurrentMealService;
        savedNextGroupId = spawner.NextGroupId;
        savedNextOrderId = orderSystem.NextOrderId;
        savedKitchenRemaining = kitchen.ActiveRemainingPreparationSeconds;
        savedSpawnerState = spawnState;
        savedClockSpeed = gameClock.SpeedMultiplier;
        savedClockPaused = gameClock.IsPaused;

        phase = TestPhase.Saving;
        report =
            "Comanda real " + savedCanonicalOrderId +
            " capturada en Preparing. Guardando slot " + diagnosticSlot +
            " con servicio " + savedServiceState + "...";
        Repaint();

        if (!saveService.TrySaveSlot(
                diagnosticSlot,
                "BB 368EF ACTIVE SERVICE DIAGNOSTIC",
                out string rejection
            ))
        {
            FailAndCleanup(
                "El guardado durante servicio activo fue rechazado: " +
                rejection
            );
        }
    }

    private void HandleOperationCompleted(
        BistroBuilderSaveOperationResult result
    )
    {
        if (result == null || result.SlotIndex != diagnosticSlot)
        {
            return;
        }

        if (!result.Succeeded)
        {
            if (phase == TestPhase.DeletingAfterSuccess ||
                phase == TestPhase.DeletingAfterFailure)
            {
                pendingFailure = string.IsNullOrWhiteSpace(pendingFailure)
                    ? "No se pudo eliminar el slot diagnóstico: " +
                      result.Message
                    : pendingFailure +
                      " No se pudo eliminar el slot diagnóstico: " +
                      result.Message;
                CompleteFailureAfterDelete();
                return;
            }

            FailAndCleanup(
                result.OperationKind + " falló: " + result.Message
            );
            return;
        }

        switch (phase)
        {
            case TestPhase.Saving:
                ContinueAfterSave(result);
                break;

            case TestPhase.Loading:
                ContinueAfterLoad(result);
                break;

            case TestPhase.DeletingAfterSuccess:
                CompleteSuccess();
                break;

            case TestPhase.DeletingAfterFailure:
                CompleteFailureAfterDelete();
                break;
        }
    }

    private void ContinueAfterSave(BistroBuilderSaveOperationResult result)
    {
        CustomerGroup group = FindGroupById(savedGroupId);
        Waiter waiter = FindWaiterById(savedWaiterId);
        if (group == null || waiter == null)
        {
            FailAndCleanup(
                "Las entidades reales desaparecieron después de guardar."
            );
            return;
        }

        group.transform.position = savedGroupPosition +
                                   new Vector3(11f, 0f, 7f);
        waiter.transform.position = savedWaiterPosition +
                                    new Vector3(-9f, 0f, 6f);

        runToken = Guid.NewGuid().ToString("N");
        if (!inventoryService.TryAddStock(
                "active_save_mutation_" + runToken,
                "active_save_test_" + runToken,
                savedIngredientId,
                777000L,
                BistroBuilderInventoryTransactionType.Purchase,
                "Mutación posterior al guardado real 368EF.",
                out string error
            ))
        {
            FailAndCleanup(
                "No se pudo mutar el inventario tras guardar: " + error
            );
            return;
        }

        // La mutación elimina primero la relación temporal para que el punto
        // de rollback previo a la carga siga siendo un estado coherente. El
        // checkpoint guardado debe reconstruir después tanto la mesa Free como
        // la reserva WaitingAtBar original.
        tableAssignmentSystem
            .ClearPendingBarTransitionReservationsForRuntimeLoad();

        if (tableRegistry.TryGetTableById(
                savedPendingTableId,
                out RestaurantTable pendingTable
            ) && pendingTable != null)
        {
            pendingTable.SetState(TableState.Dirty);
        }

        gameClock.SetSpeedMultiplier(3f);
        gameClock.SetPaused(true);

        if (!orderIntegration.TrySetCurrentMealService(
                BistroBuilderMealServiceAvailability.Breakfast,
                out error
            ))
        {
            FailAndCleanup(
                "No se pudo alterar el servicio gastronómico: " + error
            );
            return;
        }

        if (!serviceStateService.TryRestoreState(
                RestaurantServiceState.Closing,
                false
            ))
        {
            FailAndCleanup(
                "No se pudo alterar el estado del servicio antes de cargar."
            );
            return;
        }

        phase = TestPhase.Loading;
        report =
            "Guardado real completado (" + result.PayloadBytes +
            " bytes). Mundo, inventario y servicio alterados; cargando " +
            "desde un servicio todavía activo...";
        Repaint();

        if (!saveService.TryLoadSlot(
                diagnosticSlot,
                out string rejection
            ))
        {
            FailAndCleanup(
                "La carga durante servicio activo fue rechazada: " +
                rejection
            );
        }
    }

    private void ContinueAfterLoad(BistroBuilderSaveOperationResult result)
    {
        if (!ResolveDependencies(out string error))
        {
            FailAndCleanup(
                "No se resolvieron dependencias después de cargar: " + error
            );
            return;
        }

        CustomerGroup group = FindGroupById(savedGroupId);
        Waiter waiter = FindWaiterById(savedWaiterId);
        RestaurantOrder order = FindOrderByCanonicalId(savedCanonicalOrderId);

        if (group == null || waiter == null || order == null)
        {
            FailAndCleanup(
                "La carga no reconstruyó grupo, camarero o comanda con " +
                "sus identidades estables."
            );
            return;
        }

        bool serviceValid =
            serviceStateService.CurrentState == savedServiceState &&
            serviceStateService.IsServiceInProgress;
        bool mealServiceValid =
            orderIntegration.CurrentMealService == savedMealService;
        bool groupValid =
            group.CurrentState == savedGroupState &&
            group.RequestedServiceMode == savedRequestedMode &&
            group.CurrentServiceMode == savedCurrentMode &&
            group.HasAssignedBarSpot &&
            string.Equals(
                group.AssignedBarSpot.BarSpotId,
                savedBarSpotId,
                StringComparison.Ordinal
            ) &&
            Vector3.Distance(
                group.transform.position,
                savedGroupPosition
            ) < 0.05f;
        bool waiterValid =
            Vector3.Distance(
                waiter.transform.position,
                savedWaiterPosition
            ) < 0.05f;
        bool orderValid =
            order.OrderId == savedLegacyOrderId &&
            order.CurrentState == savedLegacyOrderState &&
            order.AssignedWaiter != null &&
            order.AssignedWaiter.WaiterId == savedWaiterId &&
            order.HasBarDestination &&
            string.Equals(
                order.BarSpot.BarSpotId,
                savedBarSpotId,
                StringComparison.Ordinal
            );

        bool canonicalValid =
            canonicalOrderService.TryGetOrderSnapshot(
                savedCanonicalOrderId,
                out BistroBuilderCanonicalOrder canonicalOrder
            ) &&
            canonicalOrder != null &&
            TryFindLine(canonicalOrder, savedLineId, out var line) &&
            line.State == savedLineState &&
            string.Equals(
                line.DishId,
                savedDishId,
                StringComparison.Ordinal
            );

        bool lifecycleValid =
            lifecycleService.TryGetReservationId(
                order,
                savedLineId,
                out string restoredReservationId
            ) &&
            string.Equals(
                restoredReservationId,
                savedReservationId,
                StringComparison.Ordinal
            );

        bool reservationValid =
            inventoryService.TryGetReservationSnapshot(
                savedReservationId,
                out BistroBuilderInventoryReservationSnapshot reservation
            ) &&
            reservation != null &&
            reservation.Status == savedReservationStatus;

        bool stockValid =
            inventoryService.TryGetStockSnapshot(
                savedIngredientId,
                out BistroBuilderInventoryStockSnapshot stock
            ) &&
            AreStockSnapshotsEquivalent(savedStock, stock);

        bool availabilityValid =
            availabilityService.RecalculateAll(out error) &&
            availabilityService.TryGetSnapshot(
                savedDishId,
                out BistroBuilderDishAvailabilitySnapshot availability
            ) &&
            AreAvailabilitySnapshotsEquivalent(
                savedAvailability,
                availability
            );

        bool kitchenValid =
            kitchen.ActiveOrder != null &&
            string.Equals(
                kitchen.ActiveOrder.CanonicalOrderId,
                savedCanonicalOrderId,
                StringComparison.Ordinal
            ) &&
            string.Equals(
                kitchen.ActiveOrderLineId,
                savedLineId,
                StringComparison.Ordinal
            ) &&
            kitchen.ActiveRemainingPreparationSeconds > 0f &&
            kitchen.ActiveRemainingPreparationSeconds <=
                savedKitchenRemaining + 0.1f;

        bool barSessionValid =
            barServiceSystem.TryGetSessionSnapshot(
                group,
                out BistroBuilderBarSessionSnapshot session
            ) &&
            session.ServiceMode == BistroBuilderServiceMode.WaitingAtBar;

        bool pendingTableValid =
            tableAssignmentSystem.TryGetPendingBarTransitionTable(
                group,
                out RestaurantTable restoredPendingTable
            ) &&
            restoredPendingTable != null &&
            restoredPendingTable.TableId == savedPendingTableId &&
            restoredPendingTable.CurrentState == TableState.Free &&
            restoredPendingTable.AssignedCustomerGroup == null;

        bool clockValid =
            Mathf.Approximately(
                gameClock.SpeedMultiplier,
                savedClockSpeed
            ) &&
            gameClock.IsPaused == savedClockPaused &&
            Mathf.Approximately(
                Time.timeScale,
                savedClockPaused ? 0f : savedClockSpeed
            );

        bool arrivalScheduleValid =
            spawner.TryCaptureRuntimeSpawnState(
                out BistroBuilderCustomerSpawnerRuntimeSaveRecord
                    restoredSpawnerState,
                out error
            ) &&
            AreSpawnerStatesEquivalent(
                savedSpawnerState,
                restoredSpawnerState
            );

        bool sequencesValid =
            spawner.NextGroupId == savedNextGroupId &&
            orderSystem.NextOrderId == savedNextOrderId;

        if (!serviceValid || !mealServiceValid || !groupValid ||
            !waiterValid || !orderValid ||
            !canonicalValid || !lifecycleValid || !reservationValid ||
            !stockValid || !availabilityValid || !kitchenValid ||
            !barSessionValid || !pendingTableValid || !clockValid ||
            !arrivalScheduleValid || !sequencesValid)
        {
            FailAndCleanup(
                BuildVerificationFailure(
                    serviceValid,
                    mealServiceValid,
                    groupValid,
                    waiterValid,
                    orderValid,
                    canonicalValid,
                    lifecycleValid,
                    reservationValid,
                    stockValid,
                    availabilityValid,
                    kitchenValid,
                    barSessionValid,
                    pendingTableValid,
                    clockValid,
                    arrivalScheduleValid,
                    sequencesValid,
                    error
                )
            );
            return;
        }

        phase = TestPhase.DeletingAfterSuccess;
        report =
            "Servicio real reconstruido correctamente desde el checkpoint. " +
            "Eliminando el slot diagnóstico...";
        Repaint();

        if (!saveService.TryDeleteSlot(
                diagnosticSlot,
                out string rejection
            ))
        {
            pendingFailure =
                "La reconstrucción fue correcta, pero no se pudo eliminar " +
                "el slot diagnóstico: " + rejection;
            CompleteFailureAfterDelete();
        }
    }

    private void CompleteSuccess()
    {
        phase = TestPhase.Completed;
        reportType = MessageType.Info;
        report =
            "BISTRO BUILDER — PRUEBA REAL DE SERVICIO ACTIVO 368EF " +
            "SUPERADA\n\n" +
            "- Guardado aceptado con servicio Open y comanda real Preparing.\n" +
            "- Carga aceptada mientras el servicio seguía activo.\n" +
            "- Grupo, plaza de barra, camarero y posiciones reconstruidos.\n" +
            "- Comanda legacy y canónica conservan sus identidades y estado.\n" +
            "- Cocina reanuda la misma línea con tiempo restante.\n" +
            "- Reserva consumida, balances y disponibilidad restaurados.\n" +
            "- Sesión WaitingAtBar y siguientes IDs reconstruidos.\n" +
            "- Reserva temporal de mesa restaurada sin reasignarla.\n" +
            "- Servicio gastronómico activo restaurado sin reinterpretar la carta.\n" +
            "- Pausa, velocidad y Time.timeScale restaurados por GameClock.\n" +
            "- Próxima llegada, modalidad y tiempo restante conservados.\n" +
            "- Mutaciones posteriores al guardado fueron descartadas.\n" +
            "- Slot diagnóstico eliminado.\n\n" +
            "Sal ahora de Play Mode para descartar los ajustes de prueba.";
        UnsubscribeFromSaveService();
        Debug.Log(report);
        Repaint();
    }

    private void FailAndCleanup(string message)
    {
        pendingFailure = string.IsNullOrWhiteSpace(message)
            ? "Fallo funcional no especificado."
            : message;

        if (saveService != null && !saveService.IsBusy &&
            diagnosticSlot >= 1 && saveService.SlotExists(diagnosticSlot))
        {
            phase = TestPhase.DeletingAfterFailure;
            reportType = MessageType.Error;
            report =
                "La prueba ha fallado y elimina el slot diagnóstico...\n\n" +
                pendingFailure;
            Repaint();

            if (saveService.TryDeleteSlot(
                    diagnosticSlot,
                    out string rejection
                ))
            {
                return;
            }

            pendingFailure +=
                " No se pudo eliminar el slot: " + rejection;
        }

        CompleteFailureAfterDelete();
    }

    private void CompleteFailureAfterDelete()
    {
        phase = TestPhase.Failed;
        reportType = MessageType.Error;
        report =
            "PRUEBA REAL DE SERVICIO ACTIVO 368EF FALLIDA\n\n" +
            pendingFailure +
            "\n\nSal de Play Mode para restaurar la escena.";
        UnsubscribeFromSaveService();
        Debug.LogError(report);
        Repaint();
    }

    private CustomerGroup FindDiagnosticGroup()
    {
        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.InstanceID
        );
        for (int index = 0; index < groups.Length; index++)
        {
            CustomerGroup group = groups[index];
            if (group != null &&
                group.RequestedServiceMode ==
                    BistroBuilderServiceMode.WaitingAtBar)
            {
                return group;
            }
        }

        return null;
    }

    private static CustomerGroup FindGroupById(int groupId)
    {
        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID
        );
        for (int index = 0; index < groups.Length; index++)
        {
            if (groups[index] != null && groups[index].GroupId == groupId)
            {
                return groups[index];
            }
        }

        return null;
    }

    private static Waiter FindWaiterById(int waiterId)
    {
        Waiter[] waiters = FindObjectsByType<Waiter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID
        );
        for (int index = 0; index < waiters.Length; index++)
        {
            if (waiters[index] != null &&
                waiters[index].WaiterId == waiterId)
            {
                return waiters[index];
            }
        }

        return null;
    }

    private RestaurantOrder FindOrderByCanonicalId(string canonicalOrderId)
    {
        if (orderSystem == null)
        {
            return null;
        }

        for (int index = 0; index < orderSystem.ActiveOrders.Count; index++)
        {
            RestaurantOrder order = orderSystem.ActiveOrders[index];
            if (order != null &&
                string.Equals(
                    order.CanonicalOrderId,
                    canonicalOrderId,
                    StringComparison.Ordinal
                ))
            {
                return order;
            }
        }

        return null;
    }

    private static bool TryFindLine(
        BistroBuilderCanonicalOrder order,
        string lineId,
        out BistroBuilderCanonicalOrderLine line
    )
    {
        line = null;
        if (order == null)
        {
            return false;
        }

        for (int index = 0; index < order.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine candidate = order.Lines[index];
            if (candidate != null &&
                string.Equals(
                    candidate.LineId,
                    lineId,
                    StringComparison.Ordinal
                ))
            {
                line = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool AreStockSnapshotsEquivalent(
        BistroBuilderInventoryStockSnapshot left,
        BistroBuilderInventoryStockSnapshot right
    )
    {
        return string.Equals(
                   left.IngredientId,
                   right.IngredientId,
                   StringComparison.Ordinal
               ) &&
               string.Equals(
                   left.StorageLocationId,
                   right.StorageLocationId,
                   StringComparison.Ordinal
               ) &&
               left.OnHandCanonicalMilliUnits ==
                   right.OnHandCanonicalMilliUnits &&
               left.ReservedCanonicalMilliUnits ==
                   right.ReservedCanonicalMilliUnits &&
               left.ConsumedCanonicalMilliUnits ==
                   right.ConsumedCanonicalMilliUnits &&
               left.WastedCanonicalMilliUnits ==
                   right.WastedCanonicalMilliUnits &&
               left.Revision == right.Revision;
    }

    private static bool AreAvailabilitySnapshotsEquivalent(
        BistroBuilderDishAvailabilitySnapshot left,
        BistroBuilderDishAvailabilitySnapshot right
    )
    {
        return string.Equals(
                   left.DishId,
                   right.DishId,
                   StringComparison.Ordinal
               ) &&
               left.State == right.State &&
               left.AvailablePortions == right.AvailablePortions &&
               string.Equals(
                   left.LimitingIngredientId,
                   right.LimitingIngredientId,
                   StringComparison.Ordinal
               ) &&
               left.LimitingIngredientAvailableCanonicalMilliUnits ==
                   right.LimitingIngredientAvailableCanonicalMilliUnits &&
               left.LimitingIngredientRequiredCanonicalMilliUnits ==
                   right.LimitingIngredientRequiredCanonicalMilliUnits;
    }

    private static bool AreSpawnerStatesEquivalent(
        BistroBuilderCustomerSpawnerRuntimeSaveRecord expected,
        BistroBuilderCustomerSpawnerRuntimeSaveRecord actual
    )
    {
        if (expected == null || actual == null ||
            expected.scheduleInitialized != actual.scheduleInitialized ||
            expected.scheduleCompleted != actual.scheduleCompleted ||
            expected.pendingArrivals == null ||
            actual.pendingArrivals == null ||
            expected.pendingArrivals.Count != actual.pendingArrivals.Count)
        {
            return false;
        }

        for (int index = 0; index < expected.pendingArrivals.Count; index++)
        {
            BistroBuilderCustomerArrivalPlanSaveRecord left =
                expected.pendingArrivals[index];
            BistroBuilderCustomerArrivalPlanSaveRecord right =
                actual.pendingArrivals[index];

            if (left == null || right == null ||
                left.groupSize != right.groupSize ||
                left.serviceMode != right.serviceMode)
            {
                return false;
            }
        }

        if (expected.scheduleCompleted)
        {
            return Mathf.Approximately(
                actual.secondsUntilNextArrival,
                0f
            );
        }

        return actual.secondsUntilNextArrival > 0f &&
               actual.secondsUntilNextArrival <=
                   expected.secondsUntilNextArrival + 0.25f &&
               actual.secondsUntilNextArrival >=
                   Mathf.Max(
                       0f,
                       expected.secondsUntilNextArrival - 2f
                   );
    }

    private static string BuildVerificationFailure(
        bool service,
        bool mealService,
        bool group,
        bool waiter,
        bool order,
        bool canonical,
        bool lifecycle,
        bool reservation,
        bool stock,
        bool availability,
        bool kitchen,
        bool bar,
        bool pendingTable,
        bool clock,
        bool arrivals,
        bool sequences,
        string error
    )
    {
        return
            "La carga activa no reconstruyó todas las invariantes. " +
            "Servicio=" + service +
            ", ServicioGastronomico=" + mealService +
            ", Grupo=" + group +
            ", Camarero=" + waiter +
            ", Order=" + order +
            ", Canonical=" + canonical +
            ", Lifecycle=" + lifecycle +
            ", Reserva=" + reservation +
            ", Stock=" + stock +
            ", Disponibilidad=" + availability +
            ", Cocina=" + kitchen +
            ", Barra=" + bar +
            ", MesaReservada=" + pendingTable +
            ", Reloj=" + clock +
            ", Llegadas=" + arrivals +
            ", Secuencias=" + sequences + ". " +
            (error ?? string.Empty);
    }

    private bool ResolveDependencies(out string error)
    {
        saveService = FindFirstObjectByType<BistroBuilderSaveGameService>();
        serviceStateService =
            FindFirstObjectByType<RestaurantServiceStateService>();
        spawner = FindFirstObjectByType<CustomerGroupSpawner>();
        tableAssignmentSystem =
            FindFirstObjectByType<TableAssignmentSystem>();
        tableRegistry = FindFirstObjectByType<RestaurantTableRegistry>();
        barServiceSystem =
            FindFirstObjectByType<BistroBuilderBarServiceSystem>();
        orderSystem = FindFirstObjectByType<OrderSystem>();
        canonicalOrderService =
            FindFirstObjectByType<BistroBuilderCanonicalOrderService>();
        orderIntegration =
            FindFirstObjectByType<
                BistroBuilderCanonicalOrderIntegrationService
            >();
        lifecycleService =
            FindFirstObjectByType<
                BistroBuilderOrderInventoryLifecycleService
            >();
        inventoryService =
            FindFirstObjectByType<BistroBuilderInventoryService>();
        availabilityService =
            FindFirstObjectByType<BistroBuilderDishAvailabilityService>();
        kitchen = FindFirstObjectByType<KitchenSystem>();
        gameClock = FindFirstObjectByType<GameClock>();

        if (saveService == null || serviceStateService == null ||
            spawner == null || tableAssignmentSystem == null ||
            tableRegistry == null || barServiceSystem == null ||
            orderSystem == null || canonicalOrderService == null ||
            orderIntegration == null || lifecycleService == null ||
            inventoryService == null ||
            availabilityService == null || kitchen == null ||
            gameClock == null)
        {
            error = "Faltan dependencias runtime para la prueba real 368EF.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private int FindFreeDiagnosticSlot()
    {
        for (int slot = 989; slot >= 980; slot--)
        {
            if (!saveService.SlotExists(slot))
            {
                return slot;
            }
        }

        return 0;
    }

    private void SubscribeToSaveService()
    {
        UnsubscribeFromSaveService();
        saveService.OperationCompleted += HandleOperationCompleted;
        subscribed = true;
    }

    private void UnsubscribeFromSaveService()
    {
        if (subscribed && saveService != null)
        {
            saveService.OperationCompleted -= HandleOperationCompleted;
        }

        subscribed = false;
    }

    private void ResetRunState()
    {
        UnsubscribeFromSaveService();
        phase = TestPhase.Idle;
        reportType = MessageType.Info;
        pendingFailure = string.Empty;
        diagnosticSlot = 0;
        runToken = string.Empty;
        savedGroupId = 0;
        savedBarSpotId = string.Empty;
        savedPendingTableId = 0;
        tableToRelease = null;
        tableReleased = false;
        savedWaiterId = 0;
        savedLegacyOrderId = 0;
        savedCanonicalOrderId = string.Empty;
        savedLineId = string.Empty;
        savedDishId = string.Empty;
        savedReservationId = string.Empty;
        savedIngredientId = string.Empty;
        savedMealService = BistroBuilderMealServiceAvailability.Lunch;
        savedSpawnerState = null;
    }

    private void SetImmediateFailure(string message)
    {
        phase = TestPhase.Failed;
        reportType = MessageType.Error;
        report =
            "PRUEBA REAL DE SERVICIO ACTIVO 368EF FALLIDA\n\n" + message;
        Debug.LogError(report);
        Repaint();
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName
    )
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad serializada " + propertyName + "."
            );
        }

        return property;
    }
}
