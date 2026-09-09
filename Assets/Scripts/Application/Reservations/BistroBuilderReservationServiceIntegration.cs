using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 6C — Puente entre reservas persistentes y el servicio real.
/// Materializa una reserva vencida como CustomerGroup canónico y deja que
/// los flujos existentes de llegada, mesa, seating y servicio hagan el resto.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Reservations/Reservation Service Integration")]
public sealed class BistroBuilderReservationServiceIntegration : MonoBehaviour
{
    [SerializeField] private BistroBuilderReservationService reservationService;
    [SerializeField] private BistroBuilderReservationAvailabilityService availabilityService;
    [SerializeField] private BistroBuilderReservationRuntimeProfile runtimeProfile;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private GameClock gameClock;
    [SerializeField] private RestaurantServiceStateService serviceStateService;
    [SerializeField] private RestaurantTableRegistry tableRegistry;
    [SerializeField] private TableAssignmentSystem tableAssignmentSystem;
    [SerializeField] private CustomerGroupSpawner customerGroupSpawner;

    private readonly List<BistroBuilderReservationRecord> reservationBuffer =
        new List<BistroBuilderReservationRecord>();
    private readonly Dictionary<string, CustomerGroup> groupByReservationId =
        new Dictionary<string, CustomerGroup>(StringComparer.Ordinal);
    private readonly Dictionary<CustomerGroup, string> reservationIdByGroup =
        new Dictionary<CustomerGroup, string>();

    private bool evaluating;

    public int ActiveReservationGroupCount => groupByReservationId.Count;

    public event Action<string, CustomerGroup> ReservationGroupSpawned;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void Start()
    {
        EvaluateReservations();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearRuntimeBindings();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (reservationService == null || availabilityService == null ||
            runtimeProfile == null || generalGameStateService == null ||
            gameClock == null || serviceStateService == null ||
            tableRegistry == null || tableAssignmentSystem == null ||
            customerGroupSpawner == null)
        {
            error = "6C necesita Reservas, disponibilidad, reloj, servicio y flujo canónico de clientes.";
            return false;
        }

        if (!reservationService.ValidateConfiguration(out error) ||
            !availabilityService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !runtimeProfile.TryValidate(out error))
            return false;

        error = string.Empty;
        return true;
    }

    public bool TryGetActiveGroup(
        string reservationId,
        out CustomerGroup group)
    {
        group = null;
        string normalized = BistroBuilderReservationEngine.NormalizeId(reservationId);
        return normalized.Length > 0 &&
               groupByReservationId.TryGetValue(normalized, out group) &&
               group != null;
    }

    /// <summary>
    /// Entrada explícita para validadores/diagnóstico. La ejecución normal
    /// llega por eventos de reloj, reservas y apertura del servicio.
    /// </summary>
    public void RequestEvaluation()
    {
        EvaluateReservations();
    }

    private void EvaluateReservations()
    {
        if (evaluating || !Application.isPlaying ||
            BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            return;

        if (!ValidateConfiguration(out _))
            return;

        evaluating = true;
        try
        {
            int currentDay = generalGameStateService.DayIndex;
            int currentMinute = gameClock.Hour * 60 + gameClock.Minute;
            reservationService.CopyAllReservations(reservationBuffer);

            for (int index = 0; index < reservationBuffer.Count; index++)
            {
                BistroBuilderReservationRecord record = reservationBuffer[index];
                if (record == null || record.IsTerminal)
                    continue;

                EvaluateReservation(record, currentDay, currentMinute);
            }
        }
        finally
        {
            evaluating = false;
        }
    }

    private void EvaluateReservation(
        BistroBuilderReservationRecord record,
        int currentDay,
        int currentMinute)
    {
        if (record.status == BistroBuilderReservationStatus.Booked &&
            IsDue(record, currentDay, currentMinute))
        {
            if (!reservationService.TryTransition(
                    record.reservationId,
                    BistroBuilderReservationStatus.Due,
                    out BistroBuilderReservationRecord due,
                    out _))
                return;

            record = due;
        }

        if (record.status != BistroBuilderReservationStatus.Due)
            return;

        if (IsPastNoShowGrace(record, currentDay, currentMinute))
        {
            reservationService.TryTransition(
                record.reservationId,
                BistroBuilderReservationStatus.NoShow,
                out _,
                out _);
            return;
        }

        if (!serviceStateService.AcceptsNewCustomers)
            return;

        TryMaterializeReservation(record);
    }

    private bool TryMaterializeReservation(BistroBuilderReservationRecord record)
    {
        string reservationId = BistroBuilderReservationEngine.NormalizeId(
            record.reservationId);
        if (groupByReservationId.ContainsKey(reservationId))
            return true;

        if (record.tableId < 1)
        {
            if (!availabilityService.TryAssignBestTable(
                    reservationId,
                    out BistroBuilderReservationRecord assigned,
                    out _))
                return false;
            record = assigned;
        }

        if (!tableRegistry.TryGetTableById(
                record.tableId,
                out RestaurantTable table) ||
            table == null)
        {
            if (!availabilityService.TryAssignBestTable(
                    reservationId,
                    out BistroBuilderReservationRecord reassigned,
                    out _) ||
                !tableRegistry.TryGetTableById(reassigned.tableId, out table))
                return false;
            record = reassigned;
        }

        if (!table.CanSeatGroup(record.partySize))
            return false;

        if (!customerGroupSpawner.TrySpawnExternalTableServiceGroup(
                record.partySize,
                out CustomerGroup group,
                out _))
            return false;

        BistroBuilderCustomerAcquisitionTag acquisitionTag =
            group.GetComponent<BistroBuilderCustomerAcquisitionTag>();
        BistroBuilderCustomerAcquisitionProfile acquisition =
            BistroBuilderCustomerAcquisitionProfile.CreateBaseline();
        acquisition.sourceSystemId = "reservations.runtime";
        acquisition.sourceReferenceId = reservationId;
        acquisition.discoverySourceId = "reservation";
        if (acquisitionTag == null ||
            !acquisitionTag.TryConfigure(acquisition, out _))
        {
            customerGroupSpawner.UnregisterAndDestroyGroupForRuntimeLoad(group);
            return false;
        }

        if (!tableAssignmentSystem.TryReservePreferredTable(
                group,
                table,
                out _))
        {
            customerGroupSpawner.UnregisterAndDestroyGroupForRuntimeLoad(group);
            return false;
        }

        groupByReservationId.Add(reservationId, group);
        reservationIdByGroup.Add(group, reservationId);
        group.StateChanged += HandleReservationGroupStateChanged;
        ReservationGroupSpawned?.Invoke(reservationId, group);

        Debug.Log(
            "Reserva " + reservationId +
            " materializada como grupo " + group.GroupId +
            " para mesa " + table.TableId + ".",
            this);
        return true;
    }

    private void HandleReservationGroupStateChanged(
        CustomerGroup group,
        CustomerGroupState newState)
    {
        if (group == null ||
            !reservationIdByGroup.TryGetValue(group, out string reservationId))
            return;

        SynchronizeReservationStatus(reservationId, newState);
    }

    private void SynchronizeReservationStatus(
        string reservationId,
        CustomerGroupState groupState)
    {
        bool isArrived = groupState != CustomerGroupState.Entering;
        bool isSeated = groupState == CustomerGroupState.Seated ||
                        groupState == CustomerGroupState.WaitingForWaiter ||
                        groupState == CustomerGroupState.Ordering ||
                        groupState == CustomerGroupState.WaitingForFood ||
                        groupState == CustomerGroupState.Eating ||
                        groupState == CustomerGroupState.WaitingForBill ||
                        groupState == CustomerGroupState.Paying ||
                        groupState == CustomerGroupState.Leaving ||
                        groupState == CustomerGroupState.Finished;

        if (isArrived)
            EnsureReservationState(
                reservationId,
                BistroBuilderReservationStatus.Arrived);

        if (isSeated)
            EnsureReservationState(
                reservationId,
                BistroBuilderReservationStatus.Seated);

        if (groupState == CustomerGroupState.Finished)
        {
            EnsureReservationState(
                reservationId,
                BistroBuilderReservationStatus.Completed);
            UnbindReservationGroup(reservationId);
        }
    }

    private void EnsureReservationState(
        string reservationId,
        BistroBuilderReservationStatus target)
    {
        for (int guard = 0; guard < 4; guard++)
        {
            if (!reservationService.TryGetReservation(
                    reservationId,
                    out BistroBuilderReservationRecord record) ||
                record == null || record.IsTerminal ||
                LifecycleRank(record.status) >= LifecycleRank(target))
                return;

            BistroBuilderReservationStatus next;
            if (record.status == BistroBuilderReservationStatus.Booked)
                next = BistroBuilderReservationStatus.Due;
            else if (record.status == BistroBuilderReservationStatus.Due)
                next = BistroBuilderReservationStatus.Arrived;
            else if (record.status == BistroBuilderReservationStatus.Arrived)
                next = BistroBuilderReservationStatus.Seated;
            else if (record.status == BistroBuilderReservationStatus.Seated)
                next = BistroBuilderReservationStatus.Completed;
            else
                return;

            if (!reservationService.TryTransition(
                    reservationId,
                    next,
                    out _,
                    out _))
                return;

            if (next == target)
                return;
        }
    }

    private static int LifecycleRank(BistroBuilderReservationStatus status)
    {
        switch (status)
        {
            case BistroBuilderReservationStatus.Booked: return 0;
            case BistroBuilderReservationStatus.Due: return 1;
            case BistroBuilderReservationStatus.Arrived: return 2;
            case BistroBuilderReservationStatus.Seated: return 3;
            case BistroBuilderReservationStatus.Completed: return 4;
            default: return int.MaxValue;
        }
    }

    private static bool IsDue(
        BistroBuilderReservationRecord record,
        int currentDay,
        int currentMinute)
    {
        return currentDay > record.dayIndex ||
               (currentDay == record.dayIndex &&
                currentMinute >= record.arrivalMinute);
    }

    private bool IsPastNoShowGrace(
        BistroBuilderReservationRecord record,
        int currentDay,
        int currentMinute)
    {
        if (currentDay > record.dayIndex)
            return true;
        if (currentDay < record.dayIndex)
            return false;

        return currentMinute >
               record.arrivalMinute + runtimeProfile.NoShowGraceMinutes;
    }

    private void HandleTimeChanged(int hour, int minute)
    {
        EvaluateReservations();
    }

    private void HandleServiceOpened()
    {
        EvaluateReservations();
    }

    private void HandleReservationsChanged(long revision)
    {
        EvaluateReservations();
    }

    private void HandleReservationsRestored()
    {
        ClearRuntimeBindings();
        EvaluateReservations();
    }

    private void Subscribe()
    {
        if (gameClock != null)
            gameClock.TimeChanged += HandleTimeChanged;
        if (serviceStateService != null)
            serviceStateService.ServiceOpened += HandleServiceOpened;
        if (reservationService != null)
        {
            reservationService.ReservationsChanged += HandleReservationsChanged;
            reservationService.ReservationsRestored += HandleReservationsRestored;
        }
    }

    private void Unsubscribe()
    {
        if (gameClock != null)
            gameClock.TimeChanged -= HandleTimeChanged;
        if (serviceStateService != null)
            serviceStateService.ServiceOpened -= HandleServiceOpened;
        if (reservationService != null)
        {
            reservationService.ReservationsChanged -= HandleReservationsChanged;
            reservationService.ReservationsRestored -= HandleReservationsRestored;
        }
    }

    private void UnbindReservationGroup(string reservationId)
    {
        string normalized = BistroBuilderReservationEngine.NormalizeId(reservationId);
        if (!groupByReservationId.TryGetValue(normalized, out CustomerGroup group))
            return;

        groupByReservationId.Remove(normalized);
        if (group != null)
        {
            group.StateChanged -= HandleReservationGroupStateChanged;
            reservationIdByGroup.Remove(group);
        }
    }

    private void ClearRuntimeBindings()
    {
        foreach (KeyValuePair<string, CustomerGroup> pair in groupByReservationId)
        {
            if (pair.Value != null)
                pair.Value.StateChanged -= HandleReservationGroupStateChanged;
        }

        groupByReservationId.Clear();
        reservationIdByGroup.Clear();
    }

    /// <summary>
    /// Copia los enlaces runtime activos para persistencia 6D.
    /// </summary>
    public void CopyActiveRuntimeBindings(
        List<BistroBuilderReservationRuntimeBindingSaveRecord> destination)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        destination.Clear();
        foreach (KeyValuePair<string, CustomerGroup> pair in groupByReservationId)
        {
            if (pair.Value == null)
                continue;

            destination.Add(new BistroBuilderReservationRuntimeBindingSaveRecord
            {
                reservationId = pair.Key,
                groupId = pair.Value.GroupId
            });
        }

        destination.Sort((left, right) =>
            string.CompareOrdinal(left.reservationId, right.reservationId));
    }

    /// <summary>
    /// Desconecta enlaces anteriores antes de que service.runtime reconstruya
    /// los CustomerGroup de un slot objetivo.
    /// </summary>
    public void PrepareForRuntimeLoad()
    {
        ClearRuntimeBindings();
    }

    /// <summary>
    /// Reconecta reservas activas con los CustomerGroup ya restaurados por
    /// service.runtime. No crea grupos ni duplica la asignación de mesas.
    /// </summary>
    public bool TryRestoreRuntimeBindings(
        IReadOnlyList<BistroBuilderReservationRuntimeBindingSaveRecord> bindings,
        out string error)
    {
        error = string.Empty;
        if (!ValidateConfiguration(out error))
            return false;
        if (bindings == null)
        {
            error = "Los enlaces runtime de Reservas son nulos.";
            return false;
        }

        ClearRuntimeBindings();
        var groupsById = new Dictionary<int, CustomerGroup>();
        foreach (CustomerGroup group in tableAssignmentSystem.RegisteredGroups)
        {
            if (group != null && !groupsById.TryAdd(group.GroupId, group))
            {
                error = "service.runtime contiene GroupId duplicado: " + group.GroupId + ".";
                ClearRuntimeBindings();
                return false;
            }
        }

        var reservationIds = new HashSet<string>(StringComparer.Ordinal);
        var groupIds = new HashSet<int>();
        for (int index = 0; index < bindings.Count; index++)
        {
            BistroBuilderReservationRuntimeBindingSaveRecord binding = bindings[index];
            string reservationId = binding != null
                ? BistroBuilderReservationEngine.NormalizeId(binding.reservationId)
                : string.Empty;
            if (binding == null || reservationId.Length == 0 || binding.groupId < 1 ||
                !reservationIds.Add(reservationId) || !groupIds.Add(binding.groupId))
            {
                error = "reservations.state contiene un enlace runtime inválido o duplicado.";
                ClearRuntimeBindings();
                return false;
            }

            if (!reservationService.TryGetReservation(
                    reservationId,
                    out BistroBuilderReservationRecord reservation) ||
                reservation == null || reservation.IsTerminal)
            {
                error = "El enlace runtime apunta a una reserva inexistente o terminal.";
                ClearRuntimeBindings();
                return false;
            }

            if (!groupsById.TryGetValue(binding.groupId, out CustomerGroup group) ||
                group == null || group.GroupSize != reservation.partySize ||
                group.CurrentState == CustomerGroupState.Finished)
            {
                error = "El CustomerGroup restaurado no coincide con la reserva " + reservationId + ".";
                ClearRuntimeBindings();
                return false;
            }

            if (group.HasAssignedTable)
            {
                if (reservation.tableId < 1 ||
                    group.AssignedTable.TableId != reservation.tableId)
                {
                    error = "La mesa restaurada del grupo no coincide con la reserva " + reservationId + ".";
                    ClearRuntimeBindings();
                    return false;
                }
            }
            else if (reservation.tableId > 0)
            {
                if (!tableRegistry.TryGetTableById(reservation.tableId, out RestaurantTable table) ||
                    table == null ||
                    !tableAssignmentSystem.TryReservePreferredTable(group, table, out error))
                {
                    ClearRuntimeBindings();
                    return false;
                }
            }

            groupByReservationId.Add(reservationId, group);
            reservationIdByGroup.Add(group, reservationId);
            group.StateChanged += HandleReservationGroupStateChanged;
        }

        return true;
    }
    private void CacheDependencies()
    {
        if (reservationService == null)
            TryGetComponent(out reservationService);
        if (availabilityService == null)
            TryGetComponent(out availabilityService);
        if (generalGameStateService == null)
            TryGetComponent(out generalGameStateService);
        if (gameClock == null)
            gameClock = FindFirstObjectByType<GameClock>();
        if (serviceStateService == null)
            serviceStateService = FindFirstObjectByType<RestaurantServiceStateService>();
        if (tableRegistry == null)
            tableRegistry = FindFirstObjectByType<RestaurantTableRegistry>();
        if (tableAssignmentSystem == null)
            tableAssignmentSystem = FindFirstObjectByType<TableAssignmentSystem>();
        if (customerGroupSpawner == null)
            customerGroupSpawner = FindFirstObjectByType<CustomerGroupSpawner>();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependencies();
    }

    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
