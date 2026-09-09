using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordina Claims operativos de cocina, pass y barra.
/// No asigna tareas, no mueve agentes y no cambia estados de gameplay.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderOperationalSpatialCoordinator :
    MonoBehaviour,
    IBistroBuilderOperationalSpatialGate
{
    [SerializeField]
    private BistroBuilderSpatialInteractionService spatialService;
    [SerializeField]
    private BistroBuilderKitchenSpatialAdapter kitchenAdapter;
    [SerializeField]
    private BistroBuilderAdvancedKitchenService advancedKitchenService;
    [SerializeField]
    private BistroBuilderBarServiceRegistry barRegistry;
    [SerializeField]
    private BistroBuilderBarServiceSystem barServiceSystem;
    [SerializeField, Min(0.05f)]
    private float reconciliationInterval = 0.2f;

    private readonly Dictionary<string, string> leaseByOwner =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<string> activeOwners =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<string> removalBuffer =
        new List<string>(64);
    private readonly List<BistroBuilderBarSpatialAdapter> barAdapters =
        new List<BistroBuilderBarSpatialAdapter>(16);

    private float nextReconciliationAt;

    public int ManagedLeaseCount => leaseByOwner.Count;
    public int LastRejectedClaims { get; private set; }

    private void Awake()
    {
        ResolveDependencies();
        RebuildBindings();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextReconciliationAt)
            return;
        nextReconciliationAt = Time.unscaledTime +
            Mathf.Max(0.05f, reconciliationInterval);
        ReconcileOperationalClaims();
    }

    private void OnDisable()
    {
        ReleaseAllManagedLeases();
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();
        RebuildBindings();
        if (spatialService == null ||
            kitchenAdapter == null ||
            advancedKitchenService == null ||
            barRegistry == null ||
            barServiceSystem == null)
        {
            error =
                "El coordinador BBSIS 2B tiene dependencias incompletas.";
            return false;
        }

        if (!kitchenAdapter.ValidateConfiguration(out error))
            return false;
        for (int i = 0; i < barAdapters.Count; i++)
            if (barAdapters[i] == null ||
                !barAdapters[i].ValidateConfiguration(out error))
                return false;

        error = string.Empty;
        return true;
    }

    public void RebuildBindings()
    {
        barAdapters.Clear();
        BistroBuilderBarSpatialAdapter[] found =
            FindObjectsByType<BistroBuilderBarSpatialAdapter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID);
        for (int i = 0; i < found.Length; i++)
            if (found[i] != null)
                barAdapters.Add(found[i]);
        barAdapters.Sort((left, right) =>
            string.CompareOrdinal(
                left.BarSpot != null
                    ? left.BarSpot.BarSpotId
                    : string.Empty,
                right.BarSpot != null
                    ? right.BarSpot.BarSpotId
                    : string.Empty));
    }

    public bool TryAcquireKitchenWork(
        string stationId,
        string workId,
        int preferredSlot,
        out string rejectionReason)
    {
        rejectionReason = string.Empty;
        ResolveDependencies();
        if (spatialService == null || kitchenAdapter == null)
        {
            rejectionReason = "BBSIS 2B no está disponible.";
            return false;
        }

        string normalizedWork =
            BistroBuilderOrderIdUtility.Normalize(workId);
        string ownerId = "bbsis.kitchen.work." + normalizedWork;
        if (string.IsNullOrWhiteSpace(normalizedWork))
        {
            rejectionReason =
                "La tarea de cocina no tiene identidad estable.";
            return false;
        }

        if (TryRefresh(ownerId, 0f))
            return true;

        if (!kitchenAdapter.TryGetStationWorkVolume(
                stationId,
                preferredSlot,
                out string portId,
                out BistroBuilderSpatialVolume volume,
                out BistroBuilderSpatialConflictMode mode))
        {
            rejectionReason =
                "No existe puesto espacial para " + stationId + ".";
            return false;
        }

        return TryAcquire(
            ownerId,
            kitchenAdapter.Subject.SubjectId,
            portId,
            BistroBuilderSpatialClaimKind.Work,
            mode,
            volume,
            90,
            0f,
            out rejectionReason);
    }

    public void ReleaseKitchenWork(string workId)
    {
        string normalized =
            BistroBuilderOrderIdUtility.Normalize(workId);
        ReleaseManagedOwner(
            "bbsis.kitchen.work." + normalized);
    }

    public bool TryAcquirePassTransfer(
        string ownerId,
        float durationSeconds,
        out BistroBuilderSpatialLeaseDecision decision)
    {
        decision = new BistroBuilderSpatialLeaseDecision();
        ResolveDependencies();
        string normalized =
            BistroBuilderOrderIdUtility.Normalize(ownerId);
        string managedOwner = "bbsis.pass." + normalized;
        if (spatialService == null || kitchenAdapter == null ||
            string.IsNullOrWhiteSpace(normalized) ||
            !kitchenAdapter.TryGetPassVolume(
                out BistroBuilderSpatialVolume volume,
                out BistroBuilderSpatialConflictMode mode))
        {
            decision.failure =
                BistroBuilderSpatialLeaseFailure.InvalidRequest;
            decision.message =
                "No pudo resolverse el pass espacial.";
            return false;
        }

        if (TryRefresh(
                managedOwner,
                Mathf.Max(0.1f, durationSeconds)))
        {
            decision.granted = true;
            return true;
        }

        bool granted = TryAcquire(
            managedOwner,
            kitchenAdapter.Subject.SubjectId,
            BistroBuilderKitchenSpatialAdapter.PassPortId,
            BistroBuilderSpatialClaimKind.Transfer,
            mode,
            volume,
            95,
            Mathf.Max(0.1f, durationSeconds),
            out string rejection);
        decision.granted = granted;
        decision.failure = granted
            ? BistroBuilderSpatialLeaseFailure.None
            : BistroBuilderSpatialLeaseFailure.Conflict;
        decision.message = granted
            ? "Transferencia de pass reservada."
            : rejection;
        return granted;
    }

    public void ReleaseOperationalOwner(string ownerId)
    {
        string normalized =
            BistroBuilderOrderIdUtility.Normalize(ownerId);
        ReleaseManagedOwner("bbsis.pass." + normalized);
        ReleaseManagedOwner(normalized);
    }

    public void ReconcileOperationalClaims()
    {
        ResolveDependencies();
        if (spatialService == null)
            return;
        activeOwners.Clear();
        LastRejectedClaims = 0;
        ReconcileKitchen();
        ReconcileBar();
        ReleaseInactiveOwners();
    }

    private void ReconcileKitchen()
    {
        if (advancedKitchenService == null ||
            kitchenAdapter == null ||
            !advancedKitchenService.TryBuildSnapshot(
                out BistroBuilderAdvancedKitchenSnapshot snapshot,
                out _))
            return;

        for (int stationIndex = 0;
             stationIndex < snapshot.stations.Count;
             stationIndex++)
        {
            BistroBuilderKitchenStationSnapshot station =
                snapshot.stations[stationIndex];
            if (station == null)
                continue;
            int slot = 0;
            for (int taskIndex = 0;
                 taskIndex < station.tasks.Count;
                 taskIndex++)
            {
                BistroBuilderKitchenTaskSnapshot task =
                    station.tasks[taskIndex];
                if (task == null || !task.active)
                    continue;
                string owner = "bbsis.kitchen.work." +
                    BistroBuilderOrderIdUtility.Normalize(task.lineId);
                activeOwners.Add(owner);
                if (!TryAcquireKitchenWork(
                        station.stationId,
                        task.lineId,
                        slot++,
                        out _))
                    LastRejectedClaims++;
            }
        }
    }

    private void ReconcileBar()
    {
        if (barServiceSystem == null)
            return;
        for (int i = 0; i < barAdapters.Count; i++)
        {
            BistroBuilderBarSpatialAdapter adapter =
                barAdapters[i];
            BistroBuilderBarServiceSpot spot =
                adapter != null ? adapter.BarSpot : null;
            CustomerGroup group =
                spot != null ? spot.AssignedCustomerGroup : null;
            if (group == null)
                continue;

            string customerOwner = "bbsis.bar.customer." +
                group.GroupId + "." + spot.BarSpotId;
            activeOwners.Add(customerOwner);
            if (!adapter.HasCustomerLease &&
                !adapter.TryAcquireCustomerLease(group, out _))
                LastRejectedClaims++;

            if (!barServiceSystem.TryGetSessionSnapshot(
                    group,
                    out BistroBuilderBarSessionSnapshot session) ||
                !string.Equals(
                    session.AnchorBarSpotId,
                    spot.BarSpotId,
                    StringComparison.Ordinal))
                continue;

            if (NeedsServiceClaim(session.Phase))
                ReconcileBarPort(
                    adapter,
                    group,
                    BistroBuilderBarSpatialAdapter.ServicePortId,
                    BistroBuilderSpatialClaimKind.Service,
                    "bbsis.bar.service." + group.GroupId);

            if (session.Phase ==
                BistroBuilderBarSessionPhase.WaitingForItems)
                ReconcileBarPort(
                    adapter,
                    group,
                    BistroBuilderBarSpatialAdapter.TransferPortId,
                    BistroBuilderSpatialClaimKind.Transfer,
                    "bbsis.bar.transfer." + group.GroupId);
        }
    }

    private void ReconcileBarPort(
        BistroBuilderBarSpatialAdapter adapter,
        CustomerGroup group,
        string portId,
        BistroBuilderSpatialClaimKind kind,
        string ownerId)
    {
        activeOwners.Add(ownerId);
        if (TryRefresh(ownerId, 0.65f))
            return;
        if (!adapter.TryGetPortVolume(
                portId,
                out BistroBuilderSpatialVolume volume,
                out BistroBuilderSpatialConflictMode mode) ||
            !TryAcquire(
                ownerId,
                adapter.Subject.SubjectId,
                portId,
                kind,
                mode,
                volume,
                85,
                0.65f,
                out _))
            LastRejectedClaims++;
    }

    private bool TryAcquire(
        string ownerId,
        string subjectId,
        string portId,
        BistroBuilderSpatialClaimKind kind,
        BistroBuilderSpatialConflictMode mode,
        BistroBuilderSpatialVolume volume,
        int priority,
        float duration,
        out string rejection)
    {
        rejection = string.Empty;
        var request = new BistroBuilderSpatialClaimRequest
        {
            ownerId = ownerId,
            subjectId = subjectId,
            portId = portId,
            kind = kind,
            conflictMode = mode,
            volume = volume,
            priority = priority,
            durationSeconds = duration,
            validateAgainstStaticGeometry = true
        };
        if (!spatialService.TryAcquireLease(
                request,
                out BistroBuilderSpatialLease lease,
                out BistroBuilderSpatialLeaseDecision decision))
        {
            rejection = decision.message;
            return false;
        }
        leaseByOwner[ownerId] = lease.leaseId;
        return true;
    }

    private bool TryRefresh(
        string ownerId,
        float duration)
    {
        if (!leaseByOwner.TryGetValue(
                ownerId, out string leaseId))
            return false;
        if (spatialService != null &&
            spatialService.RefreshLease(leaseId, duration))
            return true;
        leaseByOwner.Remove(ownerId);
        return false;
    }

    private void ReleaseInactiveOwners()
    {
        removalBuffer.Clear();
        foreach (KeyValuePair<string, string> pair in leaseByOwner)
        {
            if (pair.Key.StartsWith(
                    "bbsis.kitchen.work.",
                    StringComparison.Ordinal) ||
                pair.Key.StartsWith(
                    "bbsis.bar.service.",
                    StringComparison.Ordinal) ||
                pair.Key.StartsWith(
                    "bbsis.bar.transfer.",
                    StringComparison.Ordinal))
            {
                if (!activeOwners.Contains(pair.Key))
                    removalBuffer.Add(pair.Key);
            }
        }
        for (int i = 0; i < removalBuffer.Count; i++)
            ReleaseManagedOwner(removalBuffer[i]);
    }

    private void ReleaseManagedOwner(string ownerId)
    {
        if (!leaseByOwner.TryGetValue(
                ownerId, out string leaseId))
            return;
        spatialService?.ReleaseLease(leaseId);
        leaseByOwner.Remove(ownerId);
    }

    private void ReleaseAllManagedLeases()
    {
        removalBuffer.Clear();
        removalBuffer.AddRange(leaseByOwner.Keys);
        for (int i = 0; i < removalBuffer.Count; i++)
            ReleaseManagedOwner(removalBuffer[i]);
        for (int i = 0; i < barAdapters.Count; i++)
            barAdapters[i]?.ReleaseCustomerLease();
    }

    private void ResolveDependencies()
    {
        if (spatialService == null)
            spatialService = FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        if (kitchenAdapter == null)
            kitchenAdapter = FindFirstObjectByType<
                BistroBuilderKitchenSpatialAdapter>();
        if (advancedKitchenService == null)
            advancedKitchenService = FindFirstObjectByType<
                BistroBuilderAdvancedKitchenService>();
        if (barRegistry == null)
            barRegistry = FindFirstObjectByType<
                BistroBuilderBarServiceRegistry>();
        if (barServiceSystem == null)
            barServiceSystem = FindFirstObjectByType<
                BistroBuilderBarServiceSystem>();
    }

    private static bool NeedsServiceClaim(
        BistroBuilderBarSessionPhase phase)
    {
        return phase ==
                   BistroBuilderBarSessionPhase.TakingOrder ||
               phase ==
                   BistroBuilderBarSessionPhase.WaitingForItems ||
               phase ==
                   BistroBuilderBarSessionPhase.WaitingForPayment ||
               phase ==
                   BistroBuilderBarSessionPhase.Paying;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        BistroBuilderSpatialInteractionService spatial,
        BistroBuilderKitchenSpatialAdapter kitchen,
        BistroBuilderAdvancedKitchenService advancedKitchen,
        BistroBuilderBarServiceRegistry registry,
        BistroBuilderBarServiceSystem bar)
    {
        spatialService = spatial;
        kitchenAdapter = kitchen;
        advancedKitchenService = advancedKitchen;
        barRegistry = registry;
        barServiceSystem = bar;
        RebuildBindings();
    }
#endif
}
