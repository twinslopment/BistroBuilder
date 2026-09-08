using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orquestador del Bloque 12. Reparte líneas canónicas entre estaciones,
/// limita capacidad, administra colas/prioridades y asigna cocineros
/// persistentes. No sustituye la autoridad de comandas ni de inventario.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Kitchen/Advanced Kitchen Service")]
public sealed class BistroBuilderAdvancedKitchenService : MonoBehaviour
{
    [SerializeField] private KitchenSystem kitchenSystem;
    [SerializeField] private BistroBuilderOrderLineExecutionService lineExecutionService;
    [SerializeField] private BistroBuilderCanonicalOrderService canonicalOrderService;
    [SerializeField] private BistroBuilderKitchenStationCatalog stationCatalog;
    [SerializeField] private BistroBuilderStaffService staffService;
    [SerializeField] private BistroBuilderKitchenInteractionCoordinator interactionCoordinator;
    [SerializeField] private bool automaticIncidents = true;
    [SerializeField, Range(2f, 60f)] private float equipmentRepairSeconds = 8f;

    private readonly Dictionary<string, StationRuntime> stations =
        new Dictionary<string, StationRuntime>(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkItem> workByLine =
        new Dictionary<string, WorkItem>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> completedQualityByLine =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<string> routeBuffer = new List<string>(4);
    private readonly List<BistroBuilderEmployeeRecord> cookBuffer =
        new List<BistroBuilderEmployeeRecord>(16);
    private readonly List<WorkItem> removalBuffer = new List<WorkItem>(32);

    private BistroBuilderKitchenIntakeMode intakeMode;
    private BistroBuilderKitchenLoadState loadState;
    private int completedLineCount;
    private int recentQualitySum;
    private int recentQualityCount;
    private IBistroBuilderOperationalSpatialGate spatialGate;

    public event Action Changed;
    public event Action<BistroBuilderKitchenLoadState> LoadStateChanged;
    public event Action<BistroBuilderKitchenIncidentEvent> IncidentOccurred;

    public bool IsOperational => isActiveAndEnabled && stationCatalog != null;
    public BistroBuilderKitchenLoadState LoadState => loadState;
    public BistroBuilderKitchenIntakeMode IntakeMode => intakeMode;
    public int ActiveCount => CountActive();
    public int QueuedCount => CountQueued();
    public int TotalCapacity => CountCapacity();
    public int CompletedLineCount => completedLineCount;
    public int AverageRecentQualityBasisPoints => recentQualityCount > 0
        ? Mathf.Clamp(recentQualitySum / recentQualityCount, 0, 10000)
        : 7000;
    public RestaurantOrder FirstActiveOrder => FindFirstActive()?.Order;
    public string FirstActiveLineId => FindFirstActive()?.LineId ?? string.Empty;
    public float FirstActiveRemainingSeconds => FindFirstActive()?.RemainingSeconds ?? 0f;

    private void Awake()
    {
        CacheDependencies();
        RebuildStations();
    }

    private void OnEnable()
    {
        CacheDependencies();
        RebuildStations();
    }

    private void Update()
    {
        if (!Application.isPlaying || !IsOperational) return;
        float delta = Mathf.Max(0f, Time.deltaTime);
        TickStationBlocks(delta);
        ReconcileCanonicalState();
        StartAvailableWork();
        TickActiveWork(delta);
        RecalculateLoad();
    }

    public bool TryRebuildStationConfiguration(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (workByLine.Count > 0)
        {
            error = "No puede reconstruirse la configuración de estaciones con preparaciones activas.";
            return false;
        }
        if (stationCatalog == null || !stationCatalog.TryValidate(out error))
            return false;
        RebuildStations();
        error = string.Empty;
        return true;
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (kitchenSystem == null || lineExecutionService == null ||
            canonicalOrderService == null || stationCatalog == null || staffService == null)
        {
            error = "12 necesita KitchenSystem, ejecución de líneas, comandas, estaciones y Personal.";
            return false;
        }
        if (!stationCatalog.TryValidate(out error) ||
            !staffService.ValidateConfiguration(out error) ||
            !lineExecutionService.ValidateConfiguration(out error))
            return false;
        if (equipmentRepairSeconds < 2f || equipmentRepairSeconds > 60f)
        {
            error = "El tiempo de reparación de cocina es inválido.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool TryEnqueueLine(
        RestaurantOrder order,
        BistroBuilderCanonicalOrderLine line,
        long sequence,
        float baseDurationSeconds,
        out string error)
    {
        if (!ValidateConfiguration(out error)) return false;
        if (order == null || line == null || order.IsFinished ||
            line.State != BistroBuilderCanonicalOrderLineState.Queued)
        {
            error = "La línea no puede entrar en la cocina avanzada.";
            return false;
        }
        if (workByLine.ContainsKey(line.LineId))
        {
            error = string.Empty;
            return true;
        }
        if (!stationCatalog.TryResolveRoute(line.DishId, routeBuffer) ||
            routeBuffer.Count == 0)
        {
            error = "No existe una ruta de estaciones para " + line.DishId + ".";
            return false;
        }
        var route = new List<string>(routeBuffer);
        for (int i = 0; i < route.Count; i++)
        {
            if (stations.ContainsKey(route[i])) continue;
            error = "La ruta referencia una estación no instalada: " + route[i] + ".";
            return false;
        }

        var work = new WorkItem(
            order,
            line.LineId,
            line.DishId,
            sequence,
            Mathf.Max(0.05f, baseDurationSeconds),
            route);
        work.Priority = ResolveAutomaticPriority(line);
        workByLine.Add(work.LineId, work);
        stations[route[0]].Queued.Add(work);
        SortQueue(stations[route[0]]);
        Changed?.Invoke();
        RecalculateLoad();
        error = string.Empty;
        return true;
    }

    public bool TrySetIntakeMode(
        BistroBuilderKitchenIntakeMode mode,
        out string error)
    {
        if (!Enum.IsDefined(typeof(BistroBuilderKitchenIntakeMode), mode))
        {
            error = "Modo de entrada de cocina inválido.";
            return false;
        }
        intakeMode = mode;
        Changed?.Invoke();
        error = string.Empty;
        return true;
    }

    public bool TryPrioritizeLine(string lineId, out string error)
    {
        lineId = BistroBuilderOrderIdUtility.Normalize(lineId);
        if (!workByLine.TryGetValue(lineId, out WorkItem work) || work.Active)
        {
            error = "Solo puede priorizarse una preparación que espera estación.";
            return false;
        }
        StationRuntime station = stations[work.StationId];
        for (int i = 0; i < station.Queued.Count; i++)
        {
            if (station.Queued[i] != work &&
                station.Queued[i].Priority == BistroBuilderKitchenPriorityKind.PlayerPriority)
            {
                error = "Esta estación ya tiene una prioridad manual activa.";
                return false;
            }
        }
        work.Priority = BistroBuilderKitchenPriorityKind.PlayerPriority;
        SortQueue(station);
        Changed?.Invoke();
        error = string.Empty;
        return true;
    }

    public bool TryForceIncident(
        string stationId,
        BistroBuilderKitchenIncidentKind kind,
        out string error)
    {
        stationId = BistroBuilderStaffStableIdUtility.Normalize(stationId);
        if (!stations.TryGetValue(stationId, out StationRuntime station) ||
            kind == BistroBuilderKitchenIncidentKind.None ||
            !Enum.IsDefined(typeof(BistroBuilderKitchenIncidentKind), kind))
        {
            error = "Estación o incidencia inválida.";
            return false;
        }
        ApplyStationIncident(
            station,
            kind,
            station.Active.Count > 0 ? station.Active[0] : null);
        error = string.Empty;
        return true;
    }

    public bool TryGetLineQualityBasisPoints(
        string lineId,
        out int qualityBasisPoints)
    {
        return completedQualityByLine.TryGetValue(
            BistroBuilderOrderIdUtility.Normalize(lineId),
            out qualityBasisPoints);
    }

    public bool TryGetOrderQualityBasisPoints(
        string canonicalOrderId,
        out int qualityBasisPoints)
    {
        qualityBasisPoints = 0;
        canonicalOrderId = BistroBuilderOrderIdUtility.Normalize(canonicalOrderId);
        if (canonicalOrderService == null ||
            !canonicalOrderService.TryGetOrderSnapshot(
                canonicalOrderId,
                out BistroBuilderCanonicalOrder order) ||
            order == null)
        {
            return false;
        }

        long sum = 0L;
        int count = 0;
        for (int i = 0; i < order.Lines.Count; i++)
        {
            BistroBuilderCanonicalOrderLine line = order.Lines[i];
            if (line == null ||
                !completedQualityByLine.TryGetValue(line.LineId, out int quality))
            {
                continue;
            }
            sum += quality;
            count++;
        }
        if (count == 0) return false;
        qualityBasisPoints = Mathf.Clamp(
            (int)Math.Round(sum / (double)count), 0, 10000);
        return true;
    }
    public bool TryBuildSnapshot(
        out BistroBuilderAdvancedKitchenSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        if (!EnsureStationRuntimeForRead(out error)) return false;

        snapshot = new BistroBuilderAdvancedKitchenSnapshot
        {
            loadState = loadState,
            intakeMode = intakeMode,
            activeCount = ActiveCount,
            queuedCount = QueuedCount,
            totalCapacity = TotalCapacity,
            completedLineCount = completedLineCount,
            averageRecentQualityBasisPoints = AverageRecentQualityBasisPoints
        };

        foreach (BistroBuilderKitchenStationDefinition definition in stationCatalog.Stations)
        {
            if (definition == null ||
                !stations.TryGetValue(definition.stationId, out StationRuntime runtime))
                continue;
            var row = new BistroBuilderKitchenStationSnapshot
            {
                stationId = definition.stationId,
                displayName = definition.displayName,
                kind = definition.kind,
                capacity = definition.baseCapacity,
                activeCount = runtime.Active.Count,
                queuedCount = runtime.Queued.Count,
                blockedSeconds = runtime.BlockedSeconds,
                loadState = ResolveStationLoad(runtime)
            };
            for (int i = 0; i < runtime.Active.Count; i++)
                row.tasks.Add(ToTaskSnapshot(runtime.Active[i]));
            for (int i = 0; i < runtime.Queued.Count; i++)
                row.tasks.Add(ToTaskSnapshot(runtime.Queued[i]));
            snapshot.stations.Add(row);
        }
        error = string.Empty;
        return true;
    }

    public bool TryCaptureRuntimeSnapshot(
        string kitchenId,
        long nextSequence,
        out BistroBuilderKitchenRuntimeSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        if (!EnsureStationRuntimeForRead(out error)) return false;

        snapshot = new BistroBuilderKitchenRuntimeSnapshot
        {
            kitchenId = kitchenId,
            nextSequence = nextSequence,
            advancedEnabled = true,
            advancedIntakeMode = (int)intakeMode
        };
        foreach (StationRuntime station in stations.Values)
        {
            snapshot.stationStates.Add(new BistroBuilderKitchenStationRuntimeSaveData
            {
                stationId = station.Definition.stationId,
                blockedSeconds = station.BlockedSeconds,
                lastIncidentKind = (int)station.LastIncident
            });
            for (int i = 0; i < station.Active.Count; i++)
                snapshot.workItems.Add(ToSaveData(station.Active[i], true, i));
            for (int i = 0; i < station.Queued.Count; i++)
                snapshot.workItems.Add(ToSaveData(station.Queued[i], false, -1));
        }
        foreach (var pair in completedQualityByLine)
            snapshot.completedQualities.Add(new BistroBuilderKitchenLineQualitySaveData
            {
                orderLineId = pair.Key,
                qualityBasisPoints = pair.Value
            });
        if (!snapshot.TryValidate(out error))
        {
            snapshot = null;
            return false;
        }
        return true;
    }

    public bool TryRestoreRuntimeSnapshot(
        BistroBuilderKitchenRuntimeSnapshot snapshot,
        IReadOnlyDictionary<string, RestaurantOrder> ordersByCanonicalId,
        out string error)
    {
        error = string.Empty;
        if (snapshot == null || !snapshot.advancedEnabled ||
            !snapshot.TryValidate(out error) || ordersByCanonicalId == null)
            return false;
        if (!ValidateConfiguration(out error)) return false;

        ClearRuntimeForLoad();
        intakeMode = (BistroBuilderKitchenIntakeMode)snapshot.advancedIntakeMode;
        for (int i = 0; i < snapshot.stationStates.Count; i++)
        {
            var saved = snapshot.stationStates[i];
            if (!stations.TryGetValue(saved.stationId, out StationRuntime station))
                continue;
            station.BlockedSeconds = saved.blockedSeconds;
            station.LastIncident =
                (BistroBuilderKitchenIncidentKind)saved.lastIncidentKind;
        }
        for (int i = 0; i < snapshot.completedQualities.Count; i++)
        {
            var quality = snapshot.completedQualities[i];
            completedQualityByLine[quality.orderLineId] = quality.qualityBasisPoints;
            recentQualitySum += quality.qualityBasisPoints;
            recentQualityCount++;
        }

        for (int i = 0; i < snapshot.workItems.Count; i++)
        {
            BistroBuilderKitchenLineWorkSaveData data = snapshot.workItems[i];
            if (!data.advanced ||
                !ordersByCanonicalId.TryGetValue(
                    data.canonicalOrderId,
                    out RestaurantOrder order) || order == null ||
                !stations.TryGetValue(data.stationId, out StationRuntime station))
            {
                error = "No se pudo reconstruir un trabajo de cocina avanzada.";
                ClearRuntimeForLoad();
                return false;
            }
            if (!stationCatalog.TryResolveRoute(data.dishId, routeBuffer) ||
                routeBuffer.Count != data.stageCount)
            {
                error = "La ruta del plato cambió desde el guardado.";
                ClearRuntimeForLoad();
                return false;
            }
            var work = new WorkItem(
                order,
                data.orderLineId,
                data.dishId,
                data.sequence,
                Mathf.Max(
                    data.totalDurationSeconds * data.stageCount,
                    data.totalDurationSeconds),
                new List<string>(routeBuffer))
            {
                StageIndex = data.stageIndex,
                Priority = (BistroBuilderKitchenPriorityKind)data.priority,
                CookEmployeeId = data.cookEmployeeId,
                TotalSeconds = data.totalDurationSeconds,
                RemainingSeconds = data.remainingDurationSeconds,
                QualityAccumulator = data.qualityBasisPoints * Math.Max(0, data.stageIndex),
                Incident = (BistroBuilderKitchenIncidentKind)data.incidentKind,
                Active = data.wasActive,
                StationSlotIndex = data.stationSlotIndex,
                Restored = data.wasActive
            };
            work.CookDisplayName = ResolveCook(work.CookEmployeeId)?.FullName ?? "Equipo base";
            workByLine.Add(work.LineId, work);
            if (data.wasActive) station.Active.Add(work);
            else station.Queued.Add(work);
        }
        foreach (StationRuntime station in stations.Values) SortQueue(station);
        if (!TryRebuildInteractionProcessPermits(out error))
        {
            ClearRuntimeForLoad();
            return false;
        }
        RecalculateLoad();
        Changed?.Invoke();
        error = string.Empty;
        return true;
    }

    public void ClearRuntimeForLoad()
    {
        interactionCoordinator?.ReleaseAllProcessSlots();
        workByLine.Clear();
        completedQualityByLine.Clear();
        completedLineCount = 0;
        recentQualitySum = 0;
        recentQualityCount = 0;
        foreach (StationRuntime station in stations.Values)
        {
            station.Active.Clear();
            station.Queued.Clear();
            station.BlockedSeconds = 0f;
            station.LastIncident = BistroBuilderKitchenIncidentKind.None;
        }
        RecalculateLoad();
    }

    private void StartAvailableWork()
    {
        if (intakeMode == BistroBuilderKitchenIntakeMode.Paused) return;
        foreach (StationRuntime station in stations.Values)
        {
            if (station.BlockedSeconds > 0f || station.Queued.Count == 0) continue;
            int allowed = station.Definition.baseCapacity;
            if (intakeMode == BistroBuilderKitchenIntakeMode.Reduced)
                allowed = Math.Max(1, allowed / 2);

            while (station.Active.Count < allowed && station.Queued.Count > 0)
            {
                WorkItem work = station.Queued[0];
                station.Queued.RemoveAt(0);
                if (!CanStillProcess(work))
                {
                    ReleaseWork(work, false);
                    continue;
                }

                int candidateSlot = NextFreeSlot(station);
                if (interactionCoordinator == null ||
                    !interactionCoordinator.TryAcquireProcessSlot(
                        station.Definition.stationId,
                        work.LineId,
                        work.StageIndex,
                        candidateSlot,
                        (int)work.Priority))
                {
                    station.Queued.Insert(0, work);
                    break;
                }

                if (spatialGate != null &&
                    !spatialGate.TryAcquireKitchenWork(
                        station.Definition.stationId,
                        work.LineId,
                        candidateSlot,
                        out _))
                {
                    interactionCoordinator.ReleaseProcessSlot(
                        work.LineId,
                        BistroBuilderInteractionReasonCode.SpatialDenied);
                    station.Queued.Insert(0, work);
                    break;
                }

                if (work.StageIndex == 0 &&
                    !lineExecutionService.TryBeginPreparation(
                        work.Order,
                        work.LineId,
                        kitchenSystem.KitchenId,
                        out string beginError))
                {
                    Debug.LogWarning(beginError, this);
                    spatialGate?.ReleaseKitchenWork(work.LineId);
                    interactionCoordinator.ReleaseProcessSlot(work.LineId);
                    ReleaseWork(work, false);
                    continue;
                }

                AssignCook(work, station);
                ConfigureStageTiming(work, station);
                MaybeTriggerAutomaticIncident(work, station);
                if (station.BlockedSeconds > 0f)
                {
                    spatialGate?.ReleaseKitchenWork(work.LineId);
                    interactionCoordinator.ReleaseProcessSlot(
                        work.LineId,
                        BistroBuilderInteractionReasonCode.Interrupted);
                    work.Active = false;
                    work.StationSlotIndex = -1;
                    station.Queued.Insert(0, work);
                    break;
                }

                work.Active = true;
                work.StationSlotIndex = candidateSlot;
                station.Active.Add(work);
                Changed?.Invoke();
            }
        }
    }
    private void TickActiveWork(float delta)
    {
        if (delta <= 0f) return;
        foreach (StationRuntime station in stations.Values)
        {
            for (int i = station.Active.Count - 1; i >= 0; i--)
            {
                WorkItem work = station.Active[i];
                if (!CanStillProcess(work))
                {
                    station.Active.RemoveAt(i);
                    ReleaseWork(work, false);
                    continue;
                }
                work.RemainingSeconds = Mathf.Max(
                    0f,
                    work.RemainingSeconds - delta);
                if (work.RemainingSeconds > 0f) continue;
                station.Active.RemoveAt(i);
                CompleteStage(work, station);
            }
        }
    }

    private void CompleteStage(WorkItem work, StationRuntime station)
    {
        spatialGate?.ReleaseKitchenWork(work.LineId);
        interactionCoordinator?.CompleteProcessSlot(work.LineId);
        BistroBuilderEmployeeRecord cook = ResolveCook(work.CookEmployeeId);
        int stageQuality = BistroBuilderAdvancedKitchenPolicy.ResolveQuality(
            cook,
            station.Definition,
            loadState,
            work.Incident);
        work.QualityAccumulator += stageQuality;

        if (work.StageIndex + 1 < work.Route.Count)
        {
            work.StageIndex++;
            work.Active = false;
            work.StationSlotIndex = -1;
            work.CookEmployeeId = string.Empty;
            work.CookDisplayName = string.Empty;
            work.Incident = BistroBuilderKitchenIncidentKind.None;
            work.TotalSeconds = 0f;
            work.RemainingSeconds = 0f;
            StationRuntime next = stations[work.Route[work.StageIndex]];
            next.Queued.Add(work);
            SortQueue(next);
            Changed?.Invoke();
            return;
        }

        int finalQuality = Mathf.Clamp(
            work.QualityAccumulator / Math.Max(1, work.Route.Count),
            0,
            10000);
        bool completed = lineExecutionService.TryCompletePreparation(
            work.Order,
            work.LineId,
            kitchenSystem.KitchenId,
            out bool productionComplete,
            out string completionError);
        if (!completed)
        {
            Debug.LogWarning(completionError, this);
            ReleaseWork(work, false);
            return;
        }
        completedQualityByLine[work.LineId] = finalQuality;
        completedLineCount++;
        recentQualitySum += finalQuality;
        recentQualityCount++;
        if (recentQualityCount > 64)
        {
            recentQualitySum = AverageRecentQualityBasisPoints * 32;
            recentQualityCount = 32;
        }
        workByLine.Remove(work.LineId);
        kitchenSystem.NotifyAdvancedLineReady(
            work.Order,
            work.LineId,
            work.DishId,
            productionComplete,
            finalQuality);
        Changed?.Invoke();
    }

    private void ReconcileCanonicalState()
    {
        removalBuffer.Clear();
        foreach (WorkItem work in workByLine.Values)
            if (!CanStillProcess(work)) removalBuffer.Add(work);
        for (int i = 0; i < removalBuffer.Count; i++)
        {
            WorkItem work = removalBuffer[i];
            if (stations.TryGetValue(work.StationId, out StationRuntime station))
            {
                station.Active.Remove(work);
                station.Queued.Remove(work);
            }
            ReleaseWork(work, false);
        }
    }

    private bool CanStillProcess(WorkItem work)
    {
        if (work == null || work.Order == null || work.Order.IsFinished) return false;
        if (!lineExecutionService.TryGetLineSnapshot(
                work.Order,
                work.LineId,
                out _,
                out BistroBuilderCanonicalOrderLine line,
                out _))
            return false;
        return line != null &&
               (line.State == BistroBuilderCanonicalOrderLineState.Queued ||
                line.State == BistroBuilderCanonicalOrderLineState.Preparing);
    }

    private void ReleaseWork(WorkItem work, bool interrupt)
    {
        if (work == null) return;
        spatialGate?.ReleaseKitchenWork(work.LineId);
        interactionCoordinator?.ReleaseProcessSlot(
            work.LineId,
            interrupt
                ? BistroBuilderInteractionReasonCode.Interrupted
                : BistroBuilderInteractionReasonCode.TaskCancelled);
        workByLine.Remove(work.LineId);
        if (interrupt && work.Order != null)
            lineExecutionService.TryInterruptPreparation(
                work.Order,
                work.LineId,
                kitchenSystem.KitchenId,
                out _);
        kitchenSystem.NotifyAdvancedLineReleased(work.LineId);
        Changed?.Invoke();
    }

    private void ConfigureStageTiming(WorkItem work, StationRuntime station)
    {
        if (work.Restored && work.TotalSeconds > 0f)
        {
            work.Restored = false;
            return;
        }
        float stageBase = work.BaseDurationSeconds / Math.Max(1, work.Route.Count);
        int cookSpeed = BistroBuilderAdvancedKitchenPolicy.ResolveCookSpeedBasisPoints(
            ResolveCook(work.CookEmployeeId));
        float stationSeconds = BistroBuilderAdvancedKitchenPolicy.ApplySpeed(
            stageBase,
            station.Definition.speedBasisPoints);
        work.TotalSeconds = BistroBuilderAdvancedKitchenPolicy.ApplySpeed(
            stationSeconds,
            cookSpeed);
        work.RemainingSeconds = work.TotalSeconds;
    }

    private void AssignCook(WorkItem work, StationRuntime station)
    {
        cookBuffer.Clear();
        staffService.CopyEmployeesByRole("cook", cookBuffer, true);
        BistroBuilderEmployeeRecord best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < cookBuffer.Count; i++)
        {
            BistroBuilderEmployeeRecord cook = cookBuffer[i];
            if (cook == null ||
                cook.availability != BistroBuilderEmployeeAvailability.Available)
                continue;
            int score = BistroBuilderAdvancedKitchenPolicy.ResolveCookSpeedBasisPoints(cook);
            score += BistroBuilderAdvancedKitchenPolicy.StableRoll(
                cook.employeeId + ":" + station.Definition.stationId,
                401) - 200;
            score -= CountAssignments(cook.employeeId) * 900;
            if (score <= bestScore) continue;
            bestScore = score;
            best = cook;
        }
        work.CookEmployeeId = best?.employeeId ?? string.Empty;
        work.CookDisplayName = best?.FullName ?? "Equipo base";
    }

    private int CountAssignments(string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId)) return 0;
        int count = 0;
        foreach (StationRuntime station in stations.Values)
            for (int i = 0; i < station.Active.Count; i++)
                if (string.Equals(
                    station.Active[i].CookEmployeeId,
                    employeeId,
                    StringComparison.Ordinal)) count++;
        return count;
    }

    private BistroBuilderEmployeeRecord ResolveCook(string employeeId)
    {
        return !string.IsNullOrWhiteSpace(employeeId) &&
               staffService.TryGetEmployee(
                   employeeId,
                   out BistroBuilderEmployeeRecord cook)
            ? cook
            : null;
    }

    private void MaybeTriggerAutomaticIncident(WorkItem work, StationRuntime station)
    {
        if (!automaticIncidents ||
            work.Incident != BistroBuilderKitchenIncidentKind.None) return;
        int risk = 10000 - station.Definition.reliabilityBasisPoints;
        if (risk <= 0) return;
        int roll = BistroBuilderAdvancedKitchenPolicy.StableRoll(
            work.LineId + ":" + station.Definition.stationId + ":" + work.StageIndex,
            10000);
        if (roll >= risk) return;
        int kindRoll = BistroBuilderAdvancedKitchenPolicy.StableRoll(
            work.LineId + ":incident",
            100);
        BistroBuilderKitchenIncidentKind kind = kindRoll < 20
            ? BistroBuilderKitchenIncidentKind.EquipmentFailure
            : kindRoll < 60
                ? BistroBuilderKitchenIncidentKind.Slowdown
                : BistroBuilderKitchenIncidentKind.QualityRisk;
        ApplyStationIncident(station, kind, work);
    }

    private void ApplyStationIncident(
        StationRuntime station,
        BistroBuilderKitchenIncidentKind kind,
        WorkItem work)
    {
        station.LastIncident = kind;
        if (work != null) work.Incident = kind;
        string message;
        if (kind == BistroBuilderKitchenIncidentKind.EquipmentFailure)
        {
            station.BlockedSeconds = Mathf.Max(
                station.BlockedSeconds,
                equipmentRepairSeconds);
            message = station.Definition.displayName +
                      " queda temporalmente bloqueada por una avería.";
        }
        else if (kind == BistroBuilderKitchenIncidentKind.Slowdown)
        {
            if (work != null)
            {
                work.TotalSeconds *= 1.25f;
                work.RemainingSeconds *= 1.25f;
            }
            message = station.Definition.displayName +
                      " trabaja temporalmente más despacio.";
        }
        else
        {
            message = "Riesgo de calidad detectado en " +
                      station.Definition.displayName + ".";
        }
        IncidentOccurred?.Invoke(new BistroBuilderKitchenIncidentEvent(
            station.Definition.stationId,
            work?.LineId ?? string.Empty,
            kind,
            message));
        Changed?.Invoke();
    }

    private void TickStationBlocks(float delta)
    {
        foreach (StationRuntime station in stations.Values)
        {
            if (station.BlockedSeconds <= 0f) continue;
            station.BlockedSeconds = Mathf.Max(
                0f,
                station.BlockedSeconds - delta);
            if (station.BlockedSeconds <= 0f)
                station.LastIncident = BistroBuilderKitchenIncidentKind.None;
        }
    }

    private void RecalculateLoad()
    {
        bool blockedRequired = false;
        foreach (StationRuntime station in stations.Values)
        {
            if (station.BlockedSeconds <= 0f || station.Queued.Count <= 0) continue;
            blockedRequired = true;
            break;
        }
        BistroBuilderKitchenLoadState next =
            BistroBuilderAdvancedKitchenPolicy.ResolveLoad(
                ActiveCount,
                QueuedCount,
                TotalCapacity,
                blockedRequired);
        if (next == loadState) return;
        loadState = next;
        LoadStateChanged?.Invoke(loadState);
        Changed?.Invoke();
    }

    private BistroBuilderKitchenLoadState ResolveStationLoad(StationRuntime station)
    {
        return BistroBuilderAdvancedKitchenPolicy.ResolveLoad(
            station.Active.Count,
            station.Queued.Count,
            station.Definition.baseCapacity,
            station.BlockedSeconds > 0f && station.Queued.Count > 0);
    }

    private static BistroBuilderKitchenPriorityKind ResolveAutomaticPriority(
        BistroBuilderCanonicalOrderLine line)
    {
        if (line.AdvancedOriginKind == BistroBuilderAdvancedOrderLineOriginKind.Replacement ||
            line.AdvancedOriginKind == BistroBuilderAdvancedOrderLineOriginKind.Courtesy)
            return BistroBuilderKitchenPriorityKind.IncidentReplacement;
        if (line.CourseIndex > 0)
            return BistroBuilderKitchenPriorityKind.CourseSync;
        return BistroBuilderKitchenPriorityKind.Normal;
    }

    private bool EnsureStationRuntimeForRead(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (stationCatalog == null)
        {
            error = "Falta el cat\u00e1logo de estaciones de cocina.";
            return false;
        }
        if (!stationCatalog.TryValidate(out error))
            return false;

        if (StationRuntimeMatchesCatalog())
        {
            error = string.Empty;
            return true;
        }

        if (workByLine.Count > 0)
        {
            error = "La configuraci\u00f3n de estaciones cambi\u00f3 mientras existen preparaciones activas.";
            return false;
        }

        RebuildStations();
        if (!StationRuntimeMatchesCatalog())
        {
            error = "No pudo inicializarse el runtime completo de estaciones de cocina.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool StationRuntimeMatchesCatalog()
    {
        if (stationCatalog == null || stations.Count != stationCatalog.Stations.Count)
            return false;
        for (int i = 0; i < stationCatalog.Stations.Count; i++)
        {
            BistroBuilderKitchenStationDefinition definition = stationCatalog.Stations[i];
            if (definition == null || !stations.ContainsKey(definition.stationId))
                return false;
        }
        return true;
    }

    private void RebuildStations()
    {
        if (stationCatalog == null) return;
        var previousBlocks = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var pair in stations)
            previousBlocks[pair.Key] = pair.Value.BlockedSeconds;
        stations.Clear();
        for (int i = 0; i < stationCatalog.Stations.Count; i++)
        {
            BistroBuilderKitchenStationDefinition definition =
                stationCatalog.Stations[i];
            if (definition == null) continue;
            var runtime = new StationRuntime(definition.DeepClone());
            if (previousBlocks.TryGetValue(
                    definition.stationId,
                    out float blocked)) runtime.BlockedSeconds = blocked;
            stations[definition.stationId] = runtime;
        }
    }

    private void CacheDependencies()
    {
        if (spatialGate == null)
        {
            BistroBuilderOperationalSpatialCoordinator coordinator =
                GetComponent<BistroBuilderOperationalSpatialCoordinator>();
            if (coordinator == null)
                coordinator = FindFirstObjectByType<
                    BistroBuilderOperationalSpatialCoordinator>();
            spatialGate = coordinator;
        }
        if (kitchenSystem == null)
            kitchenSystem = FindFirstObjectByType<KitchenSystem>();
        if (lineExecutionService == null && kitchenSystem != null)
            lineExecutionService = kitchenSystem.LineExecutionService;
        if (canonicalOrderService == null && lineExecutionService != null)
            canonicalOrderService = lineExecutionService.CanonicalOrderService;
        if (staffService == null) TryGetComponent(out staffService);
        if (interactionCoordinator == null)
        {
            interactionCoordinator = GetComponent<BistroBuilderKitchenInteractionCoordinator>();
            if (interactionCoordinator == null)
                interactionCoordinator = FindFirstObjectByType<BistroBuilderKitchenInteractionCoordinator>();
        }
    }

    private bool TryRebuildInteractionProcessPermits(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (interactionCoordinator == null)
        {
            error = "Falta el coordinador Interaction de procesos de cocina.";
            return false;
        }

        interactionCoordinator.ReleaseAllProcessSlots();
        foreach (StationRuntime station in stations.Values)
        {
            for (int i = 0; i < station.Active.Count; i++)
            {
                WorkItem work = station.Active[i];
                if (work == null || work.StationSlotIndex < 0 ||
                    !interactionCoordinator.TryAcquireProcessSlot(
                        station.Definition.stationId,
                        work.LineId,
                        work.StageIndex,
                        work.StationSlotIndex,
                        (int)work.Priority))
                {
                    error = "No pudo reconstruirse la ocupación lógica de cocina para " +
                            (work != null ? work.LineId : "<null>") + ".";
                    return false;
                }
            }
        }
        return true;
    }
    private static void SortQueue(StationRuntime station)
    {
        station.Queued.Sort((left, right) =>
        {
            int priority = ((int)right.Priority).CompareTo((int)left.Priority);
            return priority != 0
                ? priority
                : left.Sequence.CompareTo(right.Sequence);
        });
    }

    private static int NextFreeSlot(StationRuntime station)
    {
        for (int slot = 0; slot < station.Definition.baseCapacity; slot++)
        {
            bool used = false;
            for (int i = 0; i < station.Active.Count; i++)
            {
                if (station.Active[i].StationSlotIndex != slot) continue;
                used = true;
                break;
            }
            if (!used) return slot;
        }
        return station.Active.Count;
    }

    private int CountActive()
    {
        int count = 0;
        foreach (StationRuntime station in stations.Values)
            count += station.Active.Count;
        return count;
    }

    private int CountQueued()
    {
        int count = 0;
        foreach (StationRuntime station in stations.Values)
            count += station.Queued.Count;
        return count;
    }

    private int CountCapacity()
    {
        int count = 0;
        foreach (StationRuntime station in stations.Values)
            count += station.Definition.baseCapacity;
        return count;
    }

    private WorkItem FindFirstActive()
    {
        WorkItem best = null;
        foreach (StationRuntime station in stations.Values)
        {
            for (int i = 0; i < station.Active.Count; i++)
            {
                WorkItem candidate = station.Active[i];
                if (best == null || candidate.Sequence < best.Sequence)
                    best = candidate;
            }
        }
        return best;
    }

    private static BistroBuilderKitchenTaskSnapshot ToTaskSnapshot(WorkItem work)
    {
        int quality = work.StageIndex > 0
            ? Mathf.Clamp(
                work.QualityAccumulator / work.StageIndex,
                0,
                10000)
            : 7000;
        return new BistroBuilderKitchenTaskSnapshot
        {
            canonicalOrderId = work.Order?.CanonicalOrderId ?? string.Empty,
            legacyOrderId = work.Order?.OrderId ?? 0,
            lineId = work.LineId,
            dishId = work.DishId,
            stationId = work.StationId,
            stageIndex = work.StageIndex,
            stageCount = work.Route.Count,
            sequence = work.Sequence,
            priority = work.Priority,
            cookEmployeeId = work.CookEmployeeId,
            cookDisplayName = string.IsNullOrWhiteSpace(work.CookDisplayName)
                ? "Equipo base"
                : work.CookDisplayName,
            totalSeconds = work.TotalSeconds,
            remainingSeconds = work.RemainingSeconds,
            qualityBasisPoints = quality,
            incident = work.Incident,
            active = work.Active
        };
    }

    private static BistroBuilderKitchenLineWorkSaveData ToSaveData(
        WorkItem work,
        bool active,
        int slot)
    {
        int quality = work.StageIndex > 0
            ? Mathf.Clamp(
                work.QualityAccumulator / work.StageIndex,
                0,
                10000)
            : 7000;
        return new BistroBuilderKitchenLineWorkSaveData
        {
            canonicalOrderId = work.Order?.CanonicalOrderId ?? string.Empty,
            legacyOrderId = work.Order?.OrderId ?? 0,
            orderLineId = work.LineId,
            dishId = work.DishId,
            sequence = work.Sequence,
            totalDurationSeconds = Mathf.Max(0.05f, work.TotalSeconds),
            remainingDurationSeconds = Mathf.Clamp(
                work.RemainingSeconds,
                0f,
                Mathf.Max(0.05f, work.TotalSeconds)),
            wasActive = active,
            advanced = true,
            stationId = work.StationId,
            stationSlotIndex = active ? slot : -1,
            stageIndex = work.StageIndex,
            stageCount = work.Route.Count,
            priority = (int)work.Priority,
            cookEmployeeId = work.CookEmployeeId,
            qualityBasisPoints = quality,
            incidentKind = (int)work.Incident
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        equipmentRepairSeconds = Mathf.Clamp(equipmentRepairSeconds, 2f, 60f);
        CacheDependencies();
    }
#endif

    private sealed class StationRuntime
    {
        public BistroBuilderKitchenStationDefinition Definition { get; }
        public List<WorkItem> Queued { get; } = new List<WorkItem>(16);
        public List<WorkItem> Active { get; } = new List<WorkItem>(8);
        public float BlockedSeconds;
        public BistroBuilderKitchenIncidentKind LastIncident;

        public StationRuntime(BistroBuilderKitchenStationDefinition definition)
        {
            Definition = definition;
        }
    }

    private sealed class WorkItem
    {
        public RestaurantOrder Order { get; }
        public string LineId { get; }
        public string DishId { get; }
        public long Sequence { get; }
        public float BaseDurationSeconds { get; }
        public List<string> Route { get; }
        public int StageIndex;
        public BistroBuilderKitchenPriorityKind Priority;
        public string CookEmployeeId = string.Empty;
        public string CookDisplayName = string.Empty;
        public float TotalSeconds;
        public float RemainingSeconds;
        public int QualityAccumulator;
        public BistroBuilderKitchenIncidentKind Incident;
        public bool Active;
        public int StationSlotIndex = -1;
        public bool Restored;

        public string StationId =>
            Route[Mathf.Clamp(StageIndex, 0, Route.Count - 1)];

        public WorkItem(
            RestaurantOrder order,
            string lineId,
            string dishId,
            long sequence,
            float baseDurationSeconds,
            List<string> route)
        {
            Order = order;
            LineId = BistroBuilderOrderIdUtility.Normalize(lineId);
            DishId = BistroBuilderOrderIdUtility.Normalize(dishId);
            Sequence = sequence;
            BaseDurationSeconds = baseDurationSeconds;
            Route = route ?? new List<string>();
        }
    }
}
