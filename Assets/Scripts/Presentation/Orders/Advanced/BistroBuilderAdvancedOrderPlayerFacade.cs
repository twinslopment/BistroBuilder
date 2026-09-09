using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Proyección jugable de comandas avanzadas; no es autoridad de dominio.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderAdvancedOrderPlayerFacade : MonoBehaviour
{
    [SerializeField] private BistroBuilderAdvancedOrderService advancedOrderService;
    [SerializeField] private BistroBuilderCanonicalOrderService canonicalOrderService;
    [SerializeField] private BistroBuilderRestaurantMenuService menuService;
    [SerializeField] private BistroBuilderDishCatalogService dishCatalogService;

    private readonly List<BistroBuilderCanonicalOrder> orders =
        new List<BistroBuilderCanonicalOrder>(32);
    private readonly List<BistroBuilderMenuItemRuntimeState> menuItems =
        new List<BistroBuilderMenuItemRuntimeState>(64);

    public event Action Changed;

    private void Awake() => CacheDependencies();
    private void OnEnable()
    {
        CacheDependencies();
        if (advancedOrderService != null)
            advancedOrderService.AdvancedOrdersChanged += HandleChanged;
        if (canonicalOrderService != null)
            canonicalOrderService.OrdersChanged += HandleOrderChanged;
    }
    private void OnDisable()
    {
        if (advancedOrderService != null)
            advancedOrderService.AdvancedOrdersChanged -= HandleChanged;
        if (canonicalOrderService != null)
            canonicalOrderService.OrdersChanged -= HandleOrderChanged;
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (advancedOrderService == null || canonicalOrderService == null ||
            menuService == null || dishCatalogService == null)
        {
            error = "La UI 11 necesita servicios de comanda, carta y catálogo.";
            return false;
        }
        return advancedOrderService.ValidateConfiguration(out error);
    }

    public bool TryBuildSnapshot(
        out BistroBuilderAdvancedOrderPlayerSnapshot snapshot,
        out string error)
    {
        snapshot = new BistroBuilderAdvancedOrderPlayerSnapshot();
        if (!ValidateConfiguration(out error)) return false;
        canonicalOrderService.CopyOrderSnapshotsTo(orders);
        for (int orderIndex = 0; orderIndex < orders.Count; orderIndex++)
        {
            BistroBuilderCanonicalOrder order = orders[orderIndex];
            if (order == null || order.IsTerminal) continue;
            snapshot.activeOrderCount++;
            snapshot.totalPriceCents += order.CalculateTotalPriceCents();
            for (int lineIndex = 0; lineIndex < order.Lines.Count; lineIndex++)
            {
                BistroBuilderCanonicalOrderLine line = order.Lines[lineIndex];
                if (line == null) continue;
                var row = new BistroBuilderAdvancedOrderPlayerLine
                {
                    orderId = order.OrderId,
                    lineId = line.LineId,
                    dishId = line.DishId,
                    dishName = ResolveDishName(line.DishId),
                    priceCents = line.PriceCentsAtOrder,
                    state = line.State,
                    stateLabel = StateLabel(line.State),
                    origin = line.AdvancedOriginKind,
                    originLabel = OriginLabel(line.AdvancedOriginKind),
                    billingMode = line.AdvancedBillingMode,
                    billingLabel = line.IsChargeable ? "Facturable" :
                        line.AdvancedBillingMode == BistroBuilderAdvancedOrderBillingMode.Courtesy
                            ? "Cortesía" : "No se cobra",
                    sourceLineId = line.AdvancedSourceLineId,
                    incidentKind = line.AdvancedIncidentKind,
                    incidentLabel = IncidentLabel(line.AdvancedIncidentKind),
                    changeReason = line.AdvancedChangeReason,
                    actions = BistroBuilderAdvancedOrderMutationPolicy.Evaluate(order, line)
                };
                if (line.AdvancedOriginKind != BistroBuilderAdvancedOrderLineOriginKind.Original)
                    snapshot.changedLineCount++;
                if (line.AdvancedIncidentKind != BistroBuilderAdvancedOrderIncidentKind.None)
                    snapshot.incidentCount++;
                snapshot.lines.Add(row);
            }
        }

        if (menuService.TryGetSnapshot(menuItems, out _))
        {
            for (int index = 0; index < menuItems.Count; index++)
            {
                BistroBuilderMenuItemRuntimeState item = menuItems[index];
                if (item == null || !item.Enabled || !item.Unlocked || item.ManuallySoldOut)
                    continue;
                snapshot.replacements.Add(new BistroBuilderAdvancedOrderReplacementOption
                {
                    dishId = item.DishId,
                    displayName = ResolveDishName(item.DishId),
                    priceCents = item.CurrentPriceCents
                });
            }
        }
        error = string.Empty;
        return true;
    }

    public BistroBuilderAdvancedOrderMutationResult Correct(
        BistroBuilderAdvancedOrderPlayerLine line, string replacementDishId) =>
        line == null ? Invalid() : advancedOrderService.TryCorrectLine(
            line.orderId, line.lineId, replacementDishId,
            BistroBuilderAdvancedOrderIncidentKind.CustomerChange,
            "Corrección solicitada desde la ficha de comanda.", "player");

    public BistroBuilderAdvancedOrderMutationResult Repeat(
        BistroBuilderAdvancedOrderPlayerLine line) =>
        line == null ? Invalid() : advancedOrderService.TryRepeatLine(
            line.orderId, line.lineId, "player");

    public BistroBuilderAdvancedOrderMutationResult Cancel(
        BistroBuilderAdvancedOrderPlayerLine line) =>
        line == null ? Invalid() : advancedOrderService.TryCancelLine(
            line.orderId, line.lineId,
            BistroBuilderAdvancedOrderIncidentKind.CustomerChange,
            "Cancelación parcial solicitada por el cliente.", "player");

    public BistroBuilderAdvancedOrderMutationResult Replace(
        BistroBuilderAdvancedOrderPlayerLine line,
        string replacementDishId,
        bool courtesy) =>
        line == null ? Invalid() : advancedOrderService.TryReplaceAfterIncident(
            line.orderId, line.lineId, replacementDishId,
            BistroBuilderAdvancedOrderIncidentKind.QualityIssue,
            "Reposición por incidencia de cocina/servicio.", courtesy, "player");

    public BistroBuilderAdvancedOrderMutationResult Return(
        BistroBuilderAdvancedOrderPlayerLine line) =>
        line == null ? Invalid() : advancedOrderService.TryReturnWithoutReplacement(
            line.orderId, line.lineId,
            BistroBuilderAdvancedOrderIncidentKind.QualityIssue,
            "Devolución solicitada tras servir el plato.", "player");

    public BistroBuilderAdvancedOrderMutationResult ReportIncident(
        BistroBuilderAdvancedOrderPlayerLine line) =>
        line == null ? Invalid() : advancedOrderService.TryReportIncident(
            line.orderId, line.lineId,
            BistroBuilderAdvancedOrderIncidentKind.ServiceError,
            "Incidencia registrada desde la revisión de comanda.", "player");

    private string ResolveDishName(string dishId)
    {
        return dishCatalogService != null &&
            dishCatalogService.TryGetDefinition(dishId, out BistroBuilderDishDefinition dish) &&
            dish != null && !string.IsNullOrWhiteSpace(dish.DisplayName)
                ? dish.DisplayName : dishId;
    }

    public static string StateLabel(BistroBuilderCanonicalOrderLineState state)
    {
        switch (state)
        {
            case BistroBuilderCanonicalOrderLineState.Draft: return "Tomando nota";
            case BistroBuilderCanonicalOrderLineState.Submitted: return "Pedido enviado";
            case BistroBuilderCanonicalOrderLineState.Queued: return "En cola de cocina";
            case BistroBuilderCanonicalOrderLineState.Preparing: return "Preparando";
            case BistroBuilderCanonicalOrderLineState.ReadyForPickup: return "Listo para recoger";
            case BistroBuilderCanonicalOrderLineState.AssignedForDelivery: return "Asignado a camarero";
            case BistroBuilderCanonicalOrderLineState.InTransit: return "En camino a mesa";
            case BistroBuilderCanonicalOrderLineState.Served: return "Servido";
            case BistroBuilderCanonicalOrderLineState.Consumed: return "Consumido";
            case BistroBuilderCanonicalOrderLineState.Cancelled: return "Cancelado";
            case BistroBuilderCanonicalOrderLineState.Failed: return "Retirado por incidencia";
            default: return state.ToString();
        }
    }

    public static string OriginLabel(BistroBuilderAdvancedOrderLineOriginKind origin)
    {
        switch (origin)
        {
            case BistroBuilderAdvancedOrderLineOriginKind.Correction: return "Corrección";
            case BistroBuilderAdvancedOrderLineOriginKind.Repeat: return "Repetición";
            case BistroBuilderAdvancedOrderLineOriginKind.Replacement: return "Reposición";
            case BistroBuilderAdvancedOrderLineOriginKind.Courtesy: return "Cortesía";
            default: return "Original";
        }
    }

    public static string IncidentLabel(BistroBuilderAdvancedOrderIncidentKind incident)
    {
        switch (incident)
        {
            case BistroBuilderAdvancedOrderIncidentKind.CustomerChange: return "Cambio del cliente";
            case BistroBuilderAdvancedOrderIncidentKind.WrongDish: return "Plato incorrecto";
            case BistroBuilderAdvancedOrderIncidentKind.DuplicateOrder: return "Pedido duplicado";
            case BistroBuilderAdvancedOrderIncidentKind.KitchenError: return "Error de cocina";
            case BistroBuilderAdvancedOrderIncidentKind.QualityIssue: return "Problema de calidad";
            case BistroBuilderAdvancedOrderIncidentKind.AllergyRisk: return "Riesgo de alergia";
            case BistroBuilderAdvancedOrderIncidentKind.MissingItem: return "Falta un artículo";
            case BistroBuilderAdvancedOrderIncidentKind.ServiceError: return "Error de servicio";
            default: return string.Empty;
        }
    }

    private void HandleChanged() => Changed?.Invoke();
    private void HandleOrderChanged(BistroBuilderCanonicalOrderChangedEvent _) => Changed?.Invoke();
    private static BistroBuilderAdvancedOrderMutationResult Invalid() =>
        BistroBuilderAdvancedOrderMutationResult.Failure(
            BistroBuilderAdvancedOrderRestriction.InvalidRequest,
            "No hay una línea seleccionada.");

    private void CacheDependencies()
    {
        if (advancedOrderService == null) TryGetComponent(out advancedOrderService);
        if (canonicalOrderService == null) TryGetComponent(out canonicalOrderService);
        if (menuService == null) TryGetComponent(out menuService);
        if (dishCatalogService == null) TryGetComponent(out dishCatalogService);
    }
#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
