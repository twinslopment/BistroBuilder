using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Integración 368C+368D entre comandas, recetas, inventario y cocina.
///
/// Garantías:
/// - Reserva una receta por línea canónica antes de que la comanda avance.
/// - Si alguna línea no puede reservarse, revierte las reservas previas y
///   cancela la comanda completa: no existen aceptaciones parciales.
/// - Consume cada reserva exactamente al comenzar la preparación.
/// - Libera reservas todavía activas al cancelar antes de cocinar.
/// - Una cancelación posterior al inicio no devuelve ingredientes ya usados.
/// - Usa IDs deterministas para que reentradas de eventos sean idempotentes.
/// - Mesa, barra y WaitingAtBar comparten el mismo inventario canónico.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Inventory/Order Inventory Lifecycle 368CD")]
public sealed class BistroBuilderOrderInventoryLifecycleService : MonoBehaviour
{
    [SerializeField] private OrderSystem orderSystem;
    [SerializeField] private BistroBuilderInventoryService inventoryService;
    [SerializeField] private BistroBuilderRecipeCatalogService recipeCatalogService;

    [Header("Depuración")]
    [SerializeField] private bool logTransitions = true;

    private readonly Dictionary<int, OrderInventorySession> sessionsByLegacyOrderId =
        new Dictionary<int, OrderInventorySession>();
    private bool subscribed;

    public int ActiveSessionCount => sessionsByLegacyOrderId.Count;

    /// <summary>
    /// Retira únicamente los enlaces transitorios entre comandas legacy y
    /// reservas. El inventario autoritativo se restaura por su propio proveedor.
    /// </summary>
    public void ClearRuntimeForLoad()
    {
        foreach (KeyValuePair<int, OrderInventorySession> pair
                 in sessionsByLegacyOrderId)
        {
            if (pair.Value != null && pair.Value.Order != null)
            {
                pair.Value.Order.StateChanged -= HandleOrderStateChanged;
            }
        }

        sessionsByLegacyOrderId.Clear();
    }

    /// <summary>
    /// Reconstruye los enlaces de 368CD desde las identidades deterministas
    /// de OrderId/LineId y las reservas ya restauradas por inventory.canonical.
    /// No recalcula recetas ni vuelve a reservar, de modo que una actualización
    /// de contenido no reinterpreta una comanda que estaba en curso.
    /// </summary>
    public bool TryRestoreSessionsFromOrders(out string error)
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();
        ClearRuntimeForLoad();

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        IReadOnlyList<RestaurantOrder> orders = orderSystem.ActiveOrders;
        BistroBuilderCanonicalOrderIntegrationService integration =
            orderSystem.CanonicalIntegrationService;

        if (integration == null || integration.CanonicalOrderService == null)
        {
            error = "No está disponible la autoridad canónica para restaurar 368CD.";
            return false;
        }

        for (int orderIndex = 0; orderIndex < orders.Count; orderIndex++)
        {
            RestaurantOrder order = orders[orderIndex];
            if (order == null || order.IsFinished)
            {
                continue;
            }

            if (!integration.CanonicalOrderService.TryGetOrderSnapshot(
                    order.CanonicalOrderId,
                    out BistroBuilderCanonicalOrder canonicalOrder
                ) || canonicalOrder == null)
            {
                error = "No se pudo resolver la comanda canónica " +
                        order.CanonicalOrderId + ".";
                ClearRuntimeForLoad();
                return false;
            }

            var session = new OrderInventorySession(order);

            for (int lineIndex = 0;
                 lineIndex < canonicalOrder.Lines.Count;
                 lineIndex++)
            {
                BistroBuilderCanonicalOrderLine line =
                    canonicalOrder.Lines[lineIndex];

                if (line == null)
                {
                    error = "La comanda restaurada contiene una línea nula.";
                    ClearRuntimeForLoad();
                    return false;
                }

                string reservationId = BuildReservationId(
                    order.CanonicalOrderId,
                    line.LineId
                );

                if (!inventoryService.TryGetReservationSnapshot(
                        reservationId,
                        out BistroBuilderInventoryReservationSnapshot reservation
                    ) || reservation == null)
                {
                    error = "Falta la reserva persistida " + reservationId +
                            " para la línea " + line.LineId + ".";
                    ClearRuntimeForLoad();
                    return false;
                }

                if (!IsReservationStatusCompatible(line.State, reservation.Status))
                {
                    error = "La reserva " + reservationId + " está " +
                            reservation.Status + " pero la línea " +
                            line.LineId + " está " + line.State + ".";
                    ClearRuntimeForLoad();
                    return false;
                }

                session.ReservationIdByLineId.Add(line.LineId, reservationId);
            }

            sessionsByLegacyOrderId.Add(order.OrderId, session);
            order.StateChanged += HandleOrderStateChanged;
        }

        return true;
    }

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (orderSystem == null)
        {
            error = "Falta OrderSystem.";
            return false;
        }

        if (!orderSystem.ValidateConfiguration(out error))
        {
            return false;
        }

        if (inventoryService == null)
        {
            error = "Falta BistroBuilderInventoryService.";
            return false;
        }

        if (!inventoryService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (recipeCatalogService == null)
        {
            error = "Falta BistroBuilderRecipeCatalogService.";
            return false;
        }

        return recipeCatalogService.ValidateConfiguration(out error);
    }

    public bool TryGetReservationId(
        RestaurantOrder order,
        string canonicalLineId,
        out string reservationId)
    {
        reservationId = string.Empty;
        if (order == null || string.IsNullOrWhiteSpace(canonicalLineId) ||
            !sessionsByLegacyOrderId.TryGetValue(order.OrderId, out OrderInventorySession session))
        {
            return false;
        }

        return session.ReservationIdByLineId.TryGetValue(canonicalLineId, out reservationId);
    }

    /// <summary>
    /// API explícita para fallos de producción futuros. La cantidad adicional
    /// de merma debe representar pérdida física extra, no ingredientes ya
    /// consumidos por la receta, evitando doble descuento.
    /// </summary>
    public bool TryRegisterAdditionalLineWaste(
        RestaurantOrder order,
        string canonicalLineId,
        string ingredientId,
        long canonicalMilliUnits,
        string reason,
        out string error)
    {
        error = string.Empty;
        if (order == null || string.IsNullOrWhiteSpace(canonicalLineId))
        {
            error = "La comanda o la línea no son válidas.";
            return false;
        }

        string operationId = BuildOperationId("waste", order.CanonicalOrderId, canonicalLineId, ingredientId);
        string sourceId = BuildSourceId(order.CanonicalOrderId, canonicalLineId);
        return inventoryService.TryRegisterWaste(
            operationId,
            sourceId,
            ingredientId,
            canonicalMilliUnits,
            string.IsNullOrWhiteSpace(reason) ? "Merma adicional de producción." : reason,
            out error);
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        CacheDependenciesIfNeeded();
        if (orderSystem == null)
        {
            return;
        }

        orderSystem.OrderCreated += HandleOrderCreated;
        orderSystem.OrderCompleted += HandleOrderFinished;
        orderSystem.OrderCancelled += HandleOrderFinished;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || orderSystem == null)
        {
            subscribed = false;
            return;
        }

        orderSystem.OrderCreated -= HandleOrderCreated;
        orderSystem.OrderCompleted -= HandleOrderFinished;
        orderSystem.OrderCancelled -= HandleOrderFinished;
        subscribed = false;

        ClearRuntimeForLoad();
    }

    private void HandleOrderCreated(RestaurantOrder order)
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring ||
            order == null ||
            sessionsByLegacyOrderId.ContainsKey(order.OrderId))
        {
            return;
        }

        if (!TryReserveWholeOrder(order, out OrderInventorySession session, out string error))
        {
            Debug.LogWarning(
                "368CD inventario rechazó la comanda " + order.OrderId + ": " + error,
                this);

            if (!order.IsFinished)
            {
                orderSystem.CancelOrder(order);
            }
            return;
        }

        sessionsByLegacyOrderId.Add(order.OrderId, session);
        order.StateChanged += HandleOrderStateChanged;
        Log("Comanda " + order.OrderId + " reservó ingredientes para " +
            session.ReservationIdByLineId.Count + " línea(s).");
    }

    private bool TryReserveWholeOrder(
        RestaurantOrder order,
        out OrderInventorySession session,
        out string error)
    {
        session = null;
        error = string.Empty;

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        BistroBuilderCanonicalOrderIntegrationService integration =
            orderSystem.CanonicalIntegrationService;
        if (integration == null || integration.CanonicalOrderService == null ||
            !integration.CanonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder canonicalOrder))
        {
            error = "No se pudo leer la comanda canónica enlazada.";
            return false;
        }

        var createdReservationIds = new List<string>();
        var result = new OrderInventorySession(order);

        for (int lineIndex = 0; lineIndex < canonicalOrder.Lines.Count; lineIndex++)
        {
            BistroBuilderCanonicalOrderLine line = canonicalOrder.Lines[lineIndex];
            if (line == null)
            {
                error = "La comanda contiene una línea canónica nula.";
                RollbackCreatedReservations(order, createdReservationIds);
                return false;
            }

            if (!recipeCatalogService.TryGetRecipeByDishId(
                    line.DishId,
                    out BistroBuilderRecipeDefinition recipe))
            {
                error = "No existe receta para " + line.DishId + ".";
                RollbackCreatedReservations(order, createdReservationIds);
                return false;
            }

            if (!TryBuildReservationLines(recipe, out List<BistroBuilderInventoryQuantityLine> quantities, out error))
            {
                error = "Línea " + line.LineId + ": " + error;
                RollbackCreatedReservations(order, createdReservationIds);
                return false;
            }

            string reservationId = BuildReservationId(order.CanonicalOrderId, line.LineId);
            string operationId = BuildOperationId("reserve", order.CanonicalOrderId, line.LineId, string.Empty);
            string sourceId = BuildSourceId(order.CanonicalOrderId, line.LineId);

            if (!inventoryService.TryCreateReservation(
                    operationId,
                    reservationId,
                    sourceId,
                    quantities,
                    out _,
                    out error))
            {
                error = "No se pudo reservar " + line.DishId + ": " + error;
                RollbackCreatedReservations(order, createdReservationIds);
                return false;
            }

            createdReservationIds.Add(reservationId);
            result.ReservationIdByLineId.Add(line.LineId, reservationId);
        }

        session = result;
        return true;
    }

    private bool TryBuildReservationLines(
        BistroBuilderRecipeDefinition recipe,
        out List<BistroBuilderInventoryQuantityLine> quantities,
        out string error)
    {
        quantities = new List<BistroBuilderInventoryQuantityLine>();
        error = string.Empty;

        if (recipe == null || !recipe.TryValidate(out error))
        {
            return false;
        }

        var aggregated = new SortedDictionary<string, long>(StringComparer.Ordinal);
        for (int index = 0; index < recipe.Ingredients.Count; index++)
        {
            BistroBuilderRecipeIngredientAmount amount = recipe.Ingredients[index];
            if (amount == null || !amount.TryGetCanonicalMilliUnits(out long batchQuantity, out error))
            {
                return false;
            }

            // Una línea canónica representa una unidad vendida. Si la receta
            // produce varias porciones, reservamos exactamente una porción,
            // redondeando hacia arriba para no prometer stock inexistente.
            long perPortion = DivideCeiling(batchQuantity, recipe.YieldPortions);
            string ingredientId = amount.Ingredient.IngredientId;
            aggregated.TryGetValue(ingredientId, out long current);
            checked
            {
                aggregated[ingredientId] = current + perPortion;
            }
        }

        foreach (KeyValuePair<string, long> pair in aggregated)
        {
            quantities.Add(new BistroBuilderInventoryQuantityLine(pair.Key, pair.Value));
        }
        return quantities.Count > 0;
    }

    private void HandleOrderStateChanged(RestaurantOrder order, OrderState state)
    {
        if (order == null || !sessionsByLegacyOrderId.TryGetValue(order.OrderId, out OrderInventorySession session))
        {
            return;
        }

        if (state == OrderState.Cancelled)
        {
            ReleaseAllActiveReservations(session);
            RemoveSession(order);
        }
    }


    /// <summary>
    /// Consume exactamente la reserva de una línea cuando la autoridad de
    /// cocina confirma su transición a Preparing. Los pases posteriores
    /// permanecen reservados hasta que realmente comienzan.
    /// </summary>
    public bool TryConsumeLine(
        RestaurantOrder order,
        string canonicalLineId,
        out string error)
    {
        error = string.Empty;
        if (order == null || string.IsNullOrWhiteSpace(canonicalLineId) ||
            !sessionsByLegacyOrderId.TryGetValue(order.OrderId, out OrderInventorySession session) ||
            !session.ReservationIdByLineId.TryGetValue(canonicalLineId, out string reservationId))
        {
            error = "No existe una reserva 368CD para la línea indicada.";
            return false;
        }

        string operationId = BuildOperationId(
            "consume", order.CanonicalOrderId, canonicalLineId, string.Empty);
        if (!inventoryService.TryConsumeReservation(
                operationId,
                reservationId,
                "Inicio de preparación de la línea " + canonicalLineId + ".",
                out error))
        {
            return IsReservationConsumed(reservationId);
        }

        Log("Reserva " + reservationId +
            " consumida al comenzar la preparación de su línea.");
        return true;
    }

    private bool IsReservationConsumed(string reservationId)
    {
        return inventoryService.TryGetReservationSnapshot(
                   reservationId,
                   out BistroBuilderInventoryReservationSnapshot snapshot) &&
               snapshot.Status == BistroBuilderInventoryReservationStatus.Consumed;
    }
    private void ReleaseAllActiveReservations(OrderInventorySession session)
    {
        foreach (KeyValuePair<string, string> pair in session.ReservationIdByLineId)
        {
            if (IsAlreadyClosedReservation(pair.Value))
            {
                continue;
            }

            string operationId = BuildOperationId("release", session.Order.CanonicalOrderId, pair.Key, string.Empty);
            if (!inventoryService.TryReleaseReservation(
                    operationId,
                    pair.Value,
                    "Cancelación previa a consumo de la línea " + pair.Key + ".",
                    out string error))
            {
                Debug.LogError("368CD no pudo liberar " + pair.Value + ": " + error, this);
            }
        }
    }

    private bool IsAlreadyClosedReservation(string reservationId)
    {
        return inventoryService.TryGetReservationSnapshot(
                   reservationId,
                   out BistroBuilderInventoryReservationSnapshot snapshot) &&
               snapshot.Status != BistroBuilderInventoryReservationStatus.Active;
    }

    private void RollbackCreatedReservations(RestaurantOrder order, List<string> reservationIds)
    {
        for (int index = reservationIds.Count - 1; index >= 0; index--)
        {
            string reservationId = reservationIds[index];
            inventoryService.TryReleaseReservation(
                BuildOperationId("rollback", order.CanonicalOrderId, reservationId, string.Empty),
                reservationId,
                "Rollback atómico de aceptación de comanda.",
                out _);
        }
    }

    private void HandleOrderFinished(RestaurantOrder order)
    {
        if (order == null || !sessionsByLegacyOrderId.TryGetValue(order.OrderId, out OrderInventorySession session))
        {
            return;
        }

        if (order.CurrentState == OrderState.Cancelled)
        {
            ReleaseAllActiveReservations(session);
        }
        RemoveSession(order);
    }

    private void RemoveSession(RestaurantOrder order)
    {
        order.StateChanged -= HandleOrderStateChanged;
        sessionsByLegacyOrderId.Remove(order.OrderId);
    }

    private static bool IsReservationStatusCompatible(
        BistroBuilderCanonicalOrderLineState lineState,
        BistroBuilderInventoryReservationStatus reservationStatus
    )
    {
        switch (lineState)
        {
            case BistroBuilderCanonicalOrderLineState.Draft:
            case BistroBuilderCanonicalOrderLineState.Submitted:
            case BistroBuilderCanonicalOrderLineState.Queued:
                return reservationStatus ==
                       BistroBuilderInventoryReservationStatus.Active;

            case BistroBuilderCanonicalOrderLineState.Preparing:
            case BistroBuilderCanonicalOrderLineState.ReadyForPickup:
            case BistroBuilderCanonicalOrderLineState.AssignedForDelivery:
            case BistroBuilderCanonicalOrderLineState.InTransit:
            case BistroBuilderCanonicalOrderLineState.Served:
            case BistroBuilderCanonicalOrderLineState.Consumed:
                return reservationStatus ==
                       BistroBuilderInventoryReservationStatus.Consumed;

            case BistroBuilderCanonicalOrderLineState.Cancelled:
            case BistroBuilderCanonicalOrderLineState.Failed:
                return reservationStatus !=
                       BistroBuilderInventoryReservationStatus.Active;

            default:
                return false;
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (orderSystem == null) TryGetComponent(out orderSystem);
        if (inventoryService == null) TryGetComponent(out inventoryService);
        if (recipeCatalogService == null) TryGetComponent(out recipeCatalogService);
    }

    private void Log(string message)
    {
        if (logTransitions)
        {
            Debug.Log("368CD inventario: " + message, this);
        }
    }

    internal static long DivideCeiling(long numerator, int denominator)
    {
        if (numerator <= 0L || denominator <= 0)
        {
            throw new ArgumentOutOfRangeException();
        }
        long quotient = numerator / denominator;
        return numerator % denominator == 0L ? quotient : quotient + 1L;
    }

    internal static string BuildReservationId(string orderId, string lineId)
    {
        return BoundRuntimeId(
            "inventory_reservation_" + NormalizeForId(orderId) + "_" +
            NormalizeForId(lineId)
        );
    }

    internal static string BuildOperationId(
        string action,
        string orderId,
        string lineId,
        string suffix
    )
    {
        string value = "inventory_" + NormalizeForId(action) + "_" +
                       NormalizeForId(orderId) + "_" +
                       NormalizeForId(lineId);

        if (!string.IsNullOrWhiteSpace(suffix))
        {
            value += "_" + NormalizeForId(suffix);
        }

        return BoundRuntimeId(value);
    }

    private static string BuildSourceId(string orderId, string lineId)
    {
        return BoundRuntimeId(
            "order_line_" + NormalizeForId(orderId) + "_" +
            NormalizeForId(lineId)
        );
    }

    /// <summary>
    /// Conserva el texto completo mientras respeta el contrato runtime. Si
    /// una combinación futura de IDs supera 160 caracteres, mantiene un
    /// prefijo legible y añade una huella SHA-256 determinista para evitar
    /// truncados ambiguos y colisiones prácticas entre operaciones.
    /// </summary>
    private static string BoundRuntimeId(string value)
    {
        string normalized = value != null ? value.Trim() : string.Empty;
        int maximumLength =
            BistroBuilderInventoryRuntimeIdUtility.MaximumRuntimeIdLength;

        if (normalized.Length <= maximumLength)
        {
            return normalized;
        }

        byte[] hash;
        using (SHA256 algorithm = SHA256.Create())
        {
            hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        }

        var suffix = new StringBuilder(32);
        for (int index = 0; index < 16; index++)
        {
            suffix.Append(hash[index].ToString("x2"));
        }

        int prefixLength = maximumLength - suffix.Length - 1;
        return normalized.Substring(0, prefixLength) + "_" + suffix;
    }

    private static string NormalizeForId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "none";
        char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (!(char.IsLetterOrDigit(c) || c == '_')) chars[i] = '_';
        }
        return new string(chars);
    }

    private sealed class OrderInventorySession
    {
        public readonly RestaurantOrder Order;
        public readonly Dictionary<string, string> ReservationIdByLineId =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public OrderInventorySession(RestaurantOrder order)
        {
            Order = order;
        }
    }
}
