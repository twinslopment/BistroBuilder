using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Traduce AverageTicket de Marketing al puerto genérico de cobro de 3B.
/// No publica dinero ni cambia precios de carta/comanda.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Marketing/Marketing Sales Payment Adjustment Provider"
)]
public sealed class BistroBuilderMarketingSalesPaymentAdjustmentProvider :
    MonoBehaviour,
    IBistroBuilderSalesPaymentAdjustmentProvider
{
    public const string StableProviderId = "marketing.average_ticket";

    [SerializeField]
    private BistroBuilderMarketingService marketingService;

    [SerializeField]
    private BistroBuilderMenuPortfolioService menuPortfolioService;

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
        if (marketingService == null || menuPortfolioService == null)
        {
            error = "AverageTicket necesita Marketing y portfolio de cartas.";
            return false;
        }

        if (!marketingService.ValidateConfiguration(out error) ||
            !menuPortfolioService.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetAdjustmentBasisPoints(
        BistroBuilderSalesPaymentAdjustmentContext context,
        out int adjustmentBasisPoints,
        out string error)
    {
        adjustmentBasisPoints = 0;
        LastAdjustmentBasisPoints = 0;
        LastContributingCampaigns = 0;

        if (context == null || context.dayIndex < 1 ||
            context.minuteOfDay < 0 || context.minuteOfDay > 1439)
        {
            error = "AverageTicket recibió un contexto de cobro inválido.";
            return false;
        }

        if (!ValidateConfiguration(out error))
            return false;

        BistroBuilderMarketingCustomerSegment segment =
            ResolveSegment(context.acquisitionSegmentId);
        BistroBuilderMarketingDayPart dayPart =
            BistroBuilderMarketingDemandEngine.ResolveDayPart(
                context.minuteOfDay);

        BuildApplicableTargets(context);

        if (!marketingService.TryEvaluateAverageTicket(
                context.dayIndex,
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

        if (adjustmentBasisPoints < -9000 ||
            adjustmentBasisPoints > 50000)
        {
            error = "AverageTicket agregado queda fuera del rango seguro.";
            adjustmentBasisPoints = 0;
            return false;
        }

        LastAdjustmentBasisPoints = adjustmentBasisPoints;
        LastContributingCampaigns = contributors;
        error = string.Empty;
        return true;
    }

    private void BuildApplicableTargets(
        BistroBuilderSalesPaymentAdjustmentContext context)
    {
        applicableTargets.Clear();

        string activeMenuId = BistroBuilderMenuIdUtility.NormalizeStableId(
            menuPortfolioService.ActiveMenuId);
        if (BistroBuilderMenuIdUtility.IsValidStableId(activeMenuId))
            applicableTargets.Add(activeMenuId);

        if (context.orderedDishIds == null)
            return;

        for (int index = 0; index < context.orderedDishIds.Count; index++)
        {
            string dishId = BistroBuilderMenuIdUtility.NormalizeStableId(
                context.orderedDishIds[index]);
            if (BistroBuilderMenuIdUtility.IsValidStableId(dishId))
                applicableTargets.Add(dishId);
        }
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
