using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bloque 15. Orquesta el cierre operativo y convierte la jornada real en un
/// resumen persistente sin duplicar las autoridades de Finanzas, Inventario,
/// Reputación, Sala ni Comandas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Service/End Of Day Service 15")]
public sealed class BistroBuilderEndOfDayService : MonoBehaviour
{
    [Header("Autoridades canónicas")]
    [SerializeField] private RestaurantServiceStateService serviceStateService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private GameClock gameClock;
    [SerializeField] private BistroBuilderFinancialResultsService financialResultsService;
    [SerializeField] private BistroBuilderInventoryService inventoryService;
    [SerializeField] private BistroBuilderReputationService reputationService;
    [SerializeField] private BistroBuilderCustomerExperienceTrackingService experienceTrackingService;
    [SerializeField] private BistroBuilderAdvancedFrontOfHouseService frontOfHouseService;
    [SerializeField] private OrderSystem orderSystem;
    [SerializeField] private BistroBuilderCanonicalOrderService canonicalOrderService;
    [SerializeField] private BistroBuilderAdvancedOrderService advancedOrderService;

    [Header("Siguiente jornada")]
    [SerializeField, Range(0, 23)] private int nextDayPreparationHour = 8;
    [SerializeField, Range(0, 59)] private int nextDayPreparationMinute;

    private BistroBuilderEndOfDayHistorySnapshot history = new BistroBuilderEndOfDayHistorySnapshot();
    private readonly List<BistroBuilderInventoryStockSnapshot> stockBuffer = new();
    private readonly List<BistroBuilderCanonicalOrder> orderBuffer = new();
    private readonly HashSet<string> incidentLineIds = new(StringComparer.Ordinal);
    private long openingConsumed;
    private long openingWaste;
    private int openingReputation;
    private int sessionDayIndex;
    private int sessionCalendarYear;
    private int sessionCalendarMonth;
    private int sessionCalendarDay;
    private int satisfactionTotal;
    private int satisfactionCount;
    private int abandonedCount;
    private bool sessionCaptured;

    public event Action<BistroBuilderEndOfDayPhase> PhaseChanged;
    public event Action<BistroBuilderEndOfDaySummary> SummaryReady;
    public event Action HistoryChanged;

    public BistroBuilderEndOfDayPhase Phase { get; private set; } = BistroBuilderEndOfDayPhase.Idle;
    public int RemainingGuestCount => CountRemainingGuests();
    public int RemainingOrderCount => orderSystem != null ? orderSystem.ActiveOrders.Count : 0;
    public int HistoryCount => history?.summaries?.Count ?? 0;

    private void Awake()
    {
        CacheDependencies();
        EnsureHistory();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
        if (serviceStateService != null && serviceStateService.CurrentState == RestaurantServiceState.Open)
            CaptureServiceOpening();
    }

    private void OnDisable() => Unsubscribe();

    private void Update()
    {
        if (!Application.isPlaying || Phase != BistroBuilderEndOfDayPhase.Closing ||
            BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring) return;

        CollectIncidentLines();
        if (CanCompleteOperationalClose())
            serviceStateService.TryCompleteClosing();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (serviceStateService == null || generalGameStateService == null || gameClock == null ||
            financialResultsService == null || inventoryService == null || reputationService == null ||
            experienceTrackingService == null || frontOfHouseService == null || orderSystem == null ||
            canonicalOrderService == null || advancedOrderService == null)
        {
            error = "Bloque 15 necesita servicio, calendario, Finanzas, Inventario, Reputación, Sala y Comandas.";
            return false;
        }
        if (!generalGameStateService.ValidateConfiguration(out error) ||
            !financialResultsService.ValidateConfiguration(out error) ||
            !inventoryService.ValidateConfiguration(out error) ||
            !reputationService.ValidateConfiguration(out error) ||
            !experienceTrackingService.ValidateConfiguration(out error) ||
            !frontOfHouseService.ValidateConfiguration(out error) ||
            !orderSystem.ValidateConfiguration(out error) ||
            !canonicalOrderService.ValidateConfiguration(out error) ||
            !advancedOrderService.ValidateConfiguration(out error))
            return false;
        EnsureHistory();
        return BistroBuilderEndOfDayEngine.TryValidateSnapshot(history, out error);
    }

    public bool TryBeginEndOfService(out string error)
    {
        error = string.Empty;
        if (!ValidateConfiguration(out error)) return false;
        if (serviceStateService.CurrentState != RestaurantServiceState.Open)
        {
            error = "El cierre solo puede iniciarse con el servicio abierto.";
            return false;
        }
        if (!sessionCaptured) CaptureServiceOpening();
        ResolveUnseatedWaitingGuests();
        if (!serviceStateService.TryBeginClosing())
        {
            error = "No se pudo iniciar el cierre operativo.";
            return false;
        }
        SetPhase(BistroBuilderEndOfDayPhase.Closing);
        return true;
    }

    public bool CanCompleteOperationalClose()
    {
        if (serviceStateService == null || serviceStateService.CurrentState != RestaurantServiceState.Closing)
            return false;
        return CountRemainingGuests() == 0 &&
               (orderSystem == null || orderSystem.ActiveOrders.Count == 0) &&
               (experienceTrackingService == null || experienceTrackingService.ActiveVisitCount == 0);
    }

    public bool TryGetLatestSummary(out BistroBuilderEndOfDaySummary summary)
    {
        EnsureHistory();
        summary = history.summaries.Count > 0
            ? history.summaries[history.summaries.Count - 1].DeepClone()
            : null;
        return summary != null;
    }

    public bool TryGetSummary(int dayIndex, out BistroBuilderEndOfDaySummary summary)
    {
        EnsureHistory();
        for (int i = 0; i < history.summaries.Count; i++)
        {
            if (history.summaries[i].dayIndex == dayIndex)
            {
                summary = history.summaries[i].DeepClone();
                return true;
            }
        }
        summary = null;
        return false;
    }

    public bool TryAdvanceToNextDay(out string error)
    {
        error = string.Empty;
        if (Phase != BistroBuilderEndOfDayPhase.SummaryReady || serviceStateService == null ||
            !serviceStateService.IsClosed)
        {
            error = "El día solo avanza después de consolidar y cerrar el servicio.";
            return false;
        }

        DateTime nextDate;
        try
        {
            nextDate = new DateTime(
                generalGameStateService.CalendarYear,
                generalGameStateService.CalendarMonth,
                generalGameStateService.CalendarDay).AddDays(1d);
        }
        catch (Exception exception)
        {
            error = "No se pudo calcular la siguiente fecha: " + exception.Message;
            return false;
        }

        if (!generalGameStateService.TrySetCalendar(
                generalGameStateService.DayIndex + 1,
                nextDate.Year, nextDate.Month, nextDate.Day) ||
            !gameClock.TryRestoreState(
                nextDayPreparationHour, nextDayPreparationMinute,
                1f, false, 0f, true))
        {
            error = "No se pudo avanzar calendario o reloj al siguiente día.";
            return false;
        }

        if (!inventoryService.TryProcessShelfLifeForCurrentDay(out error))
            return false;

        if (!serviceStateService.TryBeginPreparation())
        {
            error = "El nuevo día avanzó, pero no pudo entrar en preparación.";
            return false;
        }

        sessionCaptured = false;
        SetPhase(BistroBuilderEndOfDayPhase.NextDayPrepared);
        return true;
    }

    public BistroBuilderEndOfDayHistorySnapshot CreateSnapshot()
    {
        EnsureHistory();
        BistroBuilderEndOfDayHistorySnapshot snapshot = history.DeepClone();
        snapshot.hasActiveSession = sessionCaptured;
        snapshot.activeDayIndex = sessionCaptured ? sessionDayIndex : 0;
        snapshot.activeCalendarYear = sessionCaptured ? sessionCalendarYear : 0;
        snapshot.activeCalendarMonth = sessionCaptured ? sessionCalendarMonth : 0;
        snapshot.activeCalendarDay = sessionCaptured ? sessionCalendarDay : 0;
        snapshot.openingConsumedCanonicalMilliUnits = sessionCaptured ? openingConsumed : 0L;
        snapshot.openingWasteCanonicalMilliUnits = sessionCaptured ? openingWaste : 0L;
        snapshot.openingReputationBasisPoints = sessionCaptured ? openingReputation : 0;
        snapshot.activeSatisfactionTotalBasisPoints = sessionCaptured ? satisfactionTotal : 0;
        snapshot.activeSatisfactionCount = sessionCaptured ? satisfactionCount : 0;
        snapshot.activeAbandonedCount = sessionCaptured ? abandonedCount : 0;
        snapshot.activeIncidentLineIds.Clear();
        if (sessionCaptured)
            foreach (string lineId in incidentLineIds)
                snapshot.activeIncidentLineIds.Add(lineId);
        snapshot.activeIncidentLineIds.Sort(StringComparer.Ordinal);
        return snapshot;
    }

    public bool TryRestoreSnapshot(BistroBuilderEndOfDayHistorySnapshot snapshot, out string error)
    {
        if (!BistroBuilderEndOfDayEngine.TryValidateSnapshot(snapshot, out error)) return false;
        history = snapshot.DeepClone();
        sessionCaptured = snapshot.hasActiveSession;
        sessionDayIndex = snapshot.activeDayIndex;
        sessionCalendarYear = snapshot.activeCalendarYear;
        sessionCalendarMonth = snapshot.activeCalendarMonth;
        sessionCalendarDay = snapshot.activeCalendarDay;
        openingConsumed = snapshot.openingConsumedCanonicalMilliUnits;
        openingWaste = snapshot.openingWasteCanonicalMilliUnits;
        openingReputation = snapshot.openingReputationBasisPoints;
        satisfactionTotal = snapshot.activeSatisfactionTotalBasisPoints;
        satisfactionCount = snapshot.activeSatisfactionCount;
        abandonedCount = snapshot.activeAbandonedCount;
        incidentLineIds.Clear();
        if (snapshot.activeIncidentLineIds != null)
            for (int i = 0; i < snapshot.activeIncidentLineIds.Count; i++)
                if (!string.IsNullOrWhiteSpace(snapshot.activeIncidentLineIds[i]))
                    incidentLineIds.Add(snapshot.activeIncidentLineIds[i]);
        ReconcileAfterRuntimeLoad();
        HistoryChanged?.Invoke();
        return true;
    }

    public bool TryResetForLegacyLoad(out string error)
    {
        history = new BistroBuilderEndOfDayHistorySnapshot();
        ClearSessionRuntime();
        SetPhase(BistroBuilderEndOfDayPhase.Idle);
        HistoryChanged?.Invoke();
        error = string.Empty;
        return true;
    }

    public void ReconcileAfterRuntimeLoad()
    {
        CacheDependencies();
        if (serviceStateService == null)
        {
            SetPhase(BistroBuilderEndOfDayPhase.Idle);
            return;
        }

        if (serviceStateService.CurrentState == RestaurantServiceState.Open)
        {
            if (!sessionCaptured) CaptureServiceOpening();
            else SetPhase(BistroBuilderEndOfDayPhase.ServiceOpen);
            return;
        }
        if (serviceStateService.CurrentState == RestaurantServiceState.Closing)
        {
            if (!sessionCaptured) CaptureServiceOpening();
            SetPhase(BistroBuilderEndOfDayPhase.Closing);
            return;
        }
        if (serviceStateService.IsClosed && generalGameStateService != null &&
            history != null && history.lastClosedDayIndex == generalGameStateService.DayIndex)
        {
            SetPhase(BistroBuilderEndOfDayPhase.SummaryReady);
            return;
        }
        SetPhase(BistroBuilderEndOfDayPhase.Idle);
    }

    private void CaptureServiceOpening()
    {
        if (generalGameStateService == null || inventoryService == null || reputationService == null) return;
        sessionDayIndex = generalGameStateService.DayIndex;
        sessionCalendarYear = generalGameStateService.CalendarYear;
        sessionCalendarMonth = generalGameStateService.CalendarMonth;
        sessionCalendarDay = generalGameStateService.CalendarDay;
        openingReputation = reputationService.GlobalScoreBasisPoints;
        CaptureInventoryTotals(out openingConsumed, out openingWaste);
        satisfactionTotal = 0;
        satisfactionCount = 0;
        abandonedCount = 0;
        incidentLineIds.Clear();
        CollectIncidentLines();
        sessionCaptured = true;
        SetPhase(BistroBuilderEndOfDayPhase.ServiceOpen);
    }

    private void HandleServiceClosed()
    {
        if (!sessionCaptured) return;
        if (!TryBuildAndStoreSummary(out BistroBuilderEndOfDaySummary summary, out string error))
        {
            Debug.LogError("Bloque 15 no pudo consolidar el cierre: " + error, this);
            return;
        }
        ClearSessionRuntime();
        SetPhase(BistroBuilderEndOfDayPhase.SummaryReady);
        SummaryReady?.Invoke(summary.DeepClone());
    }

    private bool TryBuildAndStoreSummary(out BistroBuilderEndOfDaySummary summary, out string error)
    {
        summary = null;
        error = string.Empty;
        if (!financialResultsService.TryGetDayResult(
                sessionDayIndex, out BistroBuilderDayFinancialResult financial, out error))
            return false;

        CaptureInventoryTotals(out long consumedNow, out long wasteNow);
        CollectIncidentLines();
        summary = new BistroBuilderEndOfDaySummary
        {
            dayIndex = sessionDayIndex,
            calendarYear = sessionCalendarYear,
            calendarMonth = sessionCalendarMonth,
            calendarDay = sessionCalendarDay,
            mealService = financialResultsService.MenuOfferService != null
                ? (int)financialResultsService.MenuOfferService.CurrentMealService : 0,
            closedUtc = DateTime.UtcNow.ToString("O"),
            revenueCents = financial.revenueCents,
            totalExpensesCents = financial.totalPeriodExpensesCents,
            operatingResultCents = financial.operatingResultCents,
            grossProfitCents = financial.grossProfitCents,
            paidOrderCount = financial.paidOrderCount,
            consumedLineCount = financial.consumedLineCount,
            inventoryConsumedCanonicalMilliUnits = Math.Max(0L, consumedNow - openingConsumed),
            inventoryWasteCanonicalMilliUnits = Math.Max(0L, wasteNow - openingWaste),
            servedGroupCount = satisfactionCount,
            abandonedGroupCount = abandonedCount,
            incidentCount = incidentLineIds.Count,
            averageSatisfactionBasisPoints = satisfactionCount > 0
                ? (int)Math.Round(satisfactionTotal / (double)satisfactionCount,
                    MidpointRounding.AwayFromZero) : 0,
            reputationBeforeBasisPoints = openingReputation,
            reputationAfterBasisPoints = reputationService.GlobalScoreBasisPoints
        };
        summary.reputationDeltaBasisPoints =
            summary.reputationAfterBasisPoints - summary.reputationBeforeBasisPoints;
        summary.performanceBasisPoints = BistroBuilderEndOfDayEngine.ComputePerformanceBasisPoints(
            summary.operatingResultCents, summary.revenueCents,
            summary.averageSatisfactionBasisPoints, summary.abandonedGroupCount, summary.incidentCount);

        BistroBuilderEndOfDaySummary previous = FindPreviousSummary(summary.dayIndex);
        if (previous != null)
        {
            summary.hasPreviousDayComparison = true;
            summary.revenueDeltaVsPreviousCents = summary.revenueCents - previous.revenueCents;
            summary.operatingResultDeltaVsPreviousCents =
                summary.operatingResultCents - previous.operatingResultCents;
            summary.satisfactionDeltaVsPreviousBasisPoints =
                summary.averageSatisfactionBasisPoints - previous.averageSatisfactionBasisPoints;
            summary.performanceDeltaVsPreviousBasisPoints =
                summary.performanceBasisPoints - previous.performanceBasisPoints;
        }
        BistroBuilderEndOfDayEngine.BuildGuidance(summary);
        if (!BistroBuilderEndOfDayEngine.TryValidateSummary(summary, out error)) return false;
        StoreSummary(summary);
        return true;
    }

    private void StoreSummary(BistroBuilderEndOfDaySummary summary)
    {
        EnsureHistory();
        int existing = -1;
        for (int i = 0; i < history.summaries.Count; i++)
            if (history.summaries[i].dayIndex == summary.dayIndex) { existing = i; break; }
        if (existing >= 0) history.summaries[existing] = summary.DeepClone();
        else history.summaries.Add(summary.DeepClone());
        history.summaries.Sort((a, b) => a.dayIndex.CompareTo(b.dayIndex));
        while (history.summaries.Count > 365) history.summaries.RemoveAt(0);
        history.lastClosedDayIndex = history.summaries.Count > 0
            ? history.summaries[history.summaries.Count - 1].dayIndex : 0;
        history.revision++;
        HistoryChanged?.Invoke();
    }

    private BistroBuilderEndOfDaySummary FindPreviousSummary(int dayIndex)
    {
        EnsureHistory();
        BistroBuilderEndOfDaySummary result = null;
        for (int i = 0; i < history.summaries.Count; i++)
        {
            BistroBuilderEndOfDaySummary item = history.summaries[i];
            if (item != null && item.dayIndex < dayIndex &&
                (result == null || item.dayIndex > result.dayIndex)) result = item;
        }
        return result;
    }

    private void CaptureInventoryTotals(out long consumed, out long waste)
    {
        consumed = 0L;
        waste = 0L;
        stockBuffer.Clear();
        inventoryService.CopyStockSnapshotsTo(stockBuffer);
        for (int i = 0; i < stockBuffer.Count; i++)
        {
            consumed += Math.Max(0L, stockBuffer[i].ConsumedCanonicalMilliUnits);
            waste += Math.Max(0L, stockBuffer[i].WastedCanonicalMilliUnits) +
                     Math.Max(0L, stockBuffer[i].ExpiredCanonicalMilliUnits);
        }
    }

    private void CollectIncidentLines()
    {
        if (canonicalOrderService == null) return;
        canonicalOrderService.CopyOrderSnapshotsTo(orderBuffer);
        for (int i = 0; i < orderBuffer.Count; i++)
        {
            BistroBuilderCanonicalOrder order = orderBuffer[i];
            if (order == null || order.Lines == null) continue;
            for (int line = 0; line < order.Lines.Count; line++)
            {
                BistroBuilderCanonicalOrderLine item = order.Lines[line];
                if (item != null && item.AdvancedIncidentKind != BistroBuilderAdvancedOrderIncidentKind.None)
                    incidentLineIds.Add(item.LineId);
            }
        }
    }

    private void ResolveUnseatedWaitingGuests()
    {
        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < groups.Length; i++)
        {
            CustomerGroup group = groups[i];
            if (group == null || group.HasAssignedTable || group.HasAssignedBarSpot) continue;
            if (group.CurrentState == CustomerGroupState.WaitingForTable)
                group.SetState(CustomerGroupState.Leaving);
        }
    }

    private int CountRemainingGuests()
    {
        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        int count = 0;
        for (int i = 0; i < groups.Length; i++)
            if (groups[i] != null && groups[i].CurrentState != CustomerGroupState.Finished) count++;
        return count;
    }

    private void HandleOutcome(BistroBuilderAdvancedCustomerVisitOutcome outcome)
    {
        if (!sessionCaptured || outcome == null || outcome.dayIndex != sessionDayIndex) return;
        satisfactionTotal += Mathf.Clamp(outcome.overallSatisfactionBasisPoints, 0, 10000);
        satisfactionCount++;
    }

    private void HandleAbandoned(int _) { if (sessionCaptured) abandonedCount++; }
    private void HandleAdvancedOrdersChanged() => CollectIncidentLines();
    private void HandleServiceOpened() => CaptureServiceOpening();

    private void Subscribe()
    {
        if (serviceStateService != null)
        {
            serviceStateService.ServiceOpened += HandleServiceOpened;
            serviceStateService.ServiceClosed += HandleServiceClosed;
        }
        if (experienceTrackingService != null)
            experienceTrackingService.AdvancedVisitOutcomeCompleted += HandleOutcome;
        if (frontOfHouseService != null)
            frontOfHouseService.GroupAbandoned += HandleAbandoned;
        if (advancedOrderService != null)
            advancedOrderService.AdvancedOrdersChanged += HandleAdvancedOrdersChanged;
    }

    private void Unsubscribe()
    {
        if (serviceStateService != null)
        {
            serviceStateService.ServiceOpened -= HandleServiceOpened;
            serviceStateService.ServiceClosed -= HandleServiceClosed;
        }
        if (experienceTrackingService != null)
            experienceTrackingService.AdvancedVisitOutcomeCompleted -= HandleOutcome;
        if (frontOfHouseService != null)
            frontOfHouseService.GroupAbandoned -= HandleAbandoned;
        if (advancedOrderService != null)
            advancedOrderService.AdvancedOrdersChanged -= HandleAdvancedOrdersChanged;
    }

    private void SetPhase(BistroBuilderEndOfDayPhase value)
    {
        if (Phase == value) return;
        Phase = value;
        PhaseChanged?.Invoke(value);
    }

    private void ClearSessionRuntime()
    {
        sessionCaptured = false;
        sessionDayIndex = 0;
        sessionCalendarYear = 0;
        sessionCalendarMonth = 0;
        sessionCalendarDay = 0;
        openingConsumed = 0L;
        openingWaste = 0L;
        openingReputation = 0;
        satisfactionTotal = 0;
        satisfactionCount = 0;
        abandonedCount = 0;
        incidentLineIds.Clear();
    }

    private void EnsureHistory()
    {
        if (history == null) history = new BistroBuilderEndOfDayHistorySnapshot();
        if (history.summaries == null) history.summaries = new List<BistroBuilderEndOfDaySummary>();
    }

    private void CacheDependencies()
    {
        if (serviceStateService == null) TryGetComponent(out serviceStateService);
        if (generalGameStateService == null) TryGetComponent(out generalGameStateService);
        if (gameClock == null) TryGetComponent(out gameClock);
        if (financialResultsService == null) TryGetComponent(out financialResultsService);
        if (inventoryService == null) TryGetComponent(out inventoryService);
        if (reputationService == null) TryGetComponent(out reputationService);
        if (experienceTrackingService == null) TryGetComponent(out experienceTrackingService);
        if (frontOfHouseService == null) TryGetComponent(out frontOfHouseService);
        if (orderSystem == null) TryGetComponent(out orderSystem);
        if (canonicalOrderService == null) TryGetComponent(out canonicalOrderService);
        if (advancedOrderService == null) TryGetComponent(out advancedOrderService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
