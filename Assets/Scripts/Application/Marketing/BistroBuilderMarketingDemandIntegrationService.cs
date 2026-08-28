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
    [SerializeField] private BistroBuilderMenuPortfolioService menuPortfolioService;
    [SerializeField] private BistroBuilderReservationService reservationService;
    [SerializeField] private BistroBuilderReservationAvailabilityService
        reservationAvailabilityService;

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
            customerGroupSpawner == null || menuPortfolioService == null ||
            reservationService == null ||
            reservationAvailabilityService == null)
        {
            error = "7B necesita Marketing, calendario, servicio, clientes y Reservas.";
            return false;
        }

        if (!marketingService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !menuPortfolioService.ValidateConfiguration(out error) ||
            !reservationService.ValidateConfiguration(out error) ||
            !reservationAvailabilityService.ValidateConfiguration(out error))
            return false;

        if (customerGroupSpawner.BaselineGroupCount < 1)
        {
            error = "El flujo de walk-ins no expone una demanda base válida.";
            return false;
        }

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
            BistroBuilderCustomerDemandPlan plan = BuildCustomerDemandPlan(
                projection,
                globalEffects);
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

        return BistroBuilderMarketingDemandEngine.TryBuildProjection(
            customerGroupSpawner.BaselineGroupCount,
            globalEffects,
            segmentEffects,
            out projection,
            out error);
    }

    private BistroBuilderCustomerDemandPlan BuildCustomerDemandPlan(
        BistroBuilderMarketingDemandProjection projection,
        BistroBuilderMarketingEffectSnapshot globalEffects)
    {
        string planId = "marketing.demand.day" +
            generalGameStateService.DayIndex + ".rev" + marketingService.Revision;
        var plan = new BistroBuilderCustomerDemandPlan
        {
            planId = planId,
            walkInGroupCount = projection.adjustedWalkInGroups,
            profiles = new List<BistroBuilderCustomerAcquisitionProfile>()
        };

        for (int index = 0; index < projection.walkInSegments.Count; index++)
        {
            BistroBuilderMarketingCustomerSegment segment =
                projection.walkInSegments[index];
            BistroBuilderMarketingEffectSnapshot effects = segmentEffects[segment];
            bool influenced =
                globalEffects.overallDemandBasisPoints != 0 ||
                globalEffects.walkInDemandBasisPoints != 0 ||
                effects.overallDemandBasisPoints !=
                    globalEffects.overallDemandBasisPoints ||
                effects.walkInDemandBasisPoints !=
                    globalEffects.walkInDemandBasisPoints;

            plan.profiles.Add(new BistroBuilderCustomerAcquisitionProfile
            {
                segmentId = segment.ToString().ToLowerInvariant(),
                sourceSystemId = influenced
                    ? BistroBuilderMarketingService.FinanceSourceSystemId
                    : "service.baseline",
                sourceReferenceId = influenced ? planId : string.Empty,
                marketingInfluenced = influenced
            });
        }
        return plan;
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
            generalGameStateService.CalendarChanged -= HandleCalendarChanged;
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
        if (menuPortfolioService == null)
            TryGetComponent(out menuPortfolioService);
        if (reservationService == null)
            TryGetComponent(out reservationService);
        if (reservationAvailabilityService == null)
            TryGetComponent(out reservationAvailabilityService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
