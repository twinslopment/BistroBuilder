using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad runtime de Marketing. Posee únicamente el estado de campañas;
/// delega el dinero en Finanzas y expone efectos para los sistemas propietarios.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Marketing/Marketing Service")]
public sealed class BistroBuilderMarketingService : MonoBehaviour
{
    public const string FinanceSourceSystemId = "marketing.runtime";

    [SerializeField]
    private BistroBuilderMarketingCampaignCatalog campaignCatalog;

    [SerializeField]
    private BistroBuilderDiscretionaryFinanceService discretionaryFinanceService;

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [Header("Objetivos de carta")]
    [SerializeField]
    private BistroBuilderRestaurantMenuService menuService;

    [SerializeField]
    private BistroBuilderMenuPortfolioService menuPortfolioService;

    private BistroBuilderMarketingSnapshot state;

    public event Action<long> MarketingChanged;
    public event Action MarketingRestored;

    public long Revision => state != null ? state.revision : 0L;
    public int ActiveCampaignCount => CountActiveCampaigns(CurrentDayIndex);
    public int CurrentDayIndex =>
        generalGameStateService != null ? generalGameStateService.DayIndex : 0;
    public BistroBuilderMarketingCampaignCatalog CampaignCatalog => campaignCatalog;

    private void Awake()
    {
        EnsureState();
    }

    public bool ValidateConfiguration(out string error)
    {
        EnsureState();
        CacheTargetDependencies();
        if (campaignCatalog == null ||
            discretionaryFinanceService == null ||
            generalGameStateService == null)
        {
            error = "7A necesita catálogo, Finanzas discrecionales y estado general.";
            return false;
        }

        if (!campaignCatalog.ValidateConfiguration(out error) ||
            !discretionaryFinanceService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !BistroBuilderMarketingEngine.TryValidateSnapshot(state, out error))
            return false;

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Contrata una campaña. La candidatura se valida antes de cargar Finanzas;
    /// tras el cargo solo se asigna un snapshot ya validado, evitando estados a medias.
    /// </summary>
    public bool TryStartCampaign(
        string campaignId,
        string targetId,
        out BistroBuilderMarketingCampaignRecord started,
        out string error)
    {
        started = null;
        if (!ValidateConfiguration(out error))
            return false;

        if (!campaignCatalog.TryGetCampaign(campaignId, out var definition))
        {
            error = "No existe la campaña " + campaignId + ".";
            return false;
        }

        if (generalGameStateService.ProgressionLevel <
            definition.minProgressionLevel)
        {
            error = "La campaña requiere nivel de progresión " +
                    definition.minProgressionLevel + ".";
            return false;
        }

        if (!TryValidateCampaignTarget(
                definition,
                targetId,
                out string validatedTargetId,
                out error))
        {
            return false;
        }

        int dayIndex = generalGameStateService.DayIndex;
        string instanceId = "marketing_" + Guid.NewGuid().ToString("N");
        string operationId = "marketing.expense." + instanceId;

        if (!BistroBuilderMarketingEngine.TryCreateCampaign(
                state,
                definition,
                dayIndex,
                validatedTargetId,
                instanceId,
                operationId,
                out BistroBuilderMarketingSnapshot candidate,
                out error))
            return false;

        var expense = new BistroBuilderDiscretionaryExpenseRequest
        {
            operationId = operationId,
            sourceSystemId = FinanceSourceSystemId,
            sourceReferenceId = instanceId,
            categoryId = "expense.marketing." +
                definition.type.ToString().ToLowerInvariant(),
            amountCents = definition.baseCostCents,
            description = "Marketing — " + definition.displayName
        };

        if (!discretionaryFinanceService.TryPostExpense(
                expense,
                out _,
                out error))
            return false;

        state = candidate;
        MarketingChanged?.Invoke(state.revision);
        return TryGetCampaignInstance(instanceId, out started);
    }

    public bool TryStartCampaign(
        string campaignId,
        out BistroBuilderMarketingCampaignRecord started,
        out string error)
    {
        return TryStartCampaign(campaignId, string.Empty, out started, out error);
    }

    public bool TryEvaluateEffects(
        BistroBuilderMarketingEffectQuery query,
        out BistroBuilderMarketingEffectSnapshot effects,
        out string error)
    {
        if (!ValidateConfiguration(out error))
        {
            effects = null;
            return false;
        }

        if (query == null)
        {
            effects = null;
            error = "La consulta de Marketing es nula.";
            return false;
        }

        if (query.dayIndex < 1)
            query.dayIndex = CurrentDayIndex;

        return BistroBuilderMarketingEngine.TryEvaluate(
            state,
            campaignCatalog.Campaigns,
            query,
            out effects,
            out error);
    }

    /// <summary>
    /// Evalúa el ajuste de ticket medio para un cobro concreto. Los objetivos
    /// aplicables proceden del sistema propietario del cobro, no de Marketing.
    /// </summary>
    public bool TryEvaluateAverageTicket(
        int dayIndex,
        BistroBuilderMarketingCustomerSegment segment,
        BistroBuilderMarketingDayPart dayPart,
        ISet<string> applicableTargetIds,
        out int basisPoints,
        out int contributingCampaigns,
        out string error)
    {
        basisPoints = 0;
        contributingCampaigns = 0;
        if (!ValidateConfiguration(out error))
            return false;

        if (dayIndex < 1)
            dayIndex = CurrentDayIndex;

        return BistroBuilderMarketingEngine.TryEvaluateAverageTicket(
            state,
            campaignCatalog.Campaigns,
            dayIndex,
            segment,
            dayPart,
            applicableTargetIds,
            out basisPoints,
            out contributingCampaigns,
            out error);
    }

    /// <summary>
    /// Evalúa la presión operativa aplicable a una unidad de trabajo concreta.
    /// Marketing aporta el porcentaje; cocina conserva autoridad sobre tiempos.
    /// </summary>
    public bool TryEvaluateOperationalPressure(
        int dayIndex,
        BistroBuilderMarketingCustomerSegment segment,
        BistroBuilderMarketingDayPart dayPart,
        ISet<string> applicableTargetIds,
        out int basisPoints,
        out int contributingCampaigns,
        out string error)
    {
        basisPoints = 0;
        contributingCampaigns = 0;
        if (!ValidateConfiguration(out error))
            return false;

        if (dayIndex < 1)
            dayIndex = CurrentDayIndex;

        return BistroBuilderMarketingEngine.TryEvaluateOperationalPressure(
            state,
            campaignCatalog.Campaigns,
            dayIndex,
            segment,
            dayPart,
            applicableTargetIds,
            out basisPoints,
            out contributingCampaigns,
            out error);
    }
    /// <summary>
    /// Cancela una campaña activa. El coste es prepago y no se devuelve;
    /// tampoco se revierten reservas, reputación u otros efectos ya consumados.
    /// </summary>
    public bool TryCancelCampaign(
        string instanceId,
        out BistroBuilderMarketingCampaignRecord cancelled,
        out string error)
    {
        cancelled = null;
        if (!ValidateConfiguration(out error))
            return false;
        if (!TryGetCampaignInstance(instanceId, out BistroBuilderMarketingCampaignRecord existing) ||
            existing == null)
        {
            error = "No existe la campaña activa indicada.";
            return false;
        }

        if (!BistroBuilderMarketingEngine.TryCancelCampaign(
                state,
                instanceId,
                CurrentDayIndex,
                out BistroBuilderMarketingSnapshot candidate,
                out error))
            return false;

        cancelled = existing;
        state = candidate;
        MarketingChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }
    /// <summary>Retira del estado campañas ya vencidas; nunca toca Finanzas.</summary>
    public bool TryRefreshForCurrentDay(out string error)
    {
        if (!ValidateConfiguration(out error))
            return false;

        if (!BistroBuilderMarketingEngine.TryPruneExpired(
                state,
                CurrentDayIndex,
                out BistroBuilderMarketingSnapshot candidate,
                out bool changed,
                out error))
            return false;

        if (changed)
        {
            state = candidate;
            MarketingChanged?.Invoke(state.revision);
        }
        return true;
    }

    public bool TryGetCampaignInstance(
        string instanceId,
        out BistroBuilderMarketingCampaignRecord record)
    {
        record = null;
        EnsureState();
        string id = BistroBuilderMarketingEngine.NormalizeId(instanceId);
        for (int i = 0; i < state.campaigns.Count; i++)
        {
            BistroBuilderMarketingCampaignRecord candidate = state.campaigns[i];
            if (candidate != null &&
                BistroBuilderMarketingEngine.NormalizeId(candidate.instanceId) == id)
            {
                record = candidate.DeepClone();
                return true;
            }
        }
        return false;
    }

    public void CopyActiveCampaigns(
        int dayIndex,
        List<BistroBuilderMarketingCampaignRecord> destination)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        destination.Clear();
        EnsureState();
        for (int i = 0; i < state.campaigns.Count; i++)
        {
            BistroBuilderMarketingCampaignRecord record = state.campaigns[i];
            if (record != null && record.IsActiveOnDay(dayIndex))
                destination.Add(record.DeepClone());
        }
    }

    public BistroBuilderMarketingSnapshot CreateSnapshot()
    {
        EnsureState();
        return state.DeepClone();
    }

    public bool TryRestoreSnapshot(
        BistroBuilderMarketingSnapshot snapshot,
        out string error)
    {
        if (!BistroBuilderMarketingEngine.TryValidateSnapshot(snapshot, out error))
            return false;
        state = snapshot.DeepClone();
        MarketingRestored?.Invoke();
        MarketingChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    public bool TryResetForLegacyLoad(out string error)
    {
        state = BistroBuilderMarketingEngine.CreateEmptySnapshot();
        MarketingRestored?.Invoke();
        MarketingChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    private bool TryValidateCampaignTarget(
        BistroBuilderMarketingCampaignDefinition definition,
        string targetId,
        out string normalizedTargetId,
        out string error)
    {
        error = string.Empty;
        normalizedTargetId = BistroBuilderMarketingEngine.NormalizeId(targetId);

        if (definition.targetKind == BistroBuilderMarketingTargetKind.None)
        {
            normalizedTargetId = string.Empty;
            error = string.Empty;
            return true;
        }

        if (!BistroBuilderMenuIdUtility.IsValidStableId(normalizedTargetId))
        {
            error = "La campaña necesita un objetivo de carta válido.";
            return false;
        }

        if (definition.targetKind == BistroBuilderMarketingTargetKind.Dish)
        {
            if (menuService == null ||
                !menuService.TryGetItemSnapshot(normalizedTargetId, out _))
            {
                error = "El plato objetivo no existe en la carta operativa.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        if (definition.targetKind == BistroBuilderMarketingTargetKind.Menu)
        {
            if (menuPortfolioService == null ||
                !menuPortfolioService.TryGetActivePortfolioSnapshot(
                    out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
                    out error) ||
                portfolio == null ||
                !portfolio.TryGetMenu(normalizedTargetId, out _))
            {
                if (string.IsNullOrWhiteSpace(error))
                    error = "La carta objetivo no existe en el portfolio activo.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        error = "La campaña declara un tipo de objetivo desconocido.";
        return false;
    }

    private void CacheTargetDependencies()
    {
        if (menuService == null)
            TryGetComponent(out menuService);
        if (menuPortfolioService == null)
            TryGetComponent(out menuPortfolioService);
    }

    private int CountActiveCampaigns(int dayIndex)
    {
        EnsureState();
        if (dayIndex < 1)
            return 0;
        int count = 0;
        for (int i = 0; i < state.campaigns.Count; i++)
            if (state.campaigns[i] != null &&
                state.campaigns[i].IsActiveOnDay(dayIndex))
                count++;
        return count;
    }

    private void EnsureState()
    {
        if (state == null)
            state = BistroBuilderMarketingEngine.CreateEmptySnapshot();
    }
}
