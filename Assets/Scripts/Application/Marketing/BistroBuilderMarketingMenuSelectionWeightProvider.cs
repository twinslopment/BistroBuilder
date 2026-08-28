using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Adapta TargetDemand de Marketing al puerto genérico de selección 2.1D.
/// No elige platos: solo publica un ajuste de peso para el DishId consultado.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Marketing/Marketing Menu Selection Weight Provider"
)]
public sealed class BistroBuilderMarketingMenuSelectionWeightProvider :
    MonoBehaviour,
    IBistroBuilderMenuSelectionWeightProvider
{
    public const string StableProviderId = "marketing.target_demand";

    [SerializeField]
    private BistroBuilderMarketingService marketingService;

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [SerializeField]
    private GameClock gameClock;

    [SerializeField]
    private TableAssignmentSystem tableAssignmentSystem;

    public string WeightProviderId => StableProviderId;

    private void Awake()
    {
        CacheDependencies();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();

        if (marketingService == null ||
            generalGameStateService == null ||
            gameClock == null ||
            tableAssignmentSystem == null)
        {
            error = "La ponderación de Marketing necesita campañas, " +
                    "calendario, reloj y grupos de clientes.";
            return false;
        }

        if (!marketingService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetWeightAdjustmentBasisPoints(
        BistroBuilderMenuSelectionContext context,
        string dishId,
        out int adjustmentBasisPoints,
        out string error
    )
    {
        adjustmentBasisPoints = 0;

        if (!ValidateConfiguration(out error))
            return false;

        string normalizedDishId =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);
        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalizedDishId))
        {
            error = "Marketing recibió un DishId inválido para ponderar.";
            return false;
        }

        BistroBuilderMarketingCustomerSegment segment =
            ResolveSegment(context.SelectionReferenceId);
        BistroBuilderMarketingDayPart dayPart =
            BistroBuilderMarketingDemandEngine.ResolveDayPart(
                gameClock.Hour * 60 + gameClock.Minute
            );

        if (!marketingService.TryEvaluateEffects(
                new BistroBuilderMarketingEffectQuery
                {
                    dayIndex = generalGameStateService.DayIndex,
                    segment = segment,
                    dayPart = dayPart,
                    targetId = normalizedDishId
                },
                out BistroBuilderMarketingEffectSnapshot effects,
                out error))
        {
            return false;
        }

        adjustmentBasisPoints = effects.targetDemandBasisPoints;
        if (adjustmentBasisPoints < -9000 ||
            adjustmentBasisPoints > 50000)
        {
            error = "TargetDemand agregado queda fuera de rango.";
            adjustmentBasisPoints = 0;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private BistroBuilderMarketingCustomerSegment ResolveSegment(
        string selectionReferenceId
    )
    {
        if (!TryParseGroupId(selectionReferenceId, out int groupId))
            return BistroBuilderMarketingCustomerSegment.Any;

        var groups = tableAssignmentSystem.RegisteredGroups;
        for (int index = 0; index < groups.Count; index++)
        {
            CustomerGroup group = groups[index];
            if (group == null || group.GroupId != groupId)
                continue;

            BistroBuilderCustomerAcquisitionTag tag =
                group.GetComponent<BistroBuilderCustomerAcquisitionTag>();
            if (tag != null && Enum.TryParse(
                    tag.SegmentId,
                    true,
                    out BistroBuilderMarketingCustomerSegment segment) &&
                segment != BistroBuilderMarketingCustomerSegment.Any)
            {
                return segment;
            }

            break;
        }

        return BistroBuilderMarketingCustomerSegment.Any;
    }

    private static bool TryParseGroupId(
        string selectionReferenceId,
        out int groupId
    )
    {
        groupId = 0;
        string normalized = BistroBuilderOrderIdUtility.Normalize(
            selectionReferenceId
        );

        if (normalized.StartsWith("group_", StringComparison.Ordinal))
        {
            return TryParsePositiveInt(normalized.Substring(6), out groupId);
        }

        const string CustomerPrefix = "customer_g";
        int memberMarker = normalized.IndexOf(
            "_p",
            StringComparison.Ordinal
        );
        if (!normalized.StartsWith(CustomerPrefix, StringComparison.Ordinal) ||
            memberMarker <= CustomerPrefix.Length)
        {
            return false;
        }

        string groupPart = normalized.Substring(
            CustomerPrefix.Length,
            memberMarker - CustomerPrefix.Length
        );
        return TryParsePositiveInt(groupPart, out groupId);
    }

    private static bool TryParsePositiveInt(
        string value,
        out int result
    )
    {
        return int.TryParse(
                   value,
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out result
               ) && result > 0;
    }

    private void CacheDependencies()
    {
        if (marketingService == null)
            TryGetComponent(out marketingService);
        if (generalGameStateService == null)
            TryGetComponent(out generalGameStateService);
        if (gameClock == null)
            gameClock = FindFirstObjectByType<GameClock>();
        if (tableAssignmentSystem == null)
            tableAssignmentSystem = FindFirstObjectByType<TableAssignmentSystem>();
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
