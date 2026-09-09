using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Fachada de Presentation para la pantalla jugable de Marketing.
/// Proyecta catálogo, campañas activas, objetivos y relaciones con clientes;
/// toda mutación se delega en MarketingService.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Marketing/Marketing Player Facade")]
public sealed class BistroBuilderMarketingPlayerFacade : MonoBehaviour
{
    [SerializeField] private BistroBuilderMarketingService marketingService;
    [SerializeField] private BistroBuilderGuestRelationsService guestRelationsService;
    [SerializeField] private BistroBuilderReputationService reputationService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private BistroBuilderRestaurantMenuService menuService;
    [SerializeField] private BistroBuilderMenuPortfolioService menuPortfolioService;
    [SerializeField] private BistroBuilderDishCatalogService dishCatalogService;

    private readonly List<BistroBuilderMarketingCampaignRecord> activeBuffer =
        new List<BistroBuilderMarketingCampaignRecord>();
    private readonly List<BistroBuilderMenuItemRuntimeState> menuItemBuffer =
        new List<BistroBuilderMenuItemRuntimeState>();
    private readonly List<BistroBuilderDishDefinition> dishBuffer =
        new List<BistroBuilderDishDefinition>();

    public event Action ViewInvalidated;

    private void Awake() => CacheDependencies();

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void OnDisable() => Unsubscribe();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (marketingService == null || guestRelationsService == null || reputationService == null ||
            generalGameStateService == null || menuService == null ||
            menuPortfolioService == null || dishCatalogService == null)
        {
            error = "La UI de Marketing necesita Marketing, GuestRelations, Reputación, calendario y Carta.";
            return false;
        }

        if (!marketingService.ValidateConfiguration(out error) ||
            !guestRelationsService.ValidateConfiguration(out error) ||
            !reputationService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !menuService.ValidateConfiguration(out error) ||
            !menuPortfolioService.ValidateConfiguration(out error) ||
            !dishCatalogService.ValidateConfiguration(out error))
            return false;

        error = string.Empty;
        return true;
    }

    public bool TryBuildSnapshot(
        out BistroBuilderMarketingPlayerUiSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        if (!ValidateConfiguration(out error))
            return false;

        marketingService.TryRefreshForCurrentDay(out _);
        var built = new BistroBuilderMarketingPlayerUiSnapshot
        {
            currentDayIndex = generalGameStateService.DayIndex,
            progressionLevel = generalGameStateService.ProgressionLevel,
            marketingRevision = marketingService.Revision,
            activeCampaignCount = marketingService.ActiveCampaignCount,
            reputationPoints = reputationService.ExternalReputationPoints,
            reputationDemandBasisPoints =
                reputationService.PersistentDemandBasisPoints,
            recurrentCohortCount = guestRelationsService.CohortCount
        };

        BuildTargetOptions(built, out error);
        if (!string.IsNullOrWhiteSpace(error))
            return false;
        BuildCampaignRows(built);
        BuildActiveRows(built);
        snapshot = built;
        error = string.Empty;
        return true;
    }

    public bool TryStartCampaign(
        string campaignId,
        string targetId,
        out BistroBuilderMarketingCampaignRecord started,
        out string error)
    {
        started = null;
        if (!ValidateConfiguration(out error))
            return false;
        return marketingService.TryStartCampaign(
            campaignId,
            targetId,
            out started,
            out error);
    }

    public bool TryCancelCampaign(
        string instanceId,
        out BistroBuilderMarketingCampaignRecord cancelled,
        out string error)
    {
        cancelled = null;
        if (!ValidateConfiguration(out error))
            return false;
        return marketingService.TryCancelCampaign(
            instanceId,
            out cancelled,
            out error);
    }

    private void BuildCampaignRows(BistroBuilderMarketingPlayerUiSnapshot snapshot)
    {
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> definitions =
            marketingService.CampaignCatalog.Campaigns;
        for (int index = 0; index < definitions.Count; index++)
        {
            BistroBuilderMarketingCampaignDefinition definition = definitions[index];
            if (definition == null) continue;

            bool progressionUnlocked = snapshot.progressionLevel >=
                definition.minProgressionLevel;
            string blockedReason = progressionUnlocked
                ? BuildTargetAvailabilityBlock(definition, snapshot)
                : "Requiere nivel " + definition.minProgressionLevel + ".";

            snapshot.campaigns.Add(new BistroBuilderMarketingPlayerCampaignRow
            {
                campaignId = definition.campaignId,
                displayName = definition.displayName,
                description = definition.description,
                type = definition.type,
                targetKind = definition.targetKind,
                costCents = definition.baseCostCents,
                durationDays = definition.durationDays,
                minProgressionLevel = definition.minProgressionLevel,
                effectsSummary = BuildEffectsSummary(definition.modifiers),
                progressionUnlocked = progressionUnlocked,
                blockedReason = blockedReason
            });
        }

        snapshot.campaigns.Sort(CompareCampaignRows);
    }

    private void BuildActiveRows(BistroBuilderMarketingPlayerUiSnapshot snapshot)
    {
        activeBuffer.Clear();
        marketingService.CopyActiveCampaigns(snapshot.currentDayIndex, activeBuffer);
        for (int index = 0; index < activeBuffer.Count; index++)
        {
            BistroBuilderMarketingCampaignRecord record = activeBuffer[index];
            if (record == null ||
                !marketingService.CampaignCatalog.TryGetCampaign(
                    record.campaignId,
                    out BistroBuilderMarketingCampaignDefinition definition) ||
                definition == null)
                continue;

            snapshot.activeCampaigns.Add(new BistroBuilderMarketingPlayerActiveRow
            {
                instanceId = record.instanceId,
                campaignId = record.campaignId,
                displayName = definition.displayName,
                targetId = record.targetId,
                targetDisplayName = ResolveTargetDisplayName(record.targetId, snapshot),
                type = definition.type,
                startDayIndex = record.startDayIndex,
                endDayExclusive = record.endDayExclusive,
                daysRemaining = Math.Max(0, record.endDayExclusive - snapshot.currentDayIndex),
                paidCostCents = record.paidCostCents,
                effectsSummary = BuildEffectsSummary(definition.modifiers)
            });
        }
        snapshot.activeCampaigns.Sort(CompareActiveRows);
    }

    private void BuildTargetOptions(
        BistroBuilderMarketingPlayerUiSnapshot snapshot,
        out string error)
    {
        error = string.Empty;
        menuItemBuffer.Clear();
        if (!menuService.TryGetSnapshot(menuItemBuffer, out error))
            return;

        for (int index = 0; index < menuItemBuffer.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = menuItemBuffer[index];
            if (item == null || !item.Unlocked) continue;
            string label = item.DishId;
            if (dishCatalogService.TryGetDefinition(
                    item.DishId,
                    out BistroBuilderDishDefinition definition) && definition != null)
                label = definition.DisplayName;

            snapshot.dishTargets.Add(new BistroBuilderMarketingPlayerTargetOption
            {
                kind = BistroBuilderMarketingTargetKind.Dish,
                targetId = item.DishId,
                displayName = label
            });
        }
        snapshot.dishTargets.Sort(CompareTargetOptions);

        if (!menuPortfolioService.TryGetActivePortfolioSnapshot(
                out BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
                out error) || portfolio == null)
            return;

        for (int index = 0; index < portfolio.Menus.Count; index++)
        {
            BistroBuilderNamedMenuRuntimeState menu = portfolio.Menus[index];
            if (menu == null) continue;
            snapshot.menuTargets.Add(new BistroBuilderMarketingPlayerTargetOption
            {
                kind = BistroBuilderMarketingTargetKind.Menu,
                targetId = menu.MenuId,
                displayName = menu.DisplayName +
                    (string.Equals(menu.MenuId, portfolio.ActiveMenuId, StringComparison.Ordinal)
                        ? " · activa"
                        : string.Empty)
            });
        }
        snapshot.menuTargets.Sort(CompareTargetOptions);
        error = string.Empty;
    }

    private static string BuildTargetAvailabilityBlock(
        BistroBuilderMarketingCampaignDefinition definition,
        BistroBuilderMarketingPlayerUiSnapshot snapshot)
    {
        if (definition.targetKind == BistroBuilderMarketingTargetKind.Dish &&
            snapshot.dishTargets.Count == 0)
            return "No hay platos disponibles como objetivo.";
        if (definition.targetKind == BistroBuilderMarketingTargetKind.Menu &&
            snapshot.menuTargets.Count == 0)
            return "No hay cartas disponibles como objetivo.";
        return string.Empty;
    }

    public static string BuildEffectsSummary(
        IReadOnlyList<BistroBuilderMarketingModifier> modifiers)
    {
        if (modifiers == null || modifiers.Count == 0)
            return "Sin modificadores.";

        var builder = new StringBuilder();
        for (int index = 0; index < modifiers.Count; index++)
        {
            BistroBuilderMarketingModifier modifier = modifiers[index];
            if (modifier == null) continue;
            if (builder.Length > 0) builder.Append(" · ");
            builder.Append(ModifierLabel(modifier.kind));
            builder.Append(' ');
            builder.Append(modifier.kind == BistroBuilderMarketingModifierKind.Reputation
                ? FormatSignedNumber(modifier.flatPoints)
                : FormatBasisPoints(modifier.basisPoints));

            if (modifier.segment != BistroBuilderMarketingCustomerSegment.Any)
                builder.Append(" / ").Append(SegmentLabel(modifier.segment));
            if (modifier.dayPart != BistroBuilderMarketingDayPart.Any)
                builder.Append(" / ").Append(DayPartLabel(modifier.dayPart));
        }
        return builder.Length > 0 ? builder.ToString() : "Sin modificadores.";
    }

    private static string ModifierLabel(BistroBuilderMarketingModifierKind kind)
    {
        switch (kind)
        {
            case BistroBuilderMarketingModifierKind.OverallDemand: return "Demanda";
            case BistroBuilderMarketingModifierKind.ReservationDemand: return "Reservas";
            case BistroBuilderMarketingModifierKind.WalkInDemand: return "Walk-ins";
            case BistroBuilderMarketingModifierKind.Reputation: return "Reputación";
            case BistroBuilderMarketingModifierKind.AverageTicket: return "Ticket medio";
            case BistroBuilderMarketingModifierKind.RepeatVisit: return "Repetición";
            case BistroBuilderMarketingModifierKind.OperationalPressure: return "Presión operativa";
            case BistroBuilderMarketingModifierKind.TargetDemand: return "Demanda objetivo";
            default: return kind.ToString();
        }
    }

    private static string SegmentLabel(BistroBuilderMarketingCustomerSegment segment)
    {
        switch (segment)
        {
            case BistroBuilderMarketingCustomerSegment.LocalResidents: return "residentes";
            case BistroBuilderMarketingCustomerSegment.Workers: return "trabajadores";
            case BistroBuilderMarketingCustomerSegment.YoungAdults: return "jóvenes";
            case BistroBuilderMarketingCustomerSegment.Groups: return "grupos";
            case BistroBuilderMarketingCustomerSegment.Couples: return "parejas";
            case BistroBuilderMarketingCustomerSegment.Foodies: return "foodies";
            case BistroBuilderMarketingCustomerSegment.Traditional: return "tradicional";
            case BistroBuilderMarketingCustomerSegment.PriceSensitive: return "precio";
            case BistroBuilderMarketingCustomerSegment.Planners: return "planificadores";
            case BistroBuilderMarketingCustomerSegment.HighValue: return "alto valor";
            default: return "todos";
        }
    }

    private static string DayPartLabel(BistroBuilderMarketingDayPart dayPart)
    {
        switch (dayPart)
        {
            case BistroBuilderMarketingDayPart.Breakfast: return "desayuno";
            case BistroBuilderMarketingDayPart.Lunch: return "comida";
            case BistroBuilderMarketingDayPart.Afternoon: return "tarde";
            case BistroBuilderMarketingDayPart.Dinner: return "cena";
            case BistroBuilderMarketingDayPart.LateNight: return "noche";
            default: return "todo el día";
        }
    }

    private static string FormatBasisPoints(int basisPoints)
    {
        int absolute = Math.Abs(basisPoints);
        string sign = basisPoints > 0 ? "+" : basisPoints < 0 ? "−" : string.Empty;
        if (absolute % 100 == 0)
            return sign + (absolute / 100) + " %";
        return sign + (absolute / 100) + "," +
               (absolute % 100).ToString("00") + " %";
    }

    private static string FormatSignedNumber(int value)
    {
        if (value > 0) return "+" + value;
        if (value < 0) return "−" + Math.Abs(value);
        return "0";
    }

    private static string ResolveTargetDisplayName(
        string targetId,
        BistroBuilderMarketingPlayerUiSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(targetId)) return "General";
        string resolved = FindTarget(snapshot.dishTargets, targetId);
        if (!string.IsNullOrWhiteSpace(resolved)) return resolved;
        resolved = FindTarget(snapshot.menuTargets, targetId);
        return string.IsNullOrWhiteSpace(resolved) ? targetId : resolved;
    }

    private static string FindTarget(
        List<BistroBuilderMarketingPlayerTargetOption> targets,
        string targetId)
    {
        for (int index = 0; index < targets.Count; index++)
        {
            BistroBuilderMarketingPlayerTargetOption option = targets[index];
            if (option != null && string.Equals(
                    option.targetId,
                    targetId,
                    StringComparison.Ordinal))
                return option.displayName;
        }
        return string.Empty;
    }

    private static int CompareCampaignRows(
        BistroBuilderMarketingPlayerCampaignRow first,
        BistroBuilderMarketingPlayerCampaignRow second)
    {
        if (ReferenceEquals(first, second)) return 0;
        if (first == null) return 1;
        if (second == null) return -1;
        int type = first.type.CompareTo(second.type);
        return type != 0
            ? type
            : string.Compare(first.displayName, second.displayName,
                StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareActiveRows(
        BistroBuilderMarketingPlayerActiveRow first,
        BistroBuilderMarketingPlayerActiveRow second)
    {
        if (ReferenceEquals(first, second)) return 0;
        if (first == null) return 1;
        if (second == null) return -1;
        int ending = first.endDayExclusive.CompareTo(second.endDayExclusive);
        return ending != 0
            ? ending
            : string.Compare(first.displayName, second.displayName,
                StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareTargetOptions(
        BistroBuilderMarketingPlayerTargetOption first,
        BistroBuilderMarketingPlayerTargetOption second)
    {
        if (ReferenceEquals(first, second)) return 0;
        if (first == null) return 1;
        if (second == null) return -1;
        return string.Compare(first.displayName, second.displayName,
            StringComparison.OrdinalIgnoreCase);
    }

    private void Subscribe()
    {
        Unsubscribe();
        if (marketingService != null)
        {
            marketingService.MarketingChanged += HandleRevisionChanged;
            marketingService.MarketingRestored += HandleChanged;
        }
        if (guestRelationsService != null)
        {
            guestRelationsService.RelationsChanged += HandleRevisionChanged;
            guestRelationsService.RelationsRestored += HandleChanged;
        }
        if (reputationService != null)
        {
            reputationService.ReputationChanged += HandleRevisionChanged;
            reputationService.ReputationRestored += HandleChanged;
        }
        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged += HandleChanged;
            generalGameStateService.ProgressionChanged += HandleChanged;
        }
        if (menuService != null)
            menuService.MenuChanged += HandleMenuChanged;
        if (menuPortfolioService != null)
        {
            menuPortfolioService.PortfolioChanged += HandleChanged;
            menuPortfolioService.ActiveMenuChanged += HandleActiveMenuChanged;
        }
        if (dishCatalogService != null)
            dishCatalogService.CatalogChanged += HandleChanged;
    }

    private void Unsubscribe()
    {
        if (marketingService != null)
        {
            marketingService.MarketingChanged -= HandleRevisionChanged;
            marketingService.MarketingRestored -= HandleChanged;
        }
        if (guestRelationsService != null)
        {
            guestRelationsService.RelationsChanged -= HandleRevisionChanged;
            guestRelationsService.RelationsRestored -= HandleChanged;
        }
        if (reputationService != null)
        {
            reputationService.ReputationChanged -= HandleRevisionChanged;
            reputationService.ReputationRestored -= HandleChanged;
        }
        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleChanged;
            generalGameStateService.ProgressionChanged -= HandleChanged;
        }
        if (menuService != null)
            menuService.MenuChanged -= HandleMenuChanged;
        if (menuPortfolioService != null)
        {
            menuPortfolioService.PortfolioChanged -= HandleChanged;
            menuPortfolioService.ActiveMenuChanged -= HandleActiveMenuChanged;
        }
        if (dishCatalogService != null)
            dishCatalogService.CatalogChanged -= HandleChanged;
    }

    private void HandleRevisionChanged(long _) => ViewInvalidated?.Invoke();
    private void HandleChanged() => ViewInvalidated?.Invoke();
    private void HandleMenuChanged(BistroBuilderMenuChangedEvent _) =>
        ViewInvalidated?.Invoke();
    private void HandleActiveMenuChanged(BistroBuilderMenuResolutionResult _) =>
        ViewInvalidated?.Invoke();

    private void CacheDependencies()
    {
        if (marketingService == null) TryGetComponent(out marketingService);
        if (guestRelationsService == null) TryGetComponent(out guestRelationsService);
        if (reputationService == null) TryGetComponent(out reputationService);
        if (generalGameStateService == null) TryGetComponent(out generalGameStateService);
        if (menuService == null) TryGetComponent(out menuService);
        if (menuPortfolioService == null) TryGetComponent(out menuPortfolioService);
        if (dishCatalogService == null) TryGetComponent(out dishCatalogService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
