using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marca global de reconstrucción. Los componentes de prefab que arrancan
/// corrutinas en OnEnable deben permanecer quietos hasta que service.runtime
/// haya resuelto todas las referencias cruzadas.
/// </summary>
public static class BistroBuilderActiveServiceRuntimeLoadScope
{
    public static bool IsRestoring { get; internal set; }
}

[Serializable]
public sealed class BistroBuilderActiveServiceSaveData
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public bool wasActiveService;
    public string checkpointId = string.Empty;
    public string capturedUtc = string.Empty;
    public int nextGroupId = 1;
    public int nextLegacyOrderId = 1;
    public int currentMealService =
        (int)BistroBuilderMealServiceAvailability.Lunch;
    public BistroBuilderCustomerSpawnerRuntimeSaveRecord customerSpawner =
        new BistroBuilderCustomerSpawnerRuntimeSaveRecord();

    public List<BistroBuilderCustomerGroupSaveRecord> groups =
        new List<BistroBuilderCustomerGroupSaveRecord>();
    public List<BistroBuilderTableRuntimeSaveRecord> tables =
        new List<BistroBuilderTableRuntimeSaveRecord>();
    public List<BistroBuilderWaiterRuntimeSaveRecord> waiters =
        new List<BistroBuilderWaiterRuntimeSaveRecord>();
    public List<BistroBuilderLegacyOrderSaveRecord> legacyOrders =
        new List<BistroBuilderLegacyOrderSaveRecord>();
    public List<BistroBuilderBarSessionSaveRecord> barSessions =
        new List<BistroBuilderBarSessionSaveRecord>();
    public List<BistroBuilderPendingBarTableReservationSaveRecord>
        pendingBarTableReservations =
            new List<BistroBuilderPendingBarTableReservationSaveRecord>();
    public List<BistroBuilderTransferredBarChargeSaveRecord>
        transferredBarCharges =
            new List<BistroBuilderTransferredBarChargeSaveRecord>();

    public BistroBuilderCanonicalOrderRuntimeSnapshot canonicalOrders;
    public BistroBuilderCourseAndSharingRuntimeSnapshot coursesAndSharing;
    public BistroBuilderCustomerDiningRuntimeSnapshot customerDining;
    public List<BistroBuilderKitchenRuntimeSnapshot> kitchens =
        new List<BistroBuilderKitchenRuntimeSnapshot>();

    public bool TryValidate(out string error)
    {
        error = string.Empty;

        if (schemaVersion != CurrentSchemaVersion ||
            groups == null || tables == null || waiters == null ||
            legacyOrders == null || barSessions == null ||
            pendingBarTableReservations == null ||
            transferredBarCharges == null || kitchens == null ||
            customerSpawner == null || nextGroupId < 1 ||
            nextLegacyOrderId < 1)
        {
            error = "El snapshot service.runtime contiene datos básicos inválidos.";
            return false;
        }

        if (!customerSpawner.TryValidate(out error))
        {
            return false;
        }

        BistroBuilderMealServiceAvailability mealService =
            (BistroBuilderMealServiceAvailability)currentMealService;
        if (!IsConcreteMealService(mealService))
        {
            error = "service.runtime no conserva un servicio concreto.";
            return false;
        }

        if (!wasActiveService)
        {
            /*
             * JsonUtility puede reconstruir referencias de clases serializables
             * que eran null como cascarones vacíos con sus valores por defecto.
             * Esto no representa runtime real de servicio, pero antes hacía que
             * un guardado válido con el restaurante Closed pasase Save y fallase
             * inmediatamente al hacer Load.
             *
             * Seguimos rechazando cualquier dato operativo real: grupos, mesas,
             * comandas, cocina, revisiones o secuencias no iniciales. Solo se
             * toleran y normalizan los cascarones vacíos creados por el ciclo JSON.
             */
            bool hasConcreteRuntime =
                groups.Count != 0 || tables.Count != 0 || waiters.Count != 0 ||
                legacyOrders.Count != 0 || barSessions.Count != 0 ||
                pendingBarTableReservations.Count != 0 ||
                transferredBarCharges.Count != 0 || kitchens.Count != 0 ||
                customerSpawner.scheduleInitialized ||
                customerSpawner.scheduleCompleted ||
                customerSpawner.pendingArrivals.Count != 0 ||
                !IsClosedCanonicalOrdersShell(canonicalOrders) ||
                !IsClosedCoursesAndSharingShell(coursesAndSharing) ||
                !IsClosedCustomerDiningShell(customerDining);

            if (hasConcreteRuntime)
            {
                error = "Un snapshot de restaurante cerrado contiene runtime " +
                        "de servicio incompatible.";
                return false;
            }

            // Conserva una representación canónica y determinista en memoria.
            canonicalOrders = null;
            coursesAndSharing = null;
            customerDining = null;

            return true;
        }

        if (!BistroBuilderMenuIdUtility.IsValidStableId(checkpointId) ||
            !DateTime.TryParse(
                capturedUtc,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out _
            ) ||
            canonicalOrders == null || coursesAndSharing == null ||
            customerDining == null)
        {
            error = "El snapshot activo no contiene checkpoint o agregados válidos.";
            return false;
        }

        if (!canonicalOrders.TryValidate(out error) ||
            !coursesAndSharing.TryValidate(out error) ||
            !customerDining.TryValidate(out error))
        {
            return false;
        }

        var groupIds = new HashSet<int>();
        int maximumGroupId = 0;
        for (int index = 0; index < groups.Count; index++)
        {
            if (groups[index] == null ||
                !groups[index].TryValidate(out error) ||
                !groupIds.Add(groups[index].groupId))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "service.runtime repite un GroupId.";
                }
                return false;
            }

            maximumGroupId = Math.Max(
                maximumGroupId,
                groups[index].groupId
            );
        }

        if (nextGroupId <= maximumGroupId)
        {
            error = "La siguiente identidad de grupo no supera las ya usadas.";
            return false;
        }

        var tableIds = new HashSet<int>();
        for (int index = 0; index < tables.Count; index++)
        {
            if (tables[index] == null ||
                !tables[index].TryValidate(out error) ||
                !tableIds.Add(tables[index].tableId))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "service.runtime repite un TableId.";
                }
                return false;
            }
        }

        var waiterIds = new HashSet<int>();
        for (int index = 0; index < waiters.Count; index++)
        {
            if (waiters[index] == null ||
                !waiters[index].TryValidate(out error) ||
                !waiterIds.Add(waiters[index].waiterId))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "service.runtime repite un WaiterId.";
                }
                return false;
            }
        }

        var tableByGroupId = new Dictionary<int, int>();
        for (int index = 0; index < groups.Count; index++)
        {
            BistroBuilderCustomerGroupSaveRecord group = groups[index];
            if (group.assignedTableId > 0)
            {
                if (!tableIds.Contains(group.assignedTableId) ||
                    tableByGroupId.ContainsKey(group.groupId))
                {
                    error = "Un grupo referencia una mesa inexistente o " +
                            "duplicada.";
                    return false;
                }

                tableByGroupId.Add(group.groupId, group.assignedTableId);
            }
        }

        var groupsAssignedByTables = new HashSet<int>();
        for (int index = 0; index < tables.Count; index++)
        {
            BistroBuilderTableRuntimeSaveRecord table = tables[index];
            if (table.groupId <= 0)
            {
                continue;
            }

            if (!groupIds.Contains(table.groupId) ||
                !groupsAssignedByTables.Add(table.groupId) ||
                !tableByGroupId.TryGetValue(
                    table.groupId,
                    out int groupTableId
                ) ||
                groupTableId != table.tableId)
            {
                error = "Las referencias entre mesas y grupos no son " +
                        "bidireccionales.";
                return false;
            }
        }

        if (groupsAssignedByTables.Count != tableByGroupId.Count)
        {
            error = "Existe un grupo con mesa asignada que la mesa no " +
                    "reconoce de forma bidireccional.";
            return false;
        }

        var canonicalSnapshotIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < canonicalOrders.Orders.Count; index++)
        {
            canonicalSnapshotIds.Add(canonicalOrders.Orders[index].OrderId);
        }

        var legacyIds = new HashSet<int>();
        var canonicalIds = new HashSet<string>(StringComparer.Ordinal);
        int maximumLegacyOrderId = 0;
        for (int index = 0; index < legacyOrders.Count; index++)
        {
            BistroBuilderLegacyOrderSaveRecord order = legacyOrders[index];
            if (order == null || !order.TryValidate(out error) ||
                !legacyIds.Add(order.legacyOrderId) ||
                !canonicalIds.Add(order.canonicalOrderId) ||
                !canonicalSnapshotIds.Contains(order.canonicalOrderId) ||
                !groupIds.Contains(order.groupId) ||
                !waiterIds.Contains(order.waiterId))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "service.runtime contiene una comanda incoherente.";
                }
                return false;
            }

            maximumLegacyOrderId = Math.Max(
                maximumLegacyOrderId,
                order.legacyOrderId
            );
        }

        if (nextLegacyOrderId <= maximumLegacyOrderId)
        {
            error = "La siguiente identidad legacy no supera las ya usadas.";
            return false;
        }

        var kitchenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < kitchens.Count; index++)
        {
            BistroBuilderKitchenRuntimeSnapshot kitchen = kitchens[index];
            if (kitchen == null || !kitchen.TryValidate(out error) ||
                !kitchenIds.Add(kitchen.kitchenId))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "service.runtime repite un KitchenId.";
                }
                return false;
            }
        }

        var barSessionGroupIds = new HashSet<int>();
        for (int index = 0; index < barSessions.Count; index++)
        {
            BistroBuilderBarSessionSaveRecord session = barSessions[index];
            if (session == null || !session.TryValidate(out error) ||
                !groupIds.Contains(session.groupId) ||
                !barSessionGroupIds.Add(session.groupId) ||
                (!string.IsNullOrEmpty(session.canonicalOrderId) &&
                 !canonicalSnapshotIds.Contains(session.canonicalOrderId)))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "service.runtime contiene una sesión de barra " +
                            "duplicada o sin referencias válidas.";
                }
                return false;
            }
        }

        var pendingReservationGroups = new HashSet<int>();
        var pendingReservationTables = new HashSet<int>();
        var groupRecordsById = new Dictionary<int, BistroBuilderCustomerGroupSaveRecord>();
        for (int index = 0; index < groups.Count; index++)
        {
            groupRecordsById.Add(groups[index].groupId, groups[index]);
        }

        var tableRecordsById = new Dictionary<int, BistroBuilderTableRuntimeSaveRecord>();
        for (int index = 0; index < tables.Count; index++)
        {
            tableRecordsById.Add(tables[index].tableId, tables[index]);
        }

        var barSessionsByGroupId = new Dictionary<int, BistroBuilderBarSessionSaveRecord>();
        for (int index = 0; index < barSessions.Count; index++)
        {
            barSessionsByGroupId.Add(barSessions[index].groupId, barSessions[index]);
        }

        for (int index = 0; index < pendingBarTableReservations.Count; index++)
        {
            BistroBuilderPendingBarTableReservationSaveRecord reservation =
                pendingBarTableReservations[index];

            if (reservation == null || !reservation.TryValidate(out error) ||
                !pendingReservationGroups.Add(reservation.groupId) ||
                !pendingReservationTables.Add(reservation.tableId) ||
                !groupRecordsById.TryGetValue(
                    reservation.groupId,
                    out BistroBuilderCustomerGroupSaveRecord groupRecord
                ) ||
                !tableRecordsById.TryGetValue(
                    reservation.tableId,
                    out BistroBuilderTableRuntimeSaveRecord tableRecord
                ) ||
                !barSessionsByGroupId.TryGetValue(
                    reservation.groupId,
                    out BistroBuilderBarSessionSaveRecord barSession
                ))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "service.runtime contiene una reserva de transición " +
                            "a mesa duplicada o huérfana.";
                }
                return false;
            }

            if ((BistroBuilderServiceMode)groupRecord.currentServiceMode !=
                    BistroBuilderServiceMode.WaitingAtBar ||
                groupRecord.assignedTableId != 0 ||
                tableRecord.groupId != 0 ||
                (TableState)tableRecord.state != TableState.Free ||
                (BistroBuilderServiceMode)barSession.serviceMode !=
                    BistroBuilderServiceMode.WaitingAtBar ||
                !barSession.tableRequested)
            {
                error = "La reserva de transición a mesa no coincide con el " +
                        "grupo, la mesa libre o la sesión WaitingAtBar.";
                return false;
            }
        }

        var transferredChargeKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < transferredBarCharges.Count; index++)
        {
            BistroBuilderTransferredBarChargeSaveRecord charge =
                transferredBarCharges[index];
            if (charge == null || !charge.TryValidate(out error) ||
                !groupIds.Contains(charge.groupId) ||
                !transferredChargeKeys.Add(
                    charge.groupId + "|" + charge.sourceOrderId
                ))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "service.runtime contiene un cargo de barra " +
                            "duplicado o huérfano.";
                }
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Detecta el cascarón vacío que JsonUtility puede materializar para una
    /// referencia null de comandas al reconstruir un snapshot cerrado.
    /// </summary>
    private static bool IsClosedCanonicalOrdersShell(
        BistroBuilderCanonicalOrderRuntimeSnapshot snapshot
    )
    {
        if (snapshot == null)
        {
            return true;
        }

        IReadOnlyList<BistroBuilderCanonicalOrder> orders = snapshot.Orders;
        bool emptyOrders = orders == null || orders.Count == 0;
        bool defaultSchema =
            snapshot.SchemaVersion == 0 ||
            snapshot.SchemaVersion ==
                BistroBuilderCanonicalOrderRuntimeSnapshot.CurrentSchemaVersion;
        bool defaultSequence =
            snapshot.NextSequenceNumber == 0 ||
            snapshot.NextSequenceNumber == 1;

        return emptyOrders && defaultSchema && defaultSequence;
    }

    /// <summary>
    /// Equivalente para el runtime de pases y platos compartidos. Una revisión
    /// distinta de cero ya es estado real y por tanto no es válida en Closed.
    /// </summary>
    private static bool IsClosedCoursesAndSharingShell(
        BistroBuilderCourseAndSharingRuntimeSnapshot snapshot
    )
    {
        if (snapshot == null)
        {
            return true;
        }

        bool emptyOrders = snapshot.orders == null || snapshot.orders.Count == 0;
        bool defaultSchema =
            snapshot.schemaVersion == 0 || snapshot.schemaVersion == 1;

        return emptyOrders && defaultSchema && snapshot.revision == 0;
    }

    /// <summary>
    /// Equivalente para consumo individual. Solo se admite el objeto vacío
    /// materializado por serialización; cualquier orden conservada es runtime.
    /// </summary>
    private static bool IsClosedCustomerDiningShell(
        BistroBuilderCustomerDiningRuntimeSnapshot snapshot
    )
    {
        if (snapshot == null)
        {
            return true;
        }

        IReadOnlyList<BistroBuilderCustomerDiningOrderRuntime> orders =
            snapshot.Orders;
        bool emptyOrders = orders == null || orders.Count == 0;
        bool defaultSchema =
            snapshot.SchemaVersion == 0 ||
            snapshot.SchemaVersion ==
                BistroBuilderCustomerDiningRuntimeSnapshot.CurrentSchemaVersion;

        return emptyOrders && defaultSchema;
    }

    private static bool IsConcreteMealService(
        BistroBuilderMealServiceAvailability mealService
    )
    {
        return mealService == BistroBuilderMealServiceAvailability.Breakfast ||
               mealService == BistroBuilderMealServiceAvailability.Lunch ||
               mealService == BistroBuilderMealServiceAvailability.Dinner;
    }
}


[Serializable]
public sealed class BistroBuilderCustomerSpawnerRuntimeSaveRecord
{
    public bool scheduleInitialized;
    public bool scheduleCompleted;
    public float secondsUntilNextArrival;
    public List<BistroBuilderCustomerArrivalPlanSaveRecord> pendingArrivals =
        new List<BistroBuilderCustomerArrivalPlanSaveRecord>();

    public bool TryValidate(out string error)
    {
        if (pendingArrivals == null ||
            float.IsNaN(secondsUntilNextArrival) ||
            float.IsInfinity(secondsUntilNextArrival) ||
            secondsUntilNextArrival < 0f)
        {
            error = "service.runtime contiene un plan de llegadas inválido.";
            return false;
        }

        if (!scheduleInitialized)
        {
            if (scheduleCompleted || pendingArrivals.Count != 0 ||
                secondsUntilNextArrival > 0f)
            {
                error = "Un plan de llegadas no inicializado contiene estado.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        if (scheduleCompleted)
        {
            if (pendingArrivals.Count != 0 ||
                secondsUntilNextArrival > 0f)
            {
                error = "Un plan de llegadas completado conserva pendientes.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        if (pendingArrivals.Count == 0)
        {
            error = "El plan activo no contiene próximas llegadas.";
            return false;
        }

        for (int index = 0; index < pendingArrivals.Count; index++)
        {
            BistroBuilderCustomerArrivalPlanSaveRecord arrival =
                pendingArrivals[index];

            if (arrival == null)
            {
                error = "El plan contiene una llegada futura nula en la " +
                        "posición " + index + ".";
                return false;
            }

            if (!arrival.TryValidate(out error))
            {
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderCustomerArrivalPlanSaveRecord
{
    public int groupSize;
    public int serviceMode;
    public BistroBuilderCustomerAcquisitionProfile acquisition =
        BistroBuilderCustomerAcquisitionProfile.CreateBaseline();

    public bool TryValidate(out string error)
    {
        if (groupSize < 1 ||
            !BistroBuilderServiceModeUtility.IsDefined(
                (BistroBuilderServiceMode)serviceMode
            ))
        {
            error = "El plan contiene una llegada futura inválida.";
            return false;
        }

        acquisition ??= BistroBuilderCustomerAcquisitionProfile.CreateBaseline();
        if (!acquisition.TryValidate(out error))
            return false;

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderCustomerGroupSaveRecord
{
    public int groupId;
    public int groupSize;
    public int state;
    public int requestedServiceMode;
    public int currentServiceMode;
    public float waitingTime;
    public int assignedTableId;
    public string anchorBarSpotId = string.Empty;
    public List<string> occupiedBarSpotIds = new List<string>();
    public BistroBuilderSaveVector3 worldPosition;
    public BistroBuilderSaveQuaternion worldRotation;
    public BistroBuilderCustomerAcquisitionProfile acquisition =
        BistroBuilderCustomerAcquisitionProfile.CreateBaseline();

    public bool TryValidate(out string error)
    {
        anchorBarSpotId = BistroBuilderOrderIdUtility.Normalize(anchorBarSpotId);
        acquisition ??= BistroBuilderCustomerAcquisitionProfile.CreateBaseline();

        if (groupId < 1 || groupSize < 1 ||
            !Enum.IsDefined(typeof(CustomerGroupState), state) ||
            !BistroBuilderServiceModeUtility.IsDefined(
                (BistroBuilderServiceMode)requestedServiceMode
            ) ||
            !BistroBuilderServiceModeUtility.IsDefined(
                (BistroBuilderServiceMode)currentServiceMode
            ) ||
            float.IsNaN(waitingTime) || float.IsInfinity(waitingTime) ||
            waitingTime < 0f || assignedTableId < 0 ||
            occupiedBarSpotIds == null || !worldPosition.IsFinite() ||
            !worldRotation.IsFinite() || !worldRotation.HasUsableMagnitude())
        {
            error = "service.runtime contiene un grupo inválido.";
            return false;
        }

        bool barMode = BistroBuilderServiceModeUtility.IsBarMode(
            (BistroBuilderServiceMode)currentServiceMode
        );

        if (barMode &&
            (!BistroBuilderOrderIdUtility.IsValid(anchorBarSpotId) ||
             occupiedBarSpotIds.Count == 0 || assignedTableId != 0))
        {
            error = "Un grupo de barra no conserva un destino coherente.";
            return false;
        }

        if (!barMode &&
            (occupiedBarSpotIds.Count > 0 ||
             !string.IsNullOrEmpty(anchorBarSpotId)))
        {
            error = "Un grupo de mesa conserva plazas de barra incompatibles.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < occupiedBarSpotIds.Count; index++)
        {
            occupiedBarSpotIds[index] =
                BistroBuilderOrderIdUtility.Normalize(occupiedBarSpotIds[index]);
            if (!BistroBuilderOrderIdUtility.IsValid(occupiedBarSpotIds[index]) ||
                !ids.Add(occupiedBarSpotIds[index]))
            {
                error = "El grupo contiene una plaza de barra inválida o repetida.";
                return false;
            }
        }

        if (barMode && !ids.Contains(anchorBarSpotId))
        {
            error = "La plaza ancla no pertenece a la ocupación del grupo.";
            return false;
        }

        if (!acquisition.TryValidate(out error))
            return false;

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderTableRuntimeSaveRecord
{
    public int tableId;
    public int state;
    public int groupId;

    public bool TryValidate(out string error)
    {
        if (tableId < 1 || groupId < 0 ||
            !Enum.IsDefined(typeof(TableState), state))
        {
            error = "service.runtime contiene una mesa inválida.";
            return false;
        }

        TableState tableState = (TableState)state;
        bool stateAllowsNoGroup =
            tableState == TableState.Free ||
            tableState == TableState.Dirty;

        if (groupId == 0 && !stateAllowsNoGroup)
        {
            error = "Una mesa sin grupo solo puede persistirse como Free " +
                    "o Dirty.";
            return false;
        }

        if (groupId > 0 && stateAllowsNoGroup)
        {
            error = "Una mesa con grupo no puede persistirse como Free " +
                    "o Dirty.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderPendingBarTableReservationSaveRecord
{
    public int groupId;
    public int tableId;

    public bool TryValidate(out string error)
    {
        if (groupId < 1 || tableId < 1)
        {
            error = "service.runtime contiene una reserva de transición " +
                    "a mesa inválida.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderWaiterRuntimeSaveRecord
{
    public int waiterId;
    public BistroBuilderSaveVector3 worldPosition;
    public BistroBuilderSaveQuaternion worldRotation;

    public bool TryValidate(out string error)
    {
        if (waiterId < 1 || !worldPosition.IsFinite() ||
            !worldRotation.IsFinite() || !worldRotation.HasUsableMagnitude())
        {
            error = "service.runtime contiene un camarero inválido.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderLegacyOrderSaveRecord
{
    public int legacyOrderId;
    public string canonicalOrderId = string.Empty;
    public int serviceMode;
    public int tableId;
    public string barSpotId = string.Empty;
    public int groupId;
    public int waiterId;
    public int orderState;

    public bool TryValidate(out string error)
    {
        canonicalOrderId = BistroBuilderOrderIdUtility.Normalize(canonicalOrderId);
        barSpotId = BistroBuilderOrderIdUtility.Normalize(barSpotId);
        BistroBuilderServiceMode mode = (BistroBuilderServiceMode)serviceMode;

        if (legacyOrderId < 1 ||
            !BistroBuilderOrderIdUtility.IsValid(canonicalOrderId) ||
            !BistroBuilderServiceModeUtility.IsDefined(mode) ||
            groupId < 1 || waiterId < 1 ||
            !Enum.IsDefined(typeof(OrderState), orderState))
        {
            error = "service.runtime contiene una comanda legacy inválida.";
            return false;
        }

        if (mode == BistroBuilderServiceMode.TableService)
        {
            if (tableId < 1 || !string.IsNullOrEmpty(barSpotId))
            {
                error = "La comanda de mesa no conserva un destino válido.";
                return false;
            }
        }
        else if (tableId != 0 ||
                 !BistroBuilderOrderIdUtility.IsValid(barSpotId))
        {
            error = "La comanda de barra no conserva un destino válido.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderBarSessionSaveRecord
{
    public int groupId;
    public string anchorBarSpotId = string.Empty;
    public List<string> occupiedBarSpotIds = new List<string>();
    public int serviceMode;
    public string canonicalOrderId = string.Empty;
    public int phase;
    public int chargeCents;
    public bool tableRequested;
    public List<string> servedLineIds = new List<string>();

    public bool TryValidate(out string error)
    {
        anchorBarSpotId = BistroBuilderOrderIdUtility.Normalize(anchorBarSpotId);
        canonicalOrderId = BistroBuilderOrderIdUtility.Normalize(canonicalOrderId);

        if (groupId < 1 ||
            !BistroBuilderOrderIdUtility.IsValid(anchorBarSpotId) ||
            occupiedBarSpotIds == null || occupiedBarSpotIds.Count == 0 ||
            !BistroBuilderServiceModeUtility.IsBarMode(
                (BistroBuilderServiceMode)serviceMode
            ) ||
            !Enum.IsDefined(typeof(BistroBuilderBarSessionPhase), phase) ||
            chargeCents < 0 || servedLineIds == null)
        {
            error = "service.runtime contiene una sesión de barra inválida.";
            return false;
        }

        if (!string.IsNullOrEmpty(canonicalOrderId) &&
            !BistroBuilderOrderIdUtility.IsValid(canonicalOrderId))
        {
            error = "La sesión de barra contiene un OrderId inválido.";
            return false;
        }

        BistroBuilderBarSessionPhase sessionPhase =
            (BistroBuilderBarSessionPhase)phase;
        BistroBuilderServiceMode mode =
            (BistroBuilderServiceMode)serviceMode;

        if (sessionPhase == BistroBuilderBarSessionPhase.ClosingForTable &&
            (mode != BistroBuilderServiceMode.WaitingAtBar ||
             !tableRequested))
        {
            error =
                "ClosingForTable exige WaitingAtBar y una mesa solicitada.";
            return false;
        }

        if (sessionPhase ==
                BistroBuilderBarSessionPhase.WaitingForTableAfterConsumption &&
            mode != BistroBuilderServiceMode.WaitingAtBar)
        {
            error =
                "WaitingForTableAfterConsumption solo es válido en " +
                "WaitingAtBar.";
            return false;
        }

        var spotIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < occupiedBarSpotIds.Count; index++)
        {
            occupiedBarSpotIds[index] = BistroBuilderOrderIdUtility.Normalize(
                occupiedBarSpotIds[index]
            );
            if (!BistroBuilderOrderIdUtility.IsValid(
                    occupiedBarSpotIds[index]
                ) ||
                !spotIds.Add(occupiedBarSpotIds[index]))
            {
                error = "La sesión de barra contiene plazas inválidas o " +
                        "duplicadas.";
                return false;
            }
        }

        if (!spotIds.Contains(anchorBarSpotId))
        {
            error = "La plaza ancla no pertenece a la sesión de barra.";
            return false;
        }

        var lineIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < servedLineIds.Count; index++)
        {
            servedLineIds[index] = BistroBuilderOrderIdUtility.Normalize(
                servedLineIds[index]
            );
            if (!BistroBuilderOrderIdUtility.IsValid(servedLineIds[index]) ||
                !lineIds.Add(servedLineIds[index]))
            {
                error = "La sesión de barra contiene LineId servidos " +
                        "inválidos o duplicados.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class BistroBuilderTransferredBarChargeSaveRecord
{
    public int groupId;
    public string sourceOrderId = string.Empty;
    public int amountCents;
    public bool settled;

    public bool TryValidate(out string error)
    {
        sourceOrderId = BistroBuilderOrderIdUtility.Normalize(sourceOrderId);
        if (groupId < 1 ||
            !BistroBuilderOrderIdUtility.IsValid(sourceOrderId) ||
            amountCents < 0)
        {
            error = "service.runtime contiene un cargo de barra inválido.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
