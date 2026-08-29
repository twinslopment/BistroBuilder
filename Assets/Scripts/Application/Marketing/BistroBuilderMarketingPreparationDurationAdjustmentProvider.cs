using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Traduce OperationalPressure de Marketing al puerto neutral de 367D.
/// No modifica recetas, estados de comanda ni la cola de cocina.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Marketing/Marketing Preparation Duration Adjustment Provider"
)]
public sealed class BistroBuilderMarketingPreparationDurationAdjustmentProvider :
    MonoBehaviour,
    IBistroBuilderPreparationDurationAdjustmentProvider
{
    public const string StableProviderId = "marketing.operational_pressure";

    [SerializeField]
    private BistroBuilderMarketingService marketingService;

    [SerializeField]
    private BistroBuilderMenuPortfolioService menuPortfolioService;

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [SerializeField]
    private GameClock gameClock;

    private readonly HashSet<string> applicableTargets =
        new HashSet<string>(StringComparer.Ordinal);

    public string AdjustmentProviderId => StableProviderId;
    public int LastAdjustmentBasisPoints { get; private set; }
    public int LastContributingCampaigns { get; private set; }

    private void Awake()
    {
        CacheDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (marketingService == null || menuPortfolioService == null ||
            generalGameStateService == null || gameClock == null)
        {
            error =
                "OperationalPressure necesita Marketing, portfolio, día y reloj.";
            return false;
        }

        if (!marketingService.ValidateConfiguration(out error) ||
            !menuPortfolioService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error))
            return false;

        error = string.Empty;
        return true;
    }

    public bool TryGetAdjustmentBasisPoints(
        BistroBuilderPreparationDurationAdjustmentContext context,
        out int adjustmentBasisPoints,
        out string error)
    {
        adjustmentBasisPoints = 0;
        LastAdjustmentBasisPoints = 0;
        LastContributingCampaigns = 0;

        if (context == null ||
            !BistroBuilderMenuIdUtility.IsValidStableId(
                BistroBuilderMenuIdUtility.NormalizeStableId(context.dishId)))
        {
            error =
                "OperationalPressure recibió un contexto de preparación inválido.";
            return false;
        }

        if (!ValidateConfiguration(out error))
            return false;

        BistroBuilderMarketingCustomerSegment segment =
            ResolveSegment(context.acquisitionSegmentId);
        int minuteOfDay = gameClock.Hour * 60 + gameClock.Minute;
        BistroBuilderMarketingDayPart dayPart =
            BistroBuilderMarketingDemandEngine.ResolveDayPart(minuteOfDay);

        BuildApplicableTargets(context.dishId);

        if (!marketingService.TryEvaluateOperationalPressure(
                generalGameStateService.DayIndex,
                segment,
                dayPart,
                applicableTargets,
                out adjustmentBasisPoints,
                out int contributors,
                out error))
        {
            adjustmentBasisPoints = 0;
            return false;
        }

        if (adjustmentBasisPoints <
                BistroBuilderPreparationDurationAdjustmentPolicy
                    .MinimumAdjustmentBasisPoints ||
            adjustmentBasisPoints >
                BistroBuilderPreparationDurationAdjustmentPolicy
                    .MaximumAdjustmentBasisPoints)
        {
            error = "OperationalPressure agregado queda fuera del rango seguro.";
            adjustmentBasisPoints = 0;
            return false;
        }

        LastAdjustmentBasisPoints = adjustmentBasisPoints;
        LastContributingCampaigns = contributors;
        error = string.Empty;
        return true;
    }

    private void BuildApplicableTargets(string dishId)
    {
        applicableTargets.Clear();
        string activeMenuId = BistroBuilderMenuIdUtility.NormalizeStableId(
            menuPortfolioService.ActiveMenuId);
        if (BistroBuilderMenuIdUtility.IsValidStableId(activeMenuId))
            applicableTargets.Add(activeMenuId);

        string normalizedDishId = BistroBuilderMenuIdUtility.NormalizeStableId(
            dishId);
        if (BistroBuilderMenuIdUtility.IsValidStableId(normalizedDishId))
            applicableTargets.Add(normalizedDishId);
    }

    private static BistroBuilderMarketingCustomerSegment ResolveSegment(
        string segmentId)
    {
        if (Enum.TryParse(
                segmentId,
                true,
                out BistroBuilderMarketingCustomerSegment segment) &&
            Enum.IsDefined(typeof(BistroBuilderMarketingCustomerSegment), segment))
        {
            return segment;
        }

        return BistroBuilderMarketingCustomerSegment.Any;
    }

    private void CacheDependencies()
    {
        if (marketingService == null)
            TryGetComponent(out marketingService);
        if (menuPortfolioService == null)
            TryGetComponent(out menuPortfolioService);
        if (generalGameStateService == null)
            TryGetComponent(out generalGameStateService);
        if (gameClock == null)
            TryGetComponent(out gameClock);
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
