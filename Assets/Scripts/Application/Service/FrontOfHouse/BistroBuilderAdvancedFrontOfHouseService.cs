using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad de gestión avanzada de entrada, sala y barra del Bloque 14.
/// Prioriza grupos, protege reservas, coordina alternativas de zona y barra,
/// mide saturación de sala y conserva la rotación estratégica de mesas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Service/Advanced Front Of House Service")]
public sealed class BistroBuilderAdvancedFrontOfHouseService : MonoBehaviour
{
    [Header("Autoridades canónicas")]
    [SerializeField] private TableAssignmentSystem tableAssignmentSystem;
    [SerializeField] private CustomerWaitingAreaSystem waitingAreaSystem;
    [SerializeField] private RestaurantTableRegistry tableRegistry;
    [SerializeField] private BistroBuilderBarServiceRegistry barRegistry;
    [SerializeField] private BistroBuilderBarServiceSystem barServiceSystem;
    [SerializeField] private BistroBuilderReservationService reservationService;
    [SerializeField] private BistroBuilderAdvancedCustomerProfileService profileService;
    [SerializeField] private BistroBuilderAdvancedCustomerSeatingPreferenceService seatingPreferenceService;
    [SerializeField] private BistroBuilderAdvancedWaiterService advancedWaiterService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private GameClock gameClock;

    [Header("Política de sala")]
    [SerializeField, Min(0.1f)] private float evaluationIntervalSeconds = 0.35f;
    [SerializeField, Range(5, 120)] private int reservationProtectionMinutes = 35;
    [SerializeField, Range(0.1f, 1f)] private float alternativeZonePressure01 = 0.45f;
    [SerializeField, Range(0.1f, 1f)] private float automaticBarOfferPressure01 = 0.58f;
    [SerializeField] private bool enableAutomaticBarOffers = true;
    [SerializeField] private bool enableReservationProtection = true;
    [SerializeField] private bool enableAlternativeZoneOffers = true;
    [SerializeField] private bool enableAbandonment = true;

    private readonly Dictionary<CustomerGroup, BistroBuilderFrontOfHouseQueueEntry> queueByGroup = new();
    private readonly List<CustomerGroup> queueScratch = new(32);
    private readonly List<BistroBuilderReservationRecord> reservationScratch = new(64);
    private readonly Dictionary<int, BistroBuilderFrontOfHouseTableRotationRecord> rotationByTableId = new();
    private readonly HashSet<int> pendingAutomaticBarOffers = new();
    private float evaluationCountdown;
    private bool evaluating;
    private long revision;
    private long nextSeatSequence = 1L;

    public event Action<BistroBuilderFrontOfHouseOperationalState> OperationalStateChanged;
    public event Action<int, string> AlternativeZoneOffered;
    public event Action<int> GroupSentToBar;
    public event Action<int> GroupAbandoned;
    public event Action QueueChanged;

    public BistroBuilderFrontOfHouseOperationalState OperationalState { get; private set; } =
        BistroBuilderFrontOfHouseOperationalState.Fluid;
    public int WaitingGroupCount => CountCurrentWaitingGroups();
    public long Revision => revision;

    private void Awake() => CacheDependencies();

    private void OnEnable()
    {
        CacheDependencies();
        if (tableAssignmentSystem != null)
        {
            tableAssignmentSystem.CustomerGroupRegistered += HandleGroupRegistered;
            tableAssignmentSystem.TableAssigned += HandleTableAssigned;
        }
        evaluationCountdown = 0f;
    }

    private void OnDisable()
    {
        if (tableAssignmentSystem != null)
        {
            tableAssignmentSystem.CustomerGroupRegistered -= HandleGroupRegistered;
            tableAssignmentSystem.TableAssigned -= HandleTableAssigned;
        }
        queueByGroup.Clear();
        queueScratch.Clear();
        pendingAutomaticBarOffers.Clear();
    }

    private void Update()
    {
        if (!Application.isPlaying || BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            return;
        evaluationCountdown -= Time.unscaledDeltaTime;
        if (evaluationCountdown > 0f) return;
        evaluationCountdown = Mathf.Max(0.1f, evaluationIntervalSeconds);
        EvaluateRoom();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (tableAssignmentSystem == null || waitingAreaSystem == null ||
            tableRegistry == null || barRegistry == null || barServiceSystem == null ||
            reservationService == null || profileService == null ||
            seatingPreferenceService == null || advancedWaiterService == null ||
            generalGameStateService == null || gameClock == null)
        {
            error = "Bloque 14 necesita sala, barra, reservas, clientes avanzados, camareros y reloj.";
            return false;
        }
        if (!reservationService.ValidateConfiguration(out error) ||
            !profileService.ValidateConfiguration(out error) ||
            !seatingPreferenceService.ValidateConfiguration(out error) ||
            !advancedWaiterService.ValidateConfiguration(out error) ||
            !barRegistry.ValidateConfiguration(out error) ||
            !barServiceSystem.ValidateConfiguration(out error))
            return false;
        if (evaluationIntervalSeconds <= 0f || reservationProtectionMinutes < 5)
        {
            error = "La política temporal de sala avanzada es inválida.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public int CompareWaitingGroups(CustomerGroup left, CustomerGroup right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left == null) return 1;
        if (right == null) return -1;
        BistroBuilderFrontOfHouseQueueEntry a = BuildEntry(left);
        BistroBuilderFrontOfHouseQueueEntry b = BuildEntry(right);
        int byPriority = b.priorityScore.CompareTo(a.priorityScore);
        if (byPriority != 0) return byPriority;
        int byWait = b.waitingSeconds.CompareTo(a.waitingSeconds);
        if (byWait != 0) return byWait;
        return left.GroupId.CompareTo(right.GroupId);
    }

    public void CopyQueueSnapshot(List<BistroBuilderFrontOfHouseQueueEntry> destination)
    {
        if (destination == null) return;
        destination.Clear();
        CacheDependencies();
        if (tableAssignmentSystem == null) return;

        IReadOnlyList<CustomerGroup> groups = tableAssignmentSystem.RegisteredGroups;
        for (int i = 0; i < groups.Count; i++)
        {
            CustomerGroup group = groups[i];
            if (!IsCurrentQueueCandidate(group)) continue;
            destination.Add(BuildEntry(group));
        }

        destination.Sort(CompareQueueEntries);
        for (int i = 0; i < destination.Count; i++)
            destination[i].queuePosition = i + 1;
    }

    public bool TryGetQueueEntry(int groupId, out BistroBuilderFrontOfHouseQueueEntry entry)
    {
        entry = null;
        CacheDependencies();
        if (tableAssignmentSystem == null) return false;

        IReadOnlyList<CustomerGroup> groups = tableAssignmentSystem.RegisteredGroups;
        for (int i = 0; i < groups.Count; i++)
        {
            CustomerGroup group = groups[i];
            if (group == null || group.GroupId != groupId || !IsCurrentQueueCandidate(group))
                continue;
            entry = BuildEntry(group);
            int position = 1;
            for (int j = 0; j < groups.Count; j++)
            {
                CustomerGroup other = groups[j];
                if (other == null || ReferenceEquals(other, group) || !IsCurrentQueueCandidate(other))
                    continue;
                if (CompareQueueEntries(BuildEntry(other), entry) < 0) position++;
            }
            entry.queuePosition = position;
            return true;
        }
        return false;
    }

    private int CountCurrentWaitingGroups()
    {
        CacheDependencies();
        if (tableAssignmentSystem == null) return 0;
        IReadOnlyList<CustomerGroup> groups = tableAssignmentSystem.RegisteredGroups;
        int count = 0;
        for (int i = 0; i < groups.Count; i++)
            if (IsCurrentQueueCandidate(groups[i])) count++;
        return count;
    }

    private static bool IsCurrentQueueCandidate(CustomerGroup group)
    {
        return group != null &&
               !group.HasAssignedTable &&
               group.CurrentState == CustomerGroupState.WaitingForTable &&
               group.RequestedServiceMode != BistroBuilderServiceMode.BarService;
    }

    private static int CompareQueueEntries(
        BistroBuilderFrontOfHouseQueueEntry left,
        BistroBuilderFrontOfHouseQueueEntry right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left == null) return 1;
        if (right == null) return -1;
        int byPriority = right.priorityScore.CompareTo(left.priorityScore);
        if (byPriority != 0) return byPriority;
        int byWait = right.waitingSeconds.CompareTo(left.waitingSeconds);
        if (byWait != 0) return byWait;
        return left.groupId.CompareTo(right.groupId);
    }

    public bool IsTableProtectedForGroup(RestaurantTable table, CustomerGroup group)
    {
        if (table == null) return true;
        if (!enableReservationProtection) return false;
        reservationService.CopyAllReservations(reservationScratch);
        int currentAbsoluteMinute = generalGameStateService.DayIndex * 1440 +
                                    gameClock.Hour * 60 + gameClock.Minute;
        string groupReservationId = ResolveReservationId(group);
        for (int i = 0; i < reservationScratch.Count; i++)
        {
            BistroBuilderReservationRecord record = reservationScratch[i];
            if (record == null || record.IsTerminal || record.tableId != table.TableId)
                continue;
            int reservationAbsoluteMinute = record.dayIndex * 1440 + record.arrivalMinute;
            int delta = reservationAbsoluteMinute - currentAbsoluteMinute;
            bool protect = record.status == BistroBuilderReservationStatus.Due ||
                           record.status == BistroBuilderReservationStatus.Arrived ||
                           (record.status == BistroBuilderReservationStatus.Booked &&
                            delta <= reservationProtectionMinutes && delta >= -15);
            if (!protect) continue;
            if (!string.IsNullOrEmpty(groupReservationId) &&
                string.Equals(groupReservationId, record.reservationId, StringComparison.Ordinal))
                return false;
            return true;
        }
        return false;
    }

    public float EvaluateTableScore(CustomerGroup group, RestaurantTable table)
    {
        if (group == null || table == null) return float.MinValue;
        bool protectedForReservation = IsTableProtectedForGroup(table, group);
        float distance = Vector3.Distance(group.transform.position,
            table.CustomerApproachPoint != null
                ? table.CustomerApproachPoint.position
                : table.transform.position);
        int preferenceScore = ResolveZonePreferenceScore(group, table);
        BistroBuilderFrontOfHouseTableRotationRecord rotation = GetRotation(table.TableId);
        return BistroBuilderAdvancedFrontOfHousePolicy.ComputeTableScore(
            group.GroupSize, table.Capacity, distance, preferenceScore,
            rotation.seatedCount, rotation.lastSeatSequence, protectedForReservation);
    }

    public bool TrySendGroupToBar(int groupId, out string error)
    {
        error = string.Empty;
        CustomerGroup group = FindRegisteredGroup(groupId);
        if (group == null)
        {
            error = "No existe el grupo solicitado en la cola de sala.";
            return false;
        }
        if (!barServiceSystem.TryOfferWaitingAtBar(group, out error))
            return false;
        pendingAutomaticBarOffers.Add(group.GroupId);
        revision++;
        GroupSentToBar?.Invoke(group.GroupId);
        return true;
    }

    public bool TryCaptureRuntimeSnapshot(
        out BistroBuilderAdvancedFrontOfHouseRuntimeSnapshot snapshot,
        out string error)
    {
        snapshot = new BistroBuilderAdvancedFrontOfHouseRuntimeSnapshot
        {
            revision = revision,
            nextSeatSequence = nextSeatSequence
        };
        foreach (BistroBuilderFrontOfHouseTableRotationRecord item in rotationByTableId.Values)
            snapshot.tableRotations.Add(item.DeepClone());
        snapshot.tableRotations.Sort((a, b) => a.tableId.CompareTo(b.tableId));
        return snapshot.TryValidate(out error);
    }

    public bool TryRestoreRuntimeSnapshot(
        BistroBuilderAdvancedFrontOfHouseRuntimeSnapshot snapshot,
        out string error)
    {
        if (snapshot == null)
        {
            rotationByTableId.Clear();
            revision = 0L;
            nextSeatSequence = 1L;
            error = string.Empty;
            return true;
        }
        if (!snapshot.TryValidate(out error)) return false;
        rotationByTableId.Clear();
        for (int i = 0; i < snapshot.tableRotations.Count; i++)
        {
            BistroBuilderFrontOfHouseTableRotationRecord item = snapshot.tableRotations[i].DeepClone();
            rotationByTableId.Add(item.tableId, item);
        }
        revision = snapshot.revision;
        nextSeatSequence = snapshot.nextSeatSequence;
        evaluationCountdown = 0f;
        error = string.Empty;
        return true;
    }

    public void ResetForRuntimeLoad()
    {
        queueByGroup.Clear();
        queueScratch.Clear();
        pendingAutomaticBarOffers.Clear();
        evaluationCountdown = 0f;
    }

    private void EvaluateRoom()
    {
        if (evaluating || !ValidateConfiguration(out _)) return;
        evaluating = true;
        try
        {
            queueByGroup.Clear();
            queueScratch.Clear();
            IReadOnlyList<CustomerGroup> groups = tableAssignmentSystem.RegisteredGroups;
            float maxPressure = 0f;
            for (int i = 0; i < groups.Count; i++)
            {
                CustomerGroup group = groups[i];
                if (group == null || group.HasAssignedTable ||
                    group.CurrentState != CustomerGroupState.WaitingForTable ||
                    group.RequestedServiceMode == BistroBuilderServiceMode.BarService)
                    continue;
                BistroBuilderFrontOfHouseQueueEntry entry = BuildEntry(group);
                queueByGroup[group] = entry;
                queueScratch.Add(group);
                maxPressure = Mathf.Max(maxPressure, entry.pressure01);
            }
            queueScratch.Sort(CompareWaitingGroups);
            for (int i = 0; i < queueScratch.Count; i++)
                queueByGroup[queueScratch[i]].queuePosition = i + 1;

            int freeTables = CountFreeUnprotectedTables();
            float waiterSaturation = ResolveAverageWaiterSaturation();
            BistroBuilderFrontOfHouseOperationalState nextState =
                BistroBuilderAdvancedFrontOfHousePolicy.ResolveOperationalState(
                    queueScratch.Count, freeTables, maxPressure, waiterSaturation);
            if (nextState != OperationalState)
            {
                OperationalState = nextState;
                OperationalStateChanged?.Invoke(nextState);
            }

            ProcessQueueDecisions();
            waitingAreaSystem.RefreshWaitingQueue();
            tableAssignmentSystem.RequestReevaluation();
            QueueChanged?.Invoke();
        }
        finally
        {
            evaluating = false;
        }
    }

    private void ProcessQueueDecisions()
    {
        for (int i = 0; i < queueScratch.Count; i++)
        {
            CustomerGroup group = queueScratch[i];
            if (group == null || !queueByGroup.TryGetValue(group, out var entry)) continue;

            if (enableAlternativeZoneOffers && entry.pressure01 >= alternativeZonePressure01)
                TryOfferAlternativeZone(group);

            if (enableAutomaticBarOffers && !entry.hasReservation && !group.IsOccupyingBar &&
                entry.pressure01 >= automaticBarOfferPressure01 &&
                barRegistry.FreeCapacity >= group.GroupSize &&
                !pendingAutomaticBarOffers.Contains(group.GroupId))
            {
                if (barServiceSystem.TryOfferWaitingAtBar(group, out _))
                {
                    pendingAutomaticBarOffers.Add(group.GroupId);
                    revision++;
                    GroupSentToBar?.Invoke(group.GroupId);
                }
            }

            if (enableAbandonment &&
                BistroBuilderAdvancedFrontOfHousePolicy.ShouldAbandon(
                    entry.waitingSeconds, entry.toleranceSeconds,
                    entry.hasReservation, group.IsOccupyingBar))
            {
                group.SetState(CustomerGroupState.Leaving);
                revision++;
                GroupAbandoned?.Invoke(group.GroupId);
            }
        }
    }

    private void TryOfferAlternativeZone(CustomerGroup group)
    {
        if (group == null || !tableAssignmentSystem.TryGetPreferredTable(group, out RestaurantTable preferred) ||
            preferred == null || preferred.CanSeatGroup(group.GroupSize))
            return;
        string preferredZone = ResolveAreaId(preferred);
        RestaurantTable alternative = null;
        float best = float.MinValue;
        foreach (RestaurantTable table in tableRegistry.RegisteredTables)
        {
            if (table == null || !table.CanSeatGroup(group.GroupSize) ||
                IsTableProtectedForGroup(table, group)) continue;
            string zone = ResolveAreaId(table);
            if (string.Equals(zone, preferredZone, StringComparison.Ordinal)) continue;
            float score = EvaluateTableScore(group, table);
            if (alternative == null || score > best ||
                (Mathf.Approximately(score, best) && table.TableId < alternative.TableId))
            {
                alternative = table;
                best = score;
            }
        }
        if (alternative == null) return;
        if (!tableAssignmentSystem.TryReleasePreferredTableReservation(group)) return;
        revision++;
        AlternativeZoneOffered?.Invoke(group.GroupId, ResolveAreaId(alternative));
    }


    private BistroBuilderFrontOfHouseQueueEntry BuildEntry(CustomerGroup group)
    {
        float tolerance = ResolveTableWaitTolerance(group);
        bool reservation = HasReservation(group);
        bool vip = IsVip(group);
        float wait = group != null ? group.WaitingTime : 0f;
        bool bar = group != null && group.IsOccupyingBar;
        return new BistroBuilderFrontOfHouseQueueEntry
        {
            groupId = group != null ? group.GroupId : 0,
            partySize = group != null ? group.GroupSize : 0,
            waitingSeconds = wait,
            toleranceSeconds = tolerance,
            pressure01 = Mathf.Clamp01(wait / Mathf.Max(10f, tolerance)),
            priorityScore = BistroBuilderAdvancedFrontOfHousePolicy.ComputeQueuePriority(
                wait, tolerance, group != null ? group.GroupSize : 1, reservation, vip, bar),
            hasReservation = reservation,
            isVip = vip,
            occupyingBar = bar,
            reason = BistroBuilderAdvancedFrontOfHousePolicy.ResolveQueueReason(
                wait, tolerance, reservation, vip, bar)
        };
    }

    private float ResolveTableWaitTolerance(CustomerGroup group)
    {
        if (group == null || !profileService.TryGetGroupProfile(
                group.GroupId, out BistroBuilderAdvancedCustomerGroupProfile profile) ||
            profile == null || profile.members == null || profile.members.Count == 0)
            return 75f;
        float minimum = float.MaxValue;
        for (int i = 0; i < profile.members.Count; i++)
        {
            BistroBuilderAdvancedCustomerMemberProfile member = profile.members[i];
            if (member != null) minimum = Mathf.Min(minimum, member.tableWaitToleranceSeconds);
        }
        return minimum == float.MaxValue ? 75f : Mathf.Max(20f, minimum);
    }

    private bool IsVip(CustomerGroup group)
    {
        return group != null && seatingPreferenceService.TryBuildPreference(
            group, out BistroBuilderAdvancedCustomerServicePreference preference, out _) &&
            preference != null && preference.isVip;
    }

    private bool HasReservation(CustomerGroup group) =>
        !string.IsNullOrEmpty(ResolveReservationId(group));

    private static string ResolveReservationId(CustomerGroup group)
    {
        if (group == null || !group.TryGetComponent(out BistroBuilderCustomerAcquisitionTag tag))
            return string.Empty;
        BistroBuilderCustomerAcquisitionProfile acquisition = tag.CreateSnapshot();
        if (acquisition == null ||
            !string.Equals(acquisition.discoverySourceId, "reservation", StringComparison.Ordinal))
            return string.Empty;
        return acquisition.sourceReferenceId ?? string.Empty;
    }

    private int ResolveZonePreferenceScore(CustomerGroup group, RestaurantTable table)
    {
        if (group == null || table == null ||
            !seatingPreferenceService.TryBuildPreference(
                group, out BistroBuilderAdvancedCustomerServicePreference preference, out _) ||
            preference == null || preference.preferredZoneTagIds == null)
            return 0;
        string areaId = ResolveAreaId(table);
        for (int i = 0; i < preference.preferredZoneTagIds.Count; i++)
            if (string.Equals(preference.preferredZoneTagIds[i], areaId, StringComparison.Ordinal))
                return 3000;
        return 0;
    }

    private static string ResolveAreaId(RestaurantTable table)
    {
        if (table != null && table.TryGetComponent(out RestaurantAreaMember member) &&
            member.AssignedArea != null)
            return BistroBuilderAdvancedCustomerProfileEngine.NormalizeId(member.AssignedArea.AreaId);
        return "dining";
    }

    private int CountFreeUnprotectedTables()
    {
        int count = 0;
        foreach (RestaurantTable table in tableRegistry.RegisteredTables)
            if (table != null && table.IsAvailable && !IsTableProtectedForGroup(table, null)) count++;
        return count;
    }

    private float ResolveAverageWaiterSaturation()
    {
        Waiter[] waiters = FindObjectsByType<Waiter>(FindObjectsSortMode.None);
        if (waiters == null || waiters.Length == 0) return 1f;
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < waiters.Length; i++)
        {
            if (waiters[i] != null && advancedWaiterService.TryBuildSnapshot(
                    waiters[i], out BistroBuilderAdvancedWaiterSnapshot snapshot) && snapshot != null)
            {
                sum += snapshot.saturation01;
                count++;
            }
        }
        return count > 0 ? Mathf.Clamp01(sum / count) : 1f;
    }

    private BistroBuilderFrontOfHouseTableRotationRecord GetRotation(int tableId)
    {
        if (!rotationByTableId.TryGetValue(tableId, out var record))
        {
            record = new BistroBuilderFrontOfHouseTableRotationRecord { tableId = tableId };
            rotationByTableId.Add(tableId, record);
        }
        return record;
    }

    private CustomerGroup FindRegisteredGroup(int groupId)
    {
        IReadOnlyList<CustomerGroup> groups = tableAssignmentSystem.RegisteredGroups;
        for (int i = 0; i < groups.Count; i++)
            if (groups[i] != null && groups[i].GroupId == groupId) return groups[i];
        return null;
    }

    private void HandleGroupRegistered(CustomerGroup group)
    {
        if (group == null) return;
        pendingAutomaticBarOffers.Remove(group.GroupId);
        evaluationCountdown = 0f;
    }

    private void HandleTableAssigned(CustomerGroup group, RestaurantTable table)
    {
        if (table == null) return;
        BistroBuilderFrontOfHouseTableRotationRecord rotation = GetRotation(table.TableId);
        rotation.seatedCount++;
        rotation.lastSeatSequence = nextSeatSequence++;
        if (group != null) pendingAutomaticBarOffers.Remove(group.GroupId);
        revision++;
        evaluationCountdown = 0f;
    }

    private void CacheDependencies()
    {
        if (tableAssignmentSystem == null) TryGetComponent(out tableAssignmentSystem);
        if (waitingAreaSystem == null) TryGetComponent(out waitingAreaSystem);
        if (tableRegistry == null) TryGetComponent(out tableRegistry);
        if (barRegistry == null) TryGetComponent(out barRegistry);
        if (barServiceSystem == null) TryGetComponent(out barServiceSystem);
        if (reservationService == null) TryGetComponent(out reservationService);
        if (profileService == null) TryGetComponent(out profileService);
        if (seatingPreferenceService == null) TryGetComponent(out seatingPreferenceService);
        if (advancedWaiterService == null) TryGetComponent(out advancedWaiterService);
        if (generalGameStateService == null) TryGetComponent(out generalGameStateService);
        if (gameClock == null) gameClock = FindFirstObjectByType<GameClock>();
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate()
    {
        evaluationIntervalSeconds = Mathf.Max(0.1f, evaluationIntervalSeconds);
        reservationProtectionMinutes = Mathf.Clamp(reservationProtectionMinutes, 5, 120);
        CacheDependencies();
    }
#endif
}
