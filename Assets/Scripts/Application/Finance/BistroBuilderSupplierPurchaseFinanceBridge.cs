using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Integra PurchaseOrders 2.3E con la autoridad financiera.
///
/// Confirmed y PendingDelivery reservan capacidad de caja como una proyección
/// derivada del estado canónico del pedido. Al pasar a InDelivery se registra
/// una única salida real en finance.runtime. No posee inventario ni otro ledger.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Supplier Purchase Finance Bridge")]
public sealed class BistroBuilderSupplierPurchaseFinanceBridge :
    MonoBehaviour,
    IBistroBuilderPurchaseOrderConfirmationGate
{
    [SerializeField]
    private BistroBuilderFinanceService financeService;

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [SerializeField]
    private GameClock gameClock;

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    private readonly List<BistroBuilderPurchaseOrderRecord> orders =
        new List<BistroBuilderPurchaseOrderRecord>(64);

    private BistroBuilderSupplierPurchaseOrderService orderService;
    private Coroutine bindRoutine;

    public bool IsBound => orderService != null;

    private void OnEnable()
    {
        if (saveGameService != null)
        {
            saveGameService.OperationCompleted -= HandleSaveOperationCompleted;
            saveGameService.OperationCompleted += HandleSaveOperationCompleted;
        }

        bindRoutine = StartCoroutine(BindWhenReady());
    }

    private void OnDisable()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        if (saveGameService != null)
        {
            saveGameService.OperationCompleted -= HandleSaveOperationCompleted;
        }

        UnbindOrderService();
    }

    public bool ValidateConfiguration(out string error)
    {
        if (financeService == null ||
            generalGameStateService == null ||
            gameClock == null ||
            saveGameService == null)
        {
            error = "3C necesita Finanzas, estado general, reloj y SaveGameService.";
            return false;
        }

        if (!financeService.ValidateConfiguration(out error))
        {
            return false;
        }

        return generalGameStateService.ValidateConfiguration(out error);
    }

    public bool TryAuthorizeConfirmation(
        BistroBuilderPurchaseOrderConfirmationPreview preview,
        out string error)
    {
        if (!TryGetFinancialPosition(
                out long committedCents,
                out _,
                out error))
        {
            return false;
        }

        return BistroBuilderSupplierPurchaseFinancePolicy.TryAuthorizeConfirmation(
            preview,
            financeService.CurrentBalanceCents,
            committedCents,
            financeService.CurrencyCode,
            out error);
    }

    public bool TryGetFinancialPosition(
        out long committedCents,
        out long availableCents,
        out string error)
    {
        committedCents = 0L;
        availableCents = 0L;

        if (!EnsureOrderService(out error))
        {
            return false;
        }

        orderService.CopyOrders(orders);
        if (!BistroBuilderSupplierPurchaseFinancePolicy.TryCalculateCommittedCents(
                orders,
                financeService.CurrencyCode,
                out committedCents,
                out error))
        {
            return false;
        }

        try
        {
            availableCents = checked(
                financeService.CurrentBalanceCents - committedCents);
        }
        catch (OverflowException)
        {
            error = "El saldo disponible desborda el rango monetario soportado.";
            return false;
        }

        return true;
    }

    public bool TryReconcileInDeliveryOrders(
        out int postedCount,
        out string error)
    {
        postedCount = 0;
        if (!EnsureOrderService(out error))
        {
            return false;
        }

        orderService.CopyOrders(orders);
        for (int index = 0; index < orders.Count; index++)
        {
            BistroBuilderPurchaseOrderRecord order = orders[index];
            if (order == null ||
                order.status != BistroBuilderPurchaseOrderStatus.InDelivery)
            {
                continue;
            }

            string operationId =
                BistroBuilderSupplierPurchaseFinancePolicy.BuildDebitOperationId(
                    order.purchaseOrderId);
            bool alreadyPosted = financeService.TryGetTransactionByOperationId(
                operationId,
                out _);
            int dayIndex = order.inDeliveryGameDay > 0
                ? order.inDeliveryGameDay
                : generalGameStateService.DayIndex;

            if (!TryPostDispatchPayment(order, dayIndex, out error))
            {
                return false;
            }

            if (!alreadyPosted)
            {
                postedCount++;
            }
        }

        error = string.Empty;
        return true;
    }

    private IEnumerator BindWhenReady()
    {
        while (isActiveAndEnabled)
        {
            BistroBuilderSupplierPurchaseOrderService candidate =
                BistroBuilderSupplierPurchaseOrderService.Instance;

            if (candidate != null && candidate.IsInitialized)
            {
                if (!BindOrderService(candidate, out string error))
                {
                    Debug.LogError("3C no pudo enlazar 2.3E. " + error, this);
                }
                bindRoutine = null;
                yield break;
            }

            yield return null;
        }

        bindRoutine = null;
    }

    private bool EnsureOrderService(out string error)
    {
        if (orderService != null && orderService.IsInitialized)
        {
            error = string.Empty;
            return true;
        }

        BistroBuilderSupplierPurchaseOrderService candidate =
            BistroBuilderSupplierPurchaseOrderService.Instance;
        if (candidate == null || !candidate.IsInitialized)
        {
            error = "La autoridad PurchaseOrder 2.3E no está disponible.";
            return false;
        }

        return BindOrderService(candidate, out error);
    }

    private bool BindOrderService(
        BistroBuilderSupplierPurchaseOrderService candidate,
        out string error)
    {
        if (ReferenceEquals(orderService, candidate))
        {
            error = string.Empty;
            return true;
        }

        UnbindOrderService();
        if (!candidate.TryBindConfirmationGate(this, out error))
        {
            return false;
        }

        orderService = candidate;
        orderService.OrderStateChanged -= HandleOrderStateChanged;
        orderService.OrderStateChanged += HandleOrderStateChanged;
        return true;
    }

    private void UnbindOrderService()
    {
        if (orderService == null)
        {
            return;
        }

        orderService.OrderStateChanged -= HandleOrderStateChanged;
        orderService.UnbindConfirmationGate(this);
        orderService = null;
    }

    private void HandleOrderStateChanged(BistroBuilderPurchaseOrderRecord order)
    {
        if (order == null ||
            order.status != BistroBuilderPurchaseOrderStatus.InDelivery)
        {
            return;
        }

        if (!TryPostDispatchPayment(
                order,
                generalGameStateService.DayIndex,
                out string error))
        {
            Debug.LogError(
                "3C no pudo registrar el pago del PurchaseOrder despachado. " +
                error,
                this);
        }
    }

    private bool TryPostDispatchPayment(
        BistroBuilderPurchaseOrderRecord order,
        int dayIndex,
        out string error)
    {
        string operationId =
            BistroBuilderSupplierPurchaseFinancePolicy.BuildDebitOperationId(
                order.purchaseOrderId);

        if (financeService.TryGetTransactionByOperationId(
                operationId,
                out BistroBuilderFinanceTransactionRecord existing))
        {
            if (existing.kind != BistroBuilderFinanceTransactionKind.Debit ||
                existing.amountCents != order.totalCents ||
                !string.Equals(
                    existing.sourceSystemId,
                    BistroBuilderSupplierPurchaseFinancePolicy.SourceSystemId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    existing.sourceReferenceId,
                    order.purchaseOrderId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    existing.categoryId,
                    BistroBuilderSupplierPurchaseFinancePolicy.CategoryId,
                    StringComparison.Ordinal))
            {
                error = "El PurchaseOrder ya tiene un movimiento financiero incompatible.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        if (!BistroBuilderSupplierPurchaseFinancePolicy.TryBuildDebitRequest(
                order,
                dayIndex,
                gameClock.Hour * 60 + gameClock.Minute,
                financeService.CurrencyCode,
                out BistroBuilderFinanceTransactionRequest request,
                out error))
        {
            return false;
        }

        return financeService.TryPostTransaction(request, out _, out error);
    }

    private void HandleSaveOperationCompleted(BistroBuilderSaveOperationResult result)
    {
        if (result == null ||
            !result.Succeeded ||
            result.OperationKind != BistroBuilderSaveOperationKind.Load)
        {
            return;
        }

        if (!TryReconcileInDeliveryOrders(out _, out string error))
        {
            Debug.LogError(
                "3C no pudo reconciliar pagos de proveedores tras Load. " + error,
                this);
        }
    }
}
