using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sección service.runtime 368EF.
///
/// Convierte un servicio abierto en un checkpoint coherente. Los estados
/// transitorios de reparto se normalizan a ReadyForPickup para reiniciar la
/// ruta después de cargar sin duplicar reservas, consumo ni entregas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Active Service Runtime Save Provider")]
public sealed class BistroBuilderActiveServiceSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "service.runtime";
    public const int StableSectionVersion = 1;

    [Header("Persistencia")]
    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [SerializeField]
    private RestaurantServiceStateService serviceStateService;

    [Header("Clientes, sala y barra")]
    [SerializeField]
    private CustomerGroupSpawner customerGroupSpawner;

    [SerializeField]
    private RestaurantTableRegistry tableRegistry;

    [SerializeField]
    private TableAssignmentSystem tableAssignmentSystem;

    [SerializeField]
    private BistroBuilderBarServiceRegistry barRegistry;

    [SerializeField]
    private BistroBuilderBarServiceSystem barServiceSystem;

    [Header("Comandas y cocina")]
    [SerializeField]
    private OrderSystem orderSystem;

    [SerializeField]
    private BistroBuilderCanonicalOrderService canonicalOrderService;

    [SerializeField]
    private BistroBuilderCanonicalOrderIntegrationService orderIntegration;

    [SerializeField]
    private BistroBuilderCourseAndSharingService courseAndSharingService;

    [SerializeField]
    private BistroBuilderCustomerDiningService customerDiningService;

    [SerializeField]
    private WaiterTaskCoordinator waiterTaskCoordinator;

    [SerializeField]
    private BistroBuilderOrderInventoryLifecycleService
        orderInventoryLifecycleService;

    [Header("Depuración")]
    [SerializeField]
    private bool logLoadSummary = true;

    private readonly Dictionary<int, CustomerGroup> groupsById =
        new Dictionary<int, CustomerGroup>();
    private readonly Dictionary<int, Waiter> waitersById =
        new Dictionary<int, Waiter>();
    private readonly Dictionary<string, RestaurantOrder> ordersByCanonicalId =
        new Dictionary<string, RestaurantOrder>(StringComparer.Ordinal);
    private readonly List<BistroBuilderBarServiceSpot> occupiedSpotBuffer =
        new List<BistroBuilderBarServiceSpot>(8);

    private BistroBuilderActiveServiceSaveData pendingData;

    public string SectionId => StableSectionId;
    public int SectionVersion => StableSectionVersion;
    public int LoadOrder => 500;
    public bool IsRequired => false;
    public Type StateType => typeof(BistroBuilderActiveServiceSaveData);
    public string SerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    public int PrepareOrder => 9000;
    public int ApplyOrder => 500;
    public int FinalizeOrder => 11000;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnDisable()
    {
        BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring = false;
    }

    /// <summary>
    /// Detiene toda actividad transitoria de camareros antes de desmontar el
    /// mundo actual. Evita que corrutinas o llegadas diferidas anteriores al
    /// guardado actúen sobre las entidades reconstruidas durante la carga.
    /// </summary>
    private void ResetTransientWaiterRuntime()
    {
        WaiterTableServiceFlow[] tableFlows =
            FindObjectsByType<WaiterTableServiceFlow>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID
            );
        for (int index = 0; index < tableFlows.Length; index++)
        {
            tableFlows[index]?.ResetForRuntimeLoad();
        }

        FoodDeliveryServiceFlow[] deliveryFlows =
            FindObjectsByType<FoodDeliveryServiceFlow>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID
            );
        for (int index = 0; index < deliveryFlows.Length; index++)
        {
            deliveryFlows[index]?.ResetForRuntimeLoad();
        }

        BillServiceFlow[] billFlows = FindObjectsByType<BillServiceFlow>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID
        );
        for (int index = 0; index < billFlows.Length; index++)
        {
            billFlows[index]?.ResetForRuntimeLoad();
        }

        TableCleaningServiceFlow[] cleaningFlows =
            FindObjectsByType<TableCleaningServiceFlow>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID
            );
        for (int index = 0; index < cleaningFlows.Length; index++)
        {
            cleaningFlows[index]?.ResetForRuntimeLoad();
        }

        WaiterMovementView[] movementViews =
            FindObjectsByType<WaiterMovementView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID
            );
        for (int index = 0; index < movementViews.Length; index++)
        {
            movementViews[index]?.ResetForRuntimeLoad();
        }

        waiterTaskCoordinator?.ResetForRuntimeLoad();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (saveGameService == null)
        {
            error = "Falta BistroBuilderSaveGameService.";
            return false;
        }

        if (serviceStateService == null)
        {
            error = "Falta RestaurantServiceStateService.";
            return false;
        }

        if (customerGroupSpawner == null || tableRegistry == null ||
            tableAssignmentSystem == null || barRegistry == null ||
            barServiceSystem == null)
        {
            error = "Faltan los sistemas de clientes, mesas o barra.";
            return false;
        }

        if (orderSystem == null || canonicalOrderService == null ||
            orderIntegration == null || courseAndSharingService == null ||
            customerDiningService == null ||
            waiterTaskCoordinator == null ||
            orderInventoryLifecycleService == null)
        {
            error = "Faltan los sistemas de comandas, consumo, inventario " +
                    "por línea o tareas.";
            return false;
        }

        if (!orderSystem.ValidateConfiguration(out error) ||
            !barRegistry.ValidateConfiguration(out error) ||
            !barServiceSystem.ValidateConfiguration(out error) ||
            !orderInventoryLifecycleService.ValidateConfiguration(out error))
        {
            return false;
        }

        return true;
    }

    public IEnumerator CaptureState(BistroBuilderSaveCaptureContext context)
    {
        if (!ValidateConfiguration(out string configurationError))
        {
            context.Fail(configurationError);
            yield break;
        }

        bool active = serviceStateService.IsServiceInProgress;
        var data = new BistroBuilderActiveServiceSaveData
        {
            wasActiveService = active,
            nextGroupId = customerGroupSpawner.NextGroupId,
            nextLegacyOrderId = orderSystem.NextOrderId,
            currentMealService = (int)orderIntegration.CurrentMealService
        };

        if (!active)
        {
            context.Complete(data);
            yield break;
        }

        if (!context.SharedData.TryGet(
                BistroBuilderGeneralGameSaveSectionProvider.SharedCheckpointKey,
                out string checkpointId
            ) ||
            !context.SharedData.TryGet(
                BistroBuilderGeneralGameSaveSectionProvider.SharedCapturedUtcKey,
                out string capturedUtc
            ))
        {
            context.Fail(
                "game.general no proporcionó el checkpoint del servicio activo."
            );
            yield break;
        }

        data.checkpointId = checkpointId;
        data.capturedUtc = capturedUtc;

        if (!customerGroupSpawner.TryCaptureRuntimeSpawnState(
                out data.customerSpawner,
                out string error
            ))
        {
            context.Fail(error);
            yield break;
        }

        if (serviceStateService.AcceptsNewCustomers &&
            !data.customerSpawner.scheduleInitialized)
        {
            context.Fail(
                "El servicio acepta clientes pero no conserva un calendario " +
                "de llegadas persistible."
            );
            yield break;
        }

        if (!CaptureCustomers(data, out error) ||
            !CaptureTables(data, out error) ||
            !CapturePendingBarTableReservations(data, out error) ||
            !CaptureWaiters(data, out error) ||
            !CaptureOrdersAndSubsystems(data, out error))
        {
            context.Fail(error);
            yield break;
        }

        if (!data.TryValidate(out error))
        {
            context.Fail(error);
            yield break;
        }

        context.Complete(data);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderActiveServiceSaveData data))
        {
            error = "service.runtime no tiene el tipo esperado.";
            return false;
        }

        return data.TryValidate(out error);
    }

    public IEnumerator PrepareForLoad(BistroBuilderSaveLoadContext context)
    {
        pendingData = null;
        BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring = true;

        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
            yield break;
        }

        customerGroupSpawner.StopForRuntimeLoad();
        ResetTransientWaiterRuntime();
        tableAssignmentSystem.ClearPendingBarTransitionReservationsForRuntimeLoad();
        barServiceSystem.ClearRuntimeForLoad();
        courseAndSharingService.ClearRuntimeForLoad();
        customerDiningService.ClearRuntimeForLoad();
        orderInventoryLifecycleService.ClearRuntimeForLoad();
        orderSystem.ClearRuntimeForLoad();

        Waiter[] currentWaiters = FindObjectsByType<Waiter>(
            FindObjectsSortMode.InstanceID
        );
        for (int index = 0; index < currentWaiters.Length; index++)
        {
            currentWaiters[index]?.ClearAssignment();
        }

        CustomerGroup[] currentGroups = FindObjectsByType<CustomerGroup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID
        );
        for (int index = 0; index < currentGroups.Length; index++)
        {
            customerGroupSpawner.UnregisterAndDestroyGroupForRuntimeLoad(
                currentGroups[index]
            );
        }

        foreach (RestaurantTable table in tableRegistry.RegisteredTables)
        {
            table?.ClearRuntimeStateForLoad();
        }

        // Destroy se materializa al final del frame. Esperar evita que los
        // objetos antiguos convivan con las identidades restauradas.
        yield return null;
    }

    public IEnumerator ApplyState(
        object state,
        BistroBuilderSaveLoadContext context
    )
    {
        if (!ValidateState(state, out string validationError))
        {
            context.Fail(validationError);
            yield break;
        }

        pendingData = (BistroBuilderActiveServiceSaveData)state;

        if (!context.SharedData.TryGet(
                BistroBuilderGeneralGameSaveSectionProvider.SharedStateKey,
                out BistroBuilderGeneralGameSaveData generalState
            ) ||
            generalState == null)
        {
            context.Fail(
                "service.runtime no puede restaurarse sin game.general."
            );
            yield break;
        }

        BistroBuilderSaveSnapshotMode generalSnapshotMode =
            (BistroBuilderSaveSnapshotMode)generalState.snapshotMode;

        bool generalWasActive =
            generalSnapshotMode == BistroBuilderSaveSnapshotMode.ActiveService;

        if (generalWasActive != pendingData.wasActiveService)
        {
            context.Fail(
                "game.general y service.runtime declaran modos de snapshot " +
                "incompatibles."
            );
            yield break;
        }

        BistroBuilderMealServiceAvailability generalMealService =
            (BistroBuilderMealServiceAvailability)
                generalState.currentMealService;
        if (generalMealService ==
            BistroBuilderMealServiceAvailability.None)
        {
            generalMealService =
                BistroBuilderMealServiceAvailability.Lunch;
        }

        if (pendingData.wasActiveService &&
            (int)generalMealService != pendingData.currentMealService)
        {
            context.Fail(
                "game.general y service.runtime declaran servicios " +
                "gastronómicos distintos."
            );
            yield break;
        }

        if (pendingData.wasActiveService &&
            (!string.Equals(
                 generalState.activeServiceCheckpointId,
                 pendingData.checkpointId,
                 StringComparison.Ordinal
             ) ||
             !string.Equals(
                 generalState.capturedUtc,
                 pendingData.capturedUtc,
                 StringComparison.Ordinal
             )))
        {
            context.Fail(
                "game.general y service.runtime no pertenecen al mismo " +
                "checkpoint activo."
            );
            yield break;
        }

        if ((RestaurantServiceState)generalState.serviceState ==
                RestaurantServiceState.Open &&
            !pendingData.customerSpawner.scheduleInitialized)
        {
            context.Fail(
                "La partida declara un servicio Open sin calendario de " +
                "llegadas persistido."
            );
            yield break;
        }

        context.SharedData.Set(
            "save.loaded_section." + StableSectionId,
            true
        );
        groupsById.Clear();
        waitersById.Clear();
        ordersByCanonicalId.Clear();

        if (!pendingData.wasActiveService)
        {
            customerGroupSpawner.RestoreNextGroupId(pendingData.nextGroupId);

            if (!orderSystem.TryRestoreNextOrderId(
                    pendingData.nextLegacyOrderId
                ))
            {
                context.Fail(
                    "No se pudo restaurar la siguiente identidad de comanda."
                );
                yield break;
            }

            if (!customerGroupSpawner.TryRestoreRuntimeSpawnState(
                    pendingData.customerSpawner,
                    out string closedSpawnerError
                ))
            {
                context.Fail(closedSpawnerError);
            }
            yield break;
        }

        if (!orderIntegration.TrySetCurrentMealService(
                (BistroBuilderMealServiceAvailability)
                    pendingData.currentMealService,
                out string mealServiceError
            ))
        {
            context.Fail(mealServiceError);
            yield break;
        }

        if (!canonicalOrderService.TryReplaceFromRuntimeSnapshot(
                pendingData.canonicalOrders,
                true,
                out string error
            ))
        {
            context.Fail(error);
            yield break;
        }

        for (int index = 0; index < pendingData.groups.Count; index++)
        {
            if (!customerGroupSpawner.TryCreateRestoredGroup(
                    pendingData.groups[index],
                    out CustomerGroup group,
                    out error
                ))
            {
                context.Fail(error);
                yield break;
            }

            groupsById.Add(group.GroupId, group);

            if ((index + 1) % context.ObjectsPerFrame == 0)
            {
                yield return null;
            }
        }

        BuildWaiterIndexAndRestoreTransforms(pendingData, context, out error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            context.Fail(error);
            yield break;
        }

        if (!RestoreAssignmentsAndStates(pendingData, out error))
        {
            context.Fail(error);
            yield break;
        }

        if (!orderSystem.TryRestoreRuntimeOrders(
                pendingData.legacyOrders,
                groupsById,
                tableRegistry,
                barRegistry,
                waitersById,
                pendingData.nextLegacyOrderId,
                ordersByCanonicalId,
                out error
            ))
        {
            context.Fail(error);
            yield break;
        }

        if (!orderInventoryLifecycleService.TryRestoreSessionsFromOrders(
                out error
            ) ||
            !courseAndSharingService.TryReplaceFromRuntimeSnapshot(
                pendingData.coursesAndSharing,
                out error
            ) ||
            !customerDiningService.TryReplaceFromRuntimeSnapshot(
                pendingData.customerDining,
                true,
                out error
            ))
        {
            context.Fail(error);
            yield break;
        }

        KitchenSystem[] kitchens = FindObjectsByType<KitchenSystem>(
            FindObjectsSortMode.InstanceID
        );
        var kitchensById = new Dictionary<string, KitchenSystem>(
            StringComparer.Ordinal
        );
        for (int index = 0; index < kitchens.Length; index++)
        {
            if (kitchens[index] != null)
            {
                kitchensById[kitchens[index].KitchenId] = kitchens[index];
            }
        }

        for (int index = 0; index < pendingData.kitchens.Count; index++)
        {
            BistroBuilderKitchenRuntimeSnapshot snapshot =
                pendingData.kitchens[index];

            if (!kitchensById.TryGetValue(
                    snapshot.kitchenId,
                    out KitchenSystem kitchen
                ) || kitchen == null ||
                !kitchen.TryReplaceFromRuntimeSnapshot(
                    snapshot,
                    ordersByCanonicalId,
                    out error
                ))
            {
                context.Fail(
                    string.IsNullOrWhiteSpace(error)
                        ? "No se pudo restaurar una cocina."
                        : error
                );
                yield break;
            }
        }

        if (!barServiceSystem.TryRestoreRuntimeSaveRecords(
                pendingData.barSessions,
                pendingData.transferredBarCharges,
                groupsById,
                ordersByCanonicalId,
                out error
            ) ||
            !orderSystem.TryResumeCreatedOrders(out error) ||
            !waiterTaskCoordinator.RebuildTasksAfterRuntimeLoad(
                orderSystem,
                out error
            ))
        {
            context.Fail(error);
            yield break;
        }

        customerGroupSpawner.RestoreNextGroupId(pendingData.nextGroupId);
        if (!customerGroupSpawner.TryRestoreRuntimeSpawnState(
                pendingData.customerSpawner,
                out error
            ))
        {
            context.Fail(error);
        }
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed)
        {
            BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring = false;
            return;
        }

        if (pendingData == null &&
            context.SharedData.TryGet(
                BistroBuilderGeneralGameSaveSectionProvider.SharedStateKey,
                out BistroBuilderGeneralGameSaveData generalState
            ) &&
            generalState != null &&
            (BistroBuilderSaveSnapshotMode)generalState.snapshotMode ==
                BistroBuilderSaveSnapshotMode.ActiveService)
        {
            BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring = false;
            context.Fail(
                "La partida declara un servicio activo, pero no contiene " +
                "la sección service.runtime."
            );
            return;
        }

        if (pendingData != null && pendingData.wasActiveService)
        {
            // El proveedor general finaliza antes (orden 10000) y ya ha
            // restaurado Open/Preparing/Closing, reloj y velocidad. A partir
            // de aquí los eventos pueden volver a activar flujos runtime.
            BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring = false;

            foreach (RestaurantTable table in tableRegistry.RegisteredTables)
            {
                table?.NotifyRestoredRuntimeState();
            }

            foreach (CustomerGroup group in groupsById.Values)
            {
                group?.NotifyRestoredRuntimeState();
            }

            barServiceSystem.ResumeRestoredRuntime();
            waiterTaskCoordinator.ResumeAfterRuntimeLoad();
            customerGroupSpawner.ResumeAfterRuntimeLoad();

            if (logLoadSummary)
            {
                Debug.Log(
                    "368EF service.runtime restaurado: " +
                    groupsById.Count + " grupo(s), " +
                    ordersByCanonicalId.Count + " comanda(s) y " +
                    pendingData.kitchens.Count + " cocina(s).",
                    this
                );
            }
        }
        else
        {
            BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring = false;
        }

        pendingData = null;
    }

    private bool CaptureCustomers(
        BistroBuilderActiveServiceSaveData data,
        out string error
    )
    {
        error = string.Empty;
        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(
            FindObjectsSortMode.InstanceID
        );
        Array.Sort(groups, (left, right) =>
            left.GroupId.CompareTo(right.GroupId));

        for (int index = 0; index < groups.Length; index++)
        {
            CustomerGroup group = groups[index];
            if (group == null || group.CurrentState == CustomerGroupState.Finished)
            {
                continue;
            }

            occupiedSpotBuffer.Clear();
            barRegistry.GetOccupiedSpots(group, occupiedSpotBuffer);

            var record = new BistroBuilderCustomerGroupSaveRecord
            {
                groupId = group.GroupId,
                groupSize = group.GroupSize,
                state = (int)NormalizeGroupStateForCheckpoint(group),
                requestedServiceMode = (int)group.RequestedServiceMode,
                currentServiceMode = (int)group.CurrentServiceMode,
                waitingTime = group.WaitingTime,
                assignedTableId = group.AssignedTable != null
                    ? group.AssignedTable.TableId
                    : 0,
                anchorBarSpotId = group.AssignedBarSpot != null
                    ? group.AssignedBarSpot.BarSpotId
                    : string.Empty,
                worldPosition = new BistroBuilderSaveVector3(
                    group.transform.position
                ),
                worldRotation = new BistroBuilderSaveQuaternion(
                    group.transform.rotation
                )
            };

            for (int spotIndex = 0;
                 spotIndex < occupiedSpotBuffer.Count;
                 spotIndex++)
            {
                record.occupiedBarSpotIds.Add(
                    occupiedSpotBuffer[spotIndex].BarSpotId
                );
            }

            data.groups.Add(record);
        }

        return true;
    }

    private bool CaptureTables(
        BistroBuilderActiveServiceSaveData data,
        out string error
    )
    {
        error = string.Empty;
        var tables = new List<RestaurantTable>();
        foreach (RestaurantTable table in tableRegistry.RegisteredTables)
        {
            if (table != null)
            {
                tables.Add(table);
            }
        }
        tables.Sort((left, right) => left.TableId.CompareTo(right.TableId));

        for (int index = 0; index < tables.Count; index++)
        {
            RestaurantTable table = tables[index];
            var record = new BistroBuilderTableRuntimeSaveRecord
            {
                tableId = table.TableId,
                state = (int)NormalizeTableStateForCheckpoint(table),
                groupId = table.AssignedCustomerGroup != null
                    ? table.AssignedCustomerGroup.GroupId
                    : 0
            };

            if (!record.TryValidate(out error))
            {
                error = "No puede capturarse la mesa " + table.TableId +
                        " en service.runtime: " + error;
                return false;
            }

            data.tables.Add(record);
        }

        return true;
    }

    private bool CapturePendingBarTableReservations(
        BistroBuilderActiveServiceSaveData data,
        out string error
    )
    {
        error = string.Empty;

        if (data == null || tableAssignmentSystem == null)
        {
            error = "No puede capturarse la reserva de transición a mesa.";
            return false;
        }

        var groups = new List<CustomerGroup>(
            tableAssignmentSystem.RegisteredGroups
        );
        groups.Sort((left, right) =>
            (left != null ? left.GroupId : int.MaxValue).CompareTo(
                right != null ? right.GroupId : int.MaxValue
            )
        );

        var tableIds = new HashSet<int>();
        for (int index = 0; index < groups.Count; index++)
        {
            CustomerGroup group = groups[index];
            if (group == null ||
                !tableAssignmentSystem.TryGetPendingBarTransitionTable(
                    group,
                    out RestaurantTable table
                ))
            {
                continue;
            }

            if (table == null || !tableIds.Add(table.TableId))
            {
                error = "Existe una reserva WaitingAtBar sin mesa válida o " +
                        "con una mesa reservada dos veces.";
                return false;
            }

            data.pendingBarTableReservations.Add(
                new BistroBuilderPendingBarTableReservationSaveRecord
                {
                    groupId = group.GroupId,
                    tableId = table.TableId
                }
            );
        }

        return true;
    }

    private bool CaptureWaiters(
        BistroBuilderActiveServiceSaveData data,
        out string error
    )
    {
        error = string.Empty;
        Waiter[] waiters = FindObjectsByType<Waiter>(
            FindObjectsSortMode.InstanceID
        );
        Array.Sort(waiters, (left, right) =>
            left.WaiterId.CompareTo(right.WaiterId));

        var ids = new HashSet<int>();
        for (int index = 0; index < waiters.Length; index++)
        {
            Waiter waiter = waiters[index];
            if (waiter == null || !ids.Add(waiter.WaiterId))
            {
                error = "Los camareros no conservan WaiterId únicos.";
                return false;
            }

            data.waiters.Add(new BistroBuilderWaiterRuntimeSaveRecord
            {
                waiterId = waiter.WaiterId,
                worldPosition = new BistroBuilderSaveVector3(
                    waiter.transform.position
                ),
                worldRotation = new BistroBuilderSaveQuaternion(
                    waiter.transform.rotation
                )
            });
        }

        return true;
    }

    private bool CaptureOrdersAndSubsystems(
        BistroBuilderActiveServiceSaveData data,
        out string error
    )
    {
        error = string.Empty;

        if (!canonicalOrderService.TryCaptureRuntimeSnapshot(
                out BistroBuilderCanonicalOrderRuntimeSnapshot canonical,
                out error
            ))
        {
            return false;
        }

        data.canonicalOrders =
            canonical.CreateActiveServiceCheckpointSnapshot();

        IReadOnlyList<RestaurantOrder> orders = orderSystem.ActiveOrders;
        for (int index = 0; index < orders.Count; index++)
        {
            RestaurantOrder order = orders[index];
            if (order == null || order.IsFinished)
            {
                continue;
            }

            if (order.CustomerGroup == null || order.AssignedWaiter == null)
            {
                error = "La comanda " + order.OrderId +
                        " no conserva grupo o camarero para service.runtime.";
                return false;
            }

            data.legacyOrders.Add(new BistroBuilderLegacyOrderSaveRecord
            {
                legacyOrderId = order.OrderId,
                canonicalOrderId = order.CanonicalOrderId,
                serviceMode = (int)order.ServiceMode,
                tableId = order.Table != null ? order.Table.TableId : 0,
                barSpotId = order.BarSpot != null
                    ? order.BarSpot.BarSpotId
                    : string.Empty,
                groupId = order.CustomerGroup.GroupId,
                waiterId = order.AssignedWaiter.WaiterId,
                orderState = (int)NormalizeLegacyOrderStateForCheckpoint(order)
            });
        }

        if (!courseAndSharingService.TryCaptureRuntimeSnapshot(
                out data.coursesAndSharing,
                out error
            ) ||
            !customerDiningService.TryCaptureRuntimeSnapshot(
                out data.customerDining,
                out error
            ))
        {
            return false;
        }

        KitchenSystem[] kitchens = FindObjectsByType<KitchenSystem>(
            FindObjectsSortMode.InstanceID
        );
        Array.Sort(kitchens, (left, right) =>
            string.CompareOrdinal(left.KitchenId, right.KitchenId));
        for (int index = 0; index < kitchens.Length; index++)
        {
            if (!kitchens[index].TryCaptureRuntimeSnapshot(
                    out BistroBuilderKitchenRuntimeSnapshot snapshot,
                    out error
                ))
            {
                return false;
            }
            data.kitchens.Add(snapshot);
        }

        return barServiceSystem.TryCaptureRuntimeSaveRecords(
            data.barSessions,
            data.transferredBarCharges,
            out error
        );
    }

    private bool RestoreAssignmentsAndStates(
        BistroBuilderActiveServiceSaveData data,
        out string error
    )
    {
        error = string.Empty;

        for (int index = 0; index < data.groups.Count; index++)
        {
            BistroBuilderCustomerGroupSaveRecord record = data.groups[index];
            CustomerGroup group = groupsById[record.groupId];

            if (record.assignedTableId > 0)
            {
                if (!tableRegistry.TryGetTableById(
                        record.assignedTableId,
                        out RestaurantTable table
                    ) || table == null || !group.AssignTable(table))
                {
                    error = "No se pudo restaurar una asignación de mesa.";
                    return false;
                }
            }

            BistroBuilderServiceMode currentMode =
                (BistroBuilderServiceMode)record.currentServiceMode;
            if (BistroBuilderServiceModeUtility.IsBarMode(currentMode) &&
                !barRegistry.TryRestoreGroupAllocation(
                    group,
                    record.anchorBarSpotId,
                    record.occupiedBarSpotIds,
                    currentMode,
                    out error
                ))
            {
                return false;
            }

            if (!group.TryRestoreRuntimeState(
                    (CustomerGroupState)record.state,
                    currentMode,
                    false,
                    out error
                ))
            {
                return false;
            }
        }

        for (int index = 0; index < data.tables.Count; index++)
        {
            BistroBuilderTableRuntimeSaveRecord record = data.tables[index];
            if (!tableRegistry.TryGetTableById(
                    record.tableId,
                    out RestaurantTable table
                ) || table == null ||
                !table.TryRestoreRuntimeState(
                    (TableState)record.state,
                    false,
                    out error
                ))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "No se pudo restaurar una mesa.";
                }
                return false;
            }
        }

        for (int index = 0;
             index < data.pendingBarTableReservations.Count;
             index++)
        {
            BistroBuilderPendingBarTableReservationSaveRecord record =
                data.pendingBarTableReservations[index];

            if (!groupsById.TryGetValue(
                    record.groupId,
                    out CustomerGroup group
                ) || group == null ||
                !tableRegistry.TryGetTableById(
                    record.tableId,
                    out RestaurantTable table
                ) || table == null ||
                !tableAssignmentSystem
                    .TryRestorePendingBarTransitionReservation(
                        group,
                        table,
                        out error
                    ))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "No se pudo restaurar una reserva WaitingAtBar.";
                }
                return false;
            }
        }

        return true;
    }

    private void BuildWaiterIndexAndRestoreTransforms(
        BistroBuilderActiveServiceSaveData data,
        BistroBuilderSaveLoadContext context,
        out string error
    )
    {
        error = string.Empty;
        Waiter[] sceneWaiters = FindObjectsByType<Waiter>(
            FindObjectsSortMode.InstanceID
        );
        for (int index = 0; index < sceneWaiters.Length; index++)
        {
            Waiter waiter = sceneWaiters[index];
            if (waiter == null || waitersById.ContainsKey(waiter.WaiterId))
            {
                error = "La escena contiene WaiterId duplicados.";
                return;
            }
            waiter.ClearAssignment();
            waitersById.Add(waiter.WaiterId, waiter);
        }

        for (int index = 0; index < data.waiters.Count; index++)
        {
            BistroBuilderWaiterRuntimeSaveRecord record = data.waiters[index];
            if (!waitersById.TryGetValue(record.waiterId, out Waiter waiter))
            {
                error = "No existe el camarero " + record.waiterId + ".";
                return;
            }
            waiter.transform.SetPositionAndRotation(
                record.worldPosition.ToVector3(),
                record.worldRotation.ToQuaternion()
            );
        }
    }

    private CustomerGroupState NormalizeGroupStateForCheckpoint(
        CustomerGroup group
    )
    {
        switch (group.CurrentState)
        {
            case CustomerGroupState.Entering:
                return CustomerGroupState.WaitingForTable;
            case CustomerGroupState.Seated:
                return CustomerGroupState.WaitingForWaiter;
            case CustomerGroupState.Ordering:
                return group.AssignedTable != null &&
                       orderSystem.GetActiveOrderForTable(
                           group.AssignedTable
                       ) != null
                    ? CustomerGroupState.WaitingForFood
                    : CustomerGroupState.WaitingForWaiter;
            case CustomerGroupState.Paying:
                return CustomerGroupState.WaitingForBill;
            case CustomerGroupState.OrderingAtBar:
                return orderSystem.GetActiveOrderForBarSpot(
                    group.AssignedBarSpot
                ) != null
                    ? CustomerGroupState.WaitingForBarItems
                    : CustomerGroupState.WaitingForBarOrder;
            case CustomerGroupState.PayingAtBar:
                return CustomerGroupState.PayingAtBar;
            default:
                return group.CurrentState;
        }
    }

    private TableState NormalizeTableStateForCheckpoint(RestaurantTable table)
    {
        if (table.CurrentState == TableState.TakingOrder)
        {
            return orderSystem.GetActiveOrderForTable(table) != null
                ? TableState.WaitingForFood
                : TableState.WaitingForWaiter;
        }

        return table.CurrentState == TableState.Paying
            ? TableState.WaitingForBill
            : table.CurrentState;
    }

    private static OrderState NormalizeLegacyOrderStateForCheckpoint(
        RestaurantOrder order
    )
    {
        return order.CurrentState;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (saveGameService == null) TryGetComponent(out saveGameService);
        if (serviceStateService == null)
            TryGetComponent(out serviceStateService);
        if (customerGroupSpawner == null)
            TryGetComponent(out customerGroupSpawner);
        if (tableRegistry == null) TryGetComponent(out tableRegistry);
        if (tableAssignmentSystem == null)
            TryGetComponent(out tableAssignmentSystem);
        if (barRegistry == null) TryGetComponent(out barRegistry);
        if (barServiceSystem == null) TryGetComponent(out barServiceSystem);
        if (orderSystem == null) TryGetComponent(out orderSystem);
        if (canonicalOrderService == null)
            TryGetComponent(out canonicalOrderService);
        if (orderIntegration == null)
            TryGetComponent(out orderIntegration);
        if (courseAndSharingService == null)
            TryGetComponent(out courseAndSharingService);
        if (customerDiningService == null)
            TryGetComponent(out customerDiningService);
        if (waiterTaskCoordinator == null)
            TryGetComponent(out waiterTaskCoordinator);
        if (orderInventoryLifecycleService == null)
            TryGetComponent(out orderInventoryLifecycleService);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
