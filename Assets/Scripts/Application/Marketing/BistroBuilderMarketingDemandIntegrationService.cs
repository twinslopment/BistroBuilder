using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 7B — Puente entre Marketing y los propietarios reales de demanda.
/// Marketing proyecta; CustomerGroupSpawner y Reservas siguen materializando.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Marketing/Marketing Demand Integration")]
public sealed class BistroBuilderMarketingDemandIntegrationService : MonoBehaviour
{
    [SerializeField] private BistroBuilderMarketingService marketingService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private GameClock gameClock;
    [SerializeField] private RestaurantServiceStateService serviceStateService;
    [SerializeField] private CustomerGroupSpawner customerGroupSpawner;
    [SerializeField] private BistroBuilderDynamicDemandService dynamicDemandService;
    [SerializeField] private BistroBuilderMenuPortfolioService menuPortfolioService;
    [SerializeField] private BistroBuilderReservationService reservationService;
    [SerializeField] private BistroBuilderReservationAvailabilityService
        reservationAvailabilityService;
    [SerializeField] private BistroBuilderGuestRelationsService guestRelationsService;
    [SerializeField] private BistroBuilderReputationService reputationService;

    private readonly List<BistroBuilderGuestVisitCohortRecord> eligibleCohorts =
        new List<BistroBuilderGuestVisitCohortRecord>(32);
    private readonly HashSet<string> selectedReturnCohortIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<int> replacedReturnSlots = new HashSet<int>();

    private readonly Dictionary<BistroBuilderMarketingCustomerSegment,
        BistroBuilderMarketingEffectSnapshot> segmentEffects =
            new Dictionary<BistroBuilderMarketingCustomerSegment,
                BistroBuilderMarketingEffectSnapshot>();

    private BistroBuilderMarketingDemandProjection lastProjection;
    private int reservationLeadDay;
    private int reservationLeadsGeneratedForDay;

    private static readonly string[] GeneratedGuestNames =
    {
        "Lucía Martín", "Carlos García", "Elena Alonso", "Diego Pérez",
        "Marta Fernández", "Pablo Suárez", "Sara López", "Álvaro Ramos"
    };

    public event Action DemandProjectionChanged;

    public BistroBuilderMarketingDemandProjection LastProjection =>
        lastProjection != null ? lastProjection.DeepClone() : null;
    public int GeneratedReservationLeadsToday =>
        reservationLeadsGeneratedForDay;
    public int ReservationLeadDay => reservationLeadDay;

    public bool TryCapturePersistenceState(
        out int leadDay,
        out int leadCount,
        out string error)
    {
        leadDay = reservationLeadDay;
        leadCount = reservationLeadsGeneratedForDay;
        if (!ValidateConfiguration(out error))
            return false;
        error = string.Empty;
        return true;
    }

    public bool TryRestorePersistenceState(
        int leadDay,
        int leadCount,
        out string error)
    {
        if (leadDay < 0 || leadCount < 0 || leadCount > 3 ||
            (leadDay == 0 && leadCount != 0))
        {
            error = "El estado de leads de Marketing es inválido.";
            return false;
        }

        reservationLeadDay = leadDay;
        reservationLeadsGeneratedForDay = leadCount;
        lastProjection = null;
        error = string.Empty;
        return true;
    }

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
        if (serviceStateService != null &&
            !serviceStateService.AcceptsNewCustomers)
            TryRefreshDemandForNextService(out _);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (marketingService == null || generalGameStateService == null ||
            gameClock == null || serviceStateService == null ||
            customerGroupSpawner == null || dynamicDemandService == null ||
            menuPortfolioService == null ||
            reservationService == null ||
            reservationAvailabilityService == null ||
            guestRelationsService == null || reputationService == null)
        {
            error = "7B necesita Marketing, calendario, servicio, clientes y Reservas.";
            return false;
        }

        if (!marketingService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !menuPortfolioService.ValidateConfiguration(out error) ||
            !reservationService.ValidateConfiguration(out error) ||
            !reservationAvailabilityService.ValidateConfiguration(out error) ||
            !guestRelationsService.ValidateConfiguration(out error) ||
            !reputationService.ValidateConfiguration(out error) ||
            !dynamicDemandService.ValidateConfiguration(out error))
            return false;

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Recalcula perfiles y número de walk-ins para el próximo servicio y
    /// materializa únicamente el incremento de reservas atribuible a Marketing.
    /// </summary>
    public bool TryRefreshDemandForNextService(out string error)
    {
        if (!TryBuildProjection(out BistroBuilderMarketingDemandProjection projection,
                out BistroBuilderMarketingEffectSnapshot globalEffects,
                out error))
            return false;

        lastProjection = projection.DeepClone();
        if (!serviceStateService.AcceptsNewCustomers)
        {
            if (!TryBuildCustomerDemandPlan(
                    projection,
                    globalEffects,
                    out BistroBuilderCustomerDemandPlan plan,
                    out error))
                return false;
            if (!customerGroupSpawner.TryQueueDemandPlanForNextService(
                    plan,
                    out error))
                return false;
        }

        if (!TryGenerateIncrementalReservations(projection, out _, out error))
            return false;

        DemandProjectionChanged?.Invoke();
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Recompone la proyección tras Save/Load. Con servicio cerrado vuelve a
    /// publicar el plan del próximo servicio sin duplicar leads ya guardados;
    /// con servicio activo respeta íntegramente service.runtime.
    /// </summary>
    public bool TrySynchronizeAfterLoad(out string error)
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
        {
            error = "La carga de service.runtime todavía no ha finalizado.";
            return false;
        }

        if (!serviceStateService.AcceptsNewCustomers)
            return TryRefreshDemandForNextService(out error);

        if (!TryBuildProjection(out BistroBuilderMarketingDemandProjection projection,
                out error))
            return false;

        lastProjection = projection.DeepClone();
        DemandProjectionChanged?.Invoke();
        error = string.Empty;
        return true;
    }

    public bool TryBuildProjection(
        out BistroBuilderMarketingDemandProjection projection,
        out string error)
    {
        return TryBuildProjection(out projection, out _, out error);
    }

    private bool TryBuildProjection(
        out BistroBuilderMarketingDemandProjection projection,
        out BistroBuilderMarketingEffectSnapshot globalEffects,
        out string error)
    {
        projection = null;
        globalEffects = null;
        if (!ValidateConfiguration(out error))
            return false;

        if (!dynamicDemandService.TryBuildProjection(
                out BistroBuilderDynamicDemandProjection baseProjection,
                out error))
            return false;

        int dayIndex = generalGameStateService.DayIndex;
        int minute = gameClock.Hour * 60 + gameClock.Minute;
        BistroBuilderMarketingDayPart dayPart =
            BistroBuilderMarketingDemandEngine.ResolveDayPart(minute);

        if (!marketingService.TryEvaluateEffects(
                new BistroBuilderMarketingEffectQuery
                {
                    dayIndex = dayIndex,
                    segment = BistroBuilderMarketingCustomerSegment.Any,
                    dayPart = dayPart
                },
                out globalEffects,
                out error))
            return false;

        string activeMenuId = BistroBuilderMenuIdUtility.NormalizeStableId(
            menuPortfolioService.ActiveMenuId
        );
        if (BistroBuilderMenuIdUtility.IsValidStableId(activeMenuId))
        {
            if (!marketingService.TryEvaluateEffects(
                    new BistroBuilderMarketingEffectQuery
                    {
                        dayIndex = dayIndex,
                        segment = BistroBuilderMarketingCustomerSegment.Any,
                        dayPart = dayPart,
                        targetId = activeMenuId
                    },
                    out BistroBuilderMarketingEffectSnapshot menuEffects,
                    out error))
                return false;

            globalEffects.overallDemandBasisPoints +=
                menuEffects.targetDemandBasisPoints;
        }

        segmentEffects.Clear();
        IReadOnlyList<BistroBuilderMarketingCustomerSegment> segments =
            BistroBuilderMarketingDemandEngine.Segments;
        for (int index = 0; index < segments.Count; index++)
        {
            BistroBuilderMarketingCustomerSegment segment = segments[index];
            if (!marketingService.TryEvaluateEffects(
                    new BistroBuilderMarketingEffectQuery
                    {
                        dayIndex = dayIndex,
                        segment = segment,
                        dayPart = dayPart
                    },
                    out BistroBuilderMarketingEffectSnapshot effects,
                    out error))
                return false;

            if (BistroBuilderMenuIdUtility.IsValidStableId(activeMenuId))
            {
                if (!marketingService.TryEvaluateEffects(
                        new BistroBuilderMarketingEffectQuery
                        {
                            dayIndex = dayIndex,
                            segment = segment,
                            dayPart = dayPart,
                            targetId = activeMenuId
                        },
                        out BistroBuilderMarketingEffectSnapshot menuEffects,
                        out error))
                    return false;

                effects.overallDemandBasisPoints +=
                    menuEffects.targetDemandBasisPoints;
            }

            segmentEffects.Add(segment, effects);
        }

        int reputationDemandBasisPoints =
            reputationService.PersistentDemandBasisPoints;
        if (reputationDemandBasisPoints != 0)
        {
            globalEffects.overallDemandBasisPoints += reputationDemandBasisPoints;
            foreach (KeyValuePair<BistroBuilderMarketingCustomerSegment,
                     BistroBuilderMarketingEffectSnapshot> pair in segmentEffects)
            {
                pair.Value.overallDemandBasisPoints +=
                    reputationDemandBasisPoints;
            }
        }

        return BistroBuilderMarketingDemandEngine.TryBuildProjection(
            baseProjection.baseWalkInGroups,
            globalEffects,
            segmentEffects,
            out projection,
            out error);
    }

    private bool TryBuildCustomerDemandPlan(
        BistroBuilderMarketingDemandProjection projection,
        BistroBuilderMarketingEffectSnapshot globalEffects,
        out BistroBuilderCustomerDemandPlan plan,
        out string error)
    {
        plan = null;
        error = string.Empty;
        if (projection == null || globalEffects == null)
        {
            error = "No existe una proyección válida para componer demanda.";
            return false;
        }

        string planId = "demand.day" + generalGameStateService.DayIndex +
            ".h" + gameClock.Hour + ".base" + projection.baselineWalkInGroups +
            ".mrev" + marketingService.Revision;
        plan = new BistroBuilderCustomerDemandPlan
        {
            planId = planId,
            walkInGroupCount = projection.adjustedWalkInGroups,
            profiles = new List<BistroBuilderCustomerAcquisitionProfile>(),
            arrivalDelaySeconds = new List<float>()
        };

        int reputationBasisPoints = reputationService != null
            ? reputationService.PersistentDemandBasisPoints
            : 0;
        int directGlobalOverall =
            globalEffects.overallDemandBasisPoints - reputationBasisPoints;

        for (int index = 0; index < projection.walkInSegments.Count; index++)
        {
            BistroBuilderMarketingCustomerSegment segment =
                projection.walkInSegments[index];
            BistroBuilderMarketingEffectSnapshot effects = segmentEffects[segment];
            int directSegmentOverall =
                effects.overallDemandBasisPoints - reputationBasisPoints;
            bool influenced =
                directGlobalOverall != 0 ||
                globalEffects.walkInDemandBasisPoints != 0 ||
                directSegmentOverall != directGlobalOverall ||
                effects.walkInDemandBasisPoints !=
                    globalEffects.walkInDemandBasisPoints;

            plan.profiles.Add(new BistroBuilderCustomerAcquisitionProfile
            {
                segmentId = segment.ToString().ToLowerInvariant(),
                sourceSystemId = influenced
                    ? BistroBuilderMarketingService.FinanceSourceSystemId
                    : "service.dynamic_demand",
                sourceReferenceId = planId,
                marketingInfluenced = influenced,
                discoverySourceId = influenced ? "marketing" : "organic"
            });
        }

        if (!dynamicDemandService.TryBuildArrivalDelays(
                projection.adjustedWalkInGroups,
                plan.arrivalDelaySeconds,
                out error))
            return false;

        ApplyReputationDiscoveryAttribution(
            plan,
            reputationBasisPoints,
            projection.baselineWalkInGroups);
        ApplyRepeatVisitSubstitutions(plan, globalEffects);
        if (!plan.TryValidate(out error))
            return false;
        return true;
    }
    private void ApplyReputationDiscoveryAttribution(
        BistroBuilderCustomerDemandPlan plan,
        int reputationBasisPoints,
        int baselineWalkInGroups)
    {
        if (plan == null || reputationBasisPoints <= 0 ||
            customerGroupSpawner == null || plan.profiles == null)
            return;

        int remaining = Math.Max(
            0,
            plan.walkInGroupCount - Math.Max(1, baselineWalkInGroups));
        for (int index = plan.profiles.Count - 1;
             index >= 0 && remaining > 0;
             index--)
        {
            BistroBuilderCustomerAcquisitionProfile profile = plan.profiles[index];
            if (profile == null || profile.marketingInfluenced)
                continue;
            profile.sourceSystemId = "reputation.word_of_mouth";
            profile.sourceReferenceId = plan.planId;
            profile.discoverySourceId = "word_of_mouth";
            remaining--;
        }
    }

    private void ApplyRepeatVisitSubstitutions(
        BistroBuilderCustomerDemandPlan plan,
        BistroBuilderMarketingEffectSnapshot globalEffects)
    {
        if (plan == null || globalEffects == null ||
            guestRelationsService == null || plan.profiles.Count == 0)
            return;

        guestRelationsService.CopyEligibleCohorts(
            generalGameStateService.DayIndex,
            eligibleCohorts);
        if (eligibleCohorts.Count == 0)
            return;

        selectedReturnCohortIds.Clear();
        replacedReturnSlots.Clear();

        int globalRepeat = Math.Max(0, globalEffects.repeatVisitBasisPoints);
        int globalCount =
            BistroBuilderGuestRelationsEngine.ConvertRepeatVisitBasisPointsToCount(
                globalRepeat,
                eligibleCohorts.Count,
                plan.profiles.Count);
        ReplaceReturnProfiles(
            plan,
            BistroBuilderMarketingCustomerSegment.Any,
            globalCount,
            true);

        int organicRepeat = reputationService != null
            ? reputationService.OrganicRepeatVisitBasisPoints
            : 0;
        int organicCount =
            BistroBuilderGuestRelationsEngine.ConvertRepeatVisitBasisPointsToCount(
                organicRepeat,
                Math.Max(0, eligibleCohorts.Count - selectedReturnCohortIds.Count),
                Math.Max(0, plan.profiles.Count - replacedReturnSlots.Count));
        ReplaceReturnProfiles(
            plan,
            BistroBuilderMarketingCustomerSegment.Any,
            organicCount,
            false);

        IReadOnlyList<BistroBuilderMarketingCustomerSegment> segments =
            BistroBuilderMarketingDemandEngine.Segments;
        for (int index = 0; index < segments.Count; index++)
        {
            if (replacedReturnSlots.Count >= plan.profiles.Count ||
                replacedReturnSlots.Count >=
                    BistroBuilderGuestRelationsEngine.MaximumReturnGroupsPerService)
                break;

            BistroBuilderMarketingCustomerSegment segment = segments[index];
            int specific = Math.Max(
                0,
                segmentEffects[segment].repeatVisitBasisPoints - globalRepeat);
            int availableSlots = plan.profiles.Count - replacedReturnSlots.Count;
            int eligible = CountEligibleCohorts(segment);
            int count =
                BistroBuilderGuestRelationsEngine.ConvertRepeatVisitBasisPointsToCount(
                    specific,
                    eligible,
                    availableSlots);
            ReplaceReturnProfiles(plan, segment, count, true);
        }
    }

    private int CountEligibleCohorts(
        BistroBuilderMarketingCustomerSegment segment)
    {
        int count = 0;
        for (int index = 0; index < eligibleCohorts.Count; index++)
        {
            BistroBuilderGuestVisitCohortRecord cohort = eligibleCohorts[index];
            if (cohort == null ||
                selectedReturnCohortIds.Contains(cohort.cohortId))
                continue;
            if (segment == BistroBuilderMarketingCustomerSegment.Any ||
                IsCohortSegment(cohort, segment))
                count++;
        }
        return count;
    }
    private void ReplaceReturnProfiles(
        BistroBuilderCustomerDemandPlan plan,
        BistroBuilderMarketingCustomerSegment requiredSegment,
        int count,
        bool marketingDriven)
    {
        for (int replacement = 0; replacement < count; replacement++)
        {
            BistroBuilderGuestVisitCohortRecord cohort =
                FindNextEligibleCohort(requiredSegment);
            if (cohort == null)
                return;

            int slot = FindReplacementSlot(plan, requiredSegment);
            if (slot < 0)
                return;

            plan.profiles[slot] = new BistroBuilderCustomerAcquisitionProfile
            {
                segmentId = cohort.segmentId,
                sourceSystemId = marketingDriven
                    ? BistroBuilderMarketingService.FinanceSourceSystemId
                    : "reputation.returning",
                sourceReferenceId = plan.planId,
                marketingInfluenced = marketingDriven,
                discoverySourceId = "returning_guest",
                returningVisit = true,
                guestRelationsReferenceId = cohort.cohortId,
                preferredGroupSize = cohort.partySize
            };
            selectedReturnCohortIds.Add(cohort.cohortId);
            replacedReturnSlots.Add(slot);
        }
    }

    private BistroBuilderGuestVisitCohortRecord FindNextEligibleCohort(
        BistroBuilderMarketingCustomerSegment requiredSegment)
    {
        for (int index = 0; index < eligibleCohorts.Count; index++)
        {
            BistroBuilderGuestVisitCohortRecord cohort = eligibleCohorts[index];
            if (cohort == null ||
                selectedReturnCohortIds.Contains(cohort.cohortId))
                continue;
            if (requiredSegment != BistroBuilderMarketingCustomerSegment.Any &&
                !IsCohortSegment(cohort, requiredSegment))
                continue;
            return cohort;
        }
        return null;
    }

    private int FindReplacementSlot(
        BistroBuilderCustomerDemandPlan plan,
        BistroBuilderMarketingCustomerSegment requiredSegment)
    {
        string requiredId = requiredSegment == BistroBuilderMarketingCustomerSegment.Any
            ? string.Empty
            : requiredSegment.ToString().ToLowerInvariant();

        for (int index = 0; index < plan.profiles.Count; index++)
        {
            if (replacedReturnSlots.Contains(index))
                continue;
            if (requiredId.Length == 0 ||
                string.Equals(
                    plan.profiles[index].segmentId,
                    requiredId,
                    StringComparison.OrdinalIgnoreCase))
                return index;
        }
        return -1;
    }

    private static bool IsCohortSegment(
        BistroBuilderGuestVisitCohortRecord cohort,
        BistroBuilderMarketingCustomerSegment segment)
    {
        return cohort != null && string.Equals(
            cohort.segmentId,
            segment.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }
    private bool TryGenerateIncrementalReservations(
        BistroBuilderMarketingDemandProjection projection,
        out int generatedNow,
        out string error)
    {
        generatedNow = 0;
        error = string.Empty;
        int dayIndex = generalGameStateService.DayIndex;
        if (reservationLeadDay != dayIndex)
        {
            reservationLeadDay = dayIndex;
            reservationLeadsGeneratedForDay = 0;
        }

        while (reservationLeadsGeneratedForDay <
               projection.reservationLeadCount)
        {
            int sequence = reservationLeadsGeneratedForDay;
            BistroBuilderMarketingCustomerSegment segment =
                projection.reservationSegments[sequence];
            if (!TryCreateMarketingReservation(
                    segment,
                    sequence,
                    out bool created,
                    out error))
                return false;

            if (!created)
                break;

            reservationLeadsGeneratedForDay++;
            generatedNow++;
        }

        error = string.Empty;
        return true;
    }

    private bool TryCreateMarketingReservation(
        BistroBuilderMarketingCustomerSegment segment,
        int sequence,
        out bool created,
        out string error)
    {
        created = false;
        BistroBuilderReservationDraft draft = BuildReservationDraft(
            segment,
            sequence);

        if (!reservationAvailabilityService.TryFindBestTable(
                draft,
                string.Empty,
                out BistroBuilderReservationTableAvailability selected,
                out _))
        {
            // La demanda existe, pero Reservas conserva autoridad de capacidad.
            error = string.Empty;
            return true;
        }

        if (!reservationService.TryCreateReservation(
                draft,
                out BistroBuilderReservationRecord pending,
                out error))
            return false;

        if (!reservationAvailabilityService.TryAssignSpecificTable(
                pending.reservationId,
                selected.tableId,
                out _,
                out error))
        {
            reservationService.TryCancel(
                pending.reservationId,
                out _,
                out _);
            return false;
        }

        created = true;
        Debug.Log(
            "7B generó una reserva atribuible a Marketing para " +
            draft.guestName + " (segmento " + segment + ").",
            this);
        return true;
    }

    private BistroBuilderReservationDraft BuildReservationDraft(
        BistroBuilderMarketingCustomerSegment segment,
        int sequence)
    {
        int dayIndex = generalGameStateService.DayIndex + 1;
        int nameIndex = Math.Abs(
            dayIndex * 17 + sequence * 7 + (int)segment) %
            GeneratedGuestNames.Length;

        int partySize = segment == BistroBuilderMarketingCustomerSegment.Groups
            ? 4
            : segment == BistroBuilderMarketingCustomerSegment.Workers
                ? 1
                : 2;
        int arrivalMinute = ResolveReservationMinute(segment, sequence);

        return new BistroBuilderReservationDraft
        {
            guestName = GeneratedGuestNames[nameIndex],
            partySize = partySize,
            dayIndex = dayIndex,
            arrivalMinute = arrivalMinute,
            durationMinutes = 120,
            notes = string.Empty
        };
    }

    private static int ResolveReservationMinute(
        BistroBuilderMarketingCustomerSegment segment,
        int sequence)
    {
        int offset = sequence * 30;
        switch (segment)
        {
            case BistroBuilderMarketingCustomerSegment.Workers:
            case BistroBuilderMarketingCustomerSegment.Traditional:
            case BistroBuilderMarketingCustomerSegment.LocalResidents:
            case BistroBuilderMarketingCustomerSegment.PriceSensitive:
                return 780 + offset;
            default:
                return 1200 + offset;
        }
    }

    private void HandleServiceOpeningRequested()
    {
        TryRefreshDemandForNextService(out string error);
        if (!string.IsNullOrEmpty(error))
            Debug.LogError("7B no pudo preparar la demanda: " + error, this);
    }

    private void HandleMarketingChanged(long revision)
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            return;

        if (serviceStateService != null &&
            !serviceStateService.AcceptsNewCustomers)
            TryRefreshDemandForNextService(out _);
    }

    private void HandleMarketingRestored()
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            return;

        reservationLeadDay = 0;
        reservationLeadsGeneratedForDay = 0;
        if (serviceStateService != null &&
            !serviceStateService.AcceptsNewCustomers)
            TryRefreshDemandForNextService(out _);
    }

    private void HandleCalendarChanged()
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            return;

        reservationLeadDay = 0;
        reservationLeadsGeneratedForDay = 0;
        if (serviceStateService != null &&
            !serviceStateService.AcceptsNewCustomers)
            TryRefreshDemandForNextService(out _);
    }

    private void HandleBaseDemandChanged()
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            return;
        if (serviceStateService != null && !serviceStateService.AcceptsNewCustomers)
            TryRefreshDemandForNextService(out _);
    }

    private void HandleReputationChanged(long revision) => HandleBaseDemandChanged();
    private void Subscribe()
    {
        if (serviceStateService != null)
        {
            serviceStateService.ServiceOpeningRequested -=
                HandleServiceOpeningRequested;
            serviceStateService.ServiceOpeningRequested +=
                HandleServiceOpeningRequested;
        }
        if (marketingService != null)
        {
            marketingService.MarketingChanged -= HandleMarketingChanged;
            marketingService.MarketingChanged += HandleMarketingChanged;
            marketingService.MarketingRestored -= HandleMarketingRestored;
            marketingService.MarketingRestored += HandleMarketingRestored;
        }
        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleCalendarChanged;
            generalGameStateService.CalendarChanged += HandleCalendarChanged;
            generalGameStateService.ProgressionChanged -= HandleBaseDemandChanged;
            generalGameStateService.ProgressionChanged += HandleBaseDemandChanged;
        }
        if (reputationService != null)
        {
            reputationService.ReputationChanged -= HandleReputationChanged;
            reputationService.ReputationChanged += HandleReputationChanged;
            reputationService.ReputationRestored -= HandleBaseDemandChanged;
            reputationService.ReputationRestored += HandleBaseDemandChanged;
        }
    }

    private void Unsubscribe()
    {
        if (serviceStateService != null)
            serviceStateService.ServiceOpeningRequested -=
                HandleServiceOpeningRequested;
        if (marketingService != null)
        {
            marketingService.MarketingChanged -= HandleMarketingChanged;
            marketingService.MarketingRestored -= HandleMarketingRestored;
        }
        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleCalendarChanged;
            generalGameStateService.ProgressionChanged -= HandleBaseDemandChanged;
        }
        if (reputationService != null)
        {
            reputationService.ReputationChanged -= HandleReputationChanged;
            reputationService.ReputationRestored -= HandleBaseDemandChanged;
        }
    }

    private void CacheDependencies()
    {
        if (marketingService == null)
            TryGetComponent(out marketingService);
        if (generalGameStateService == null)
            TryGetComponent(out generalGameStateService);
        if (gameClock == null)
            gameClock = FindFirstObjectByType<GameClock>();
        if (serviceStateService == null)
            serviceStateService = FindFirstObjectByType<RestaurantServiceStateService>();
        if (customerGroupSpawner == null)
            customerGroupSpawner = FindFirstObjectByType<CustomerGroupSpawner>();
        if (dynamicDemandService == null)
            TryGetComponent(out dynamicDemandService);
        if (menuPortfolioService == null)
            TryGetComponent(out menuPortfolioService);
        if (reservationService == null)
            TryGetComponent(out reservationService);
        if (reservationAvailabilityService == null)
            TryGetComponent(out reservationAvailabilityService);
        if (guestRelationsService == null)
            TryGetComponent(out guestRelationsService);
        if (reputationService == null)
            TryGetComponent(out reputationService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
