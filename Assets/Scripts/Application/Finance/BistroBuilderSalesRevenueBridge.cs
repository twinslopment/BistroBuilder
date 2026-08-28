using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Integra cobros reales del servicio con la autoridad financiera 3A.
///
/// OrderCompleted representa el pago final de mesa/barra y también el cierre
/// intermedio de WaitingAtBar. Este último nunca se contabiliza aquí: su cargo
/// se incorpora a la futura cuenta de mesa.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Sales Revenue Bridge")]
public sealed class BistroBuilderSalesRevenueBridge : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderFinanceService financeService;

    [SerializeField]
    private OrderSystem orderSystem;

    [SerializeField]
    private BistroBuilderBarServiceSystem barServiceSystem;

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [SerializeField]
    private GameClock gameClock;

    private readonly List<IBistroBuilderSalesPaymentAdjustmentProvider>
        paymentAdjustmentProviders =
            new List<IBistroBuilderSalesPaymentAdjustmentProvider>(4);
    private readonly HashSet<string> paymentAdjustmentProviderIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<string> orderedDishIds = new List<string>(16);

    public BistroBuilderFinanceService FinanceService => financeService;
    public OrderSystem OrderSystem => orderSystem;
    public BistroBuilderBarServiceSystem BarServiceSystem => barServiceSystem;
    public BistroBuilderGeneralGameStateService GeneralGameStateService =>
        generalGameStateService;
    public GameClock GameClock => gameClock;
    public long LastBaseAmountCents { get; private set; }
    public long LastFinalAmountCents { get; private set; }
    public int LastPaymentAdjustmentBasisPoints { get; private set; }
    public string LastAdjustedOrderId { get; private set; } = string.Empty;

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        if (financeService == null)
        {
            error = "Falta BistroBuilderFinanceService 3A.";
            return false;
        }

        if (!financeService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (orderSystem == null)
        {
            error = "Falta OrderSystem.";
            return false;
        }

        if (!orderSystem.ValidateConfiguration(out error))
        {
            return false;
        }

        if (barServiceSystem == null)
        {
            error = "Falta BistroBuilderBarServiceSystem para cargos transferidos.";
            return false;
        }

        if (generalGameStateService == null || gameClock == null)
        {
            error = "Faltan el día o la hora canónicos de la partida.";
            return false;
        }

        return generalGameStateService.ValidateConfiguration(out error);
    }

    private bool TryRecordCompletedOrder(
        RestaurantOrder order,
        out string error)
    {
        if (order == null || !order.HasCanonicalOrder)
        {
            error = "La comanda pagada no contiene identidad canónica.";
            return false;
        }

        BistroBuilderCanonicalOrderService canonicalOrderService =
            orderSystem != null && orderSystem.CanonicalIntegrationService != null
                ? orderSystem.CanonicalIntegrationService.CanonicalOrderService
                : null;

        if (canonicalOrderService == null ||
            !canonicalOrderService.TryGetOrderSnapshot(
                order.CanonicalOrderId,
                out BistroBuilderCanonicalOrder snapshot) ||
            snapshot == null)
        {
            error = "No se encontró la comanda canónica del cobro.";
            return false;
        }

        if (snapshot.State != BistroBuilderCanonicalOrderState.Completed)
        {
            error = "La comanda canónica aún no está completada.";
            return false;
        }

        if (snapshot.ServiceMode != order.ServiceMode)
        {
            error = "La modalidad legacy y canónica del cobro no coinciden.";
            return false;
        }

        if (snapshot.ServiceMode == BistroBuilderServiceMode.WaitingAtBar)
        {
            error = string.Empty;
            return true;
        }

        if (financeService == null ||
            barServiceSystem == null ||
            generalGameStateService == null ||
            gameClock == null)
        {
            error = "3B no tiene todas sus autoridades de cobro disponibles.";
            return false;
        }

        int canonicalAmountCents = snapshot.CalculateTotalPriceCents();
        long amountCents;

        if (snapshot.ServiceMode == BistroBuilderServiceMode.TableService)
        {
            if (order.CustomerGroup == null)
            {
                error = "La comanda de mesa no conserva su grupo de clientes.";
                return false;
            }

            int transferredBarCents =
                barServiceSystem.GetPendingTransferredChargeCents(
                    order.CustomerGroup);

            if (!BistroBuilderSalesRevenuePolicy.TryCalculateTablePaymentAmount(
                    canonicalAmountCents,
                    transferredBarCents,
                    out amountCents,
                    out error))
            {
                return false;
            }
        }
        else
        {
            amountCents = canonicalAmountCents;
        }

        long baseAmountCents = amountCents;
        if (!TryApplyPaymentAdjustments(
                order,
                snapshot,
                baseAmountCents,
                out amountCents,
                out int paymentAdjustmentBasisPoints,
                out error))
        {
            return false;
        }

        LastBaseAmountCents = baseAmountCents;
        LastFinalAmountCents = amountCents;
        LastPaymentAdjustmentBasisPoints = paymentAdjustmentBasisPoints;
        LastAdjustedOrderId = snapshot.OrderId;

        // La carta admite platos gratuitos: un pago final de 0 € es válido,
        // pero no constituye un movimiento monetario.
        if (amountCents == 0L)
        {
            error = string.Empty;
            return true;
        }

        if (!BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                snapshot.OrderId,
                snapshot.ServiceMode,
                snapshot.MealService,
                amountCents,
                generalGameStateService.DayIndex,
                gameClock.Hour * 60 + gameClock.Minute,
                out BistroBuilderFinanceTransactionRequest request,
                out error))
        {
            return false;
        }

        return financeService.TryPostTransaction(request, out _, out error);
    }

    private bool TryApplyPaymentAdjustments(
        RestaurantOrder order,
        BistroBuilderCanonicalOrder snapshot,
        long baseAmountCents,
        out long adjustedAmountCents,
        out int aggregateBasisPoints,
        out string error)
    {
        adjustedAmountCents = baseAmountCents;
        aggregateBasisPoints = 0;

        if (!TryCollectPaymentAdjustmentProviders(out error))
            return false;

        if (paymentAdjustmentProviders.Count == 0)
        {
            return BistroBuilderSalesRevenuePolicy.TryApplyPaymentAdjustment(
                baseAmountCents,
                0,
                out adjustedAmountCents,
                out error);
        }

        BistroBuilderSalesPaymentAdjustmentContext context =
            BuildPaymentAdjustmentContext(order, snapshot, baseAmountCents);
        long aggregate = 0L;

        for (int index = 0;
             index < paymentAdjustmentProviders.Count;
             index++)
        {
            if (!paymentAdjustmentProviders[index]
                    .TryGetAdjustmentBasisPoints(
                        context,
                        out int value,
                        out error))
                return false;

            if (value < -9000 || value > 50000)
            {
                error = "Un proveedor de cobro devolvió un ajuste fuera de rango.";
                return false;
            }

            aggregate += value;
        }

        if (aggregate < -9000L || aggregate > 50000L)
        {
            error = "La suma de ajustes comerciales queda fuera de rango.";
            return false;
        }

        aggregateBasisPoints = (int)aggregate;
        return BistroBuilderSalesRevenuePolicy.TryApplyPaymentAdjustment(
            baseAmountCents,
            aggregateBasisPoints,
            out adjustedAmountCents,
            out error);
    }

    private bool TryCollectPaymentAdjustmentProviders(out string error)
    {
        paymentAdjustmentProviders.Clear();
        paymentAdjustmentProviderIds.Clear();

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int index = 0; index < behaviours.Length; index++)
        {
            if (!(behaviours[index] is
                    IBistroBuilderSalesPaymentAdjustmentProvider provider))
                continue;

            string providerId = NormalizeProviderId(provider.AdjustmentProviderId);
            if (!IsSafeProviderId(providerId) ||
                !paymentAdjustmentProviderIds.Add(providerId))
            {
                error = "Existe un proveedor de ajuste de cobro inválido o duplicado.";
                return false;
            }

            paymentAdjustmentProviders.Add(provider);
        }

        error = string.Empty;
        return true;
    }

    private BistroBuilderSalesPaymentAdjustmentContext
        BuildPaymentAdjustmentContext(
            RestaurantOrder order,
            BistroBuilderCanonicalOrder snapshot,
            long baseAmountCents)
    {
        orderedDishIds.Clear();
        for (int index = 0; index < snapshot.Lines.Count; index++)
        {
            BistroBuilderCanonicalOrderLine line = snapshot.Lines[index];
            if (line == null ||
                line.State == BistroBuilderCanonicalOrderLineState.Cancelled)
                continue;

            string dishId = BistroBuilderMenuIdUtility.NormalizeStableId(
                line.DishId);
            if (BistroBuilderMenuIdUtility.IsValidStableId(dishId) &&
                !orderedDishIds.Contains(dishId))
                orderedDishIds.Add(dishId);
        }

        string segmentId = "general";
        if (order != null && order.CustomerGroup != null)
        {
            BistroBuilderCustomerAcquisitionTag tag =
                order.CustomerGroup.GetComponent<
                    BistroBuilderCustomerAcquisitionTag>();
            if (tag != null && !string.IsNullOrWhiteSpace(tag.SegmentId))
                segmentId = tag.SegmentId;
        }

        return new BistroBuilderSalesPaymentAdjustmentContext
        {
            canonicalOrderId = snapshot.OrderId,
            customerGroupReferenceId = snapshot.CustomerGroupReferenceId,
            acquisitionSegmentId = segmentId,
            serviceMode = snapshot.ServiceMode,
            mealService = snapshot.MealService,
            dayIndex = generalGameStateService.DayIndex,
            minuteOfDay = gameClock.Hour * 60 + gameClock.Minute,
            baseAmountCents = baseAmountCents,
            orderedDishIds = new List<string>(orderedDishIds)
        };
    }

    private static string NormalizeProviderId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static bool IsSafeProviderId(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 96)
            return false;

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            bool allowed =
                character >= 'a' && character <= 'z' ||
                character >= '0' && character <= '9' ||
                character == '_' || character == '-' || character == '.';
            if (!allowed)
                return false;
        }

        return true;
    }

    private void HandleOrderCompleted(RestaurantOrder order)
    {
        if (!TryRecordCompletedOrder(order, out string error))
        {
            Debug.LogError(
                "3B no pudo contabilizar un cobro completado. " + error,
                this);
        }
    }

    private void Subscribe()
    {
        if (orderSystem == null)
        {
            return;
        }

        orderSystem.OrderCompleted -= HandleOrderCompleted;
        orderSystem.OrderCompleted += HandleOrderCompleted;
    }

    private void Unsubscribe()
    {
        if (orderSystem != null)
        {
            orderSystem.OrderCompleted -= HandleOrderCompleted;
        }
    }
}
