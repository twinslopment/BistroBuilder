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

    public BistroBuilderFinanceService FinanceService => financeService;
    public OrderSystem OrderSystem => orderSystem;
    public BistroBuilderBarServiceSystem BarServiceSystem => barServiceSystem;
    public BistroBuilderGeneralGameStateService GeneralGameStateService =>
        generalGameStateService;
    public GameClock GameClock => gameClock;

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
