using System;
using System.Collections.Generic;

/// <summary>
/// Reglas puras que proyectan PurchaseOrders de 2.3E sobre Finanzas.
/// No posee estado ni modifica pedidos, caja o inventario.
/// </summary>
public static class BistroBuilderSupplierPurchaseFinancePolicy
{
    public const string SourceSystemId = "supplier.purchase";
    public const string CategoryId = "purchases.suppliers.inventory";

    public static bool IsCommittedStatus(BistroBuilderPurchaseOrderStatus status)
    {
        return status == BistroBuilderPurchaseOrderStatus.Confirmed ||
               status == BistroBuilderPurchaseOrderStatus.PendingDelivery;
    }

    public static bool TryCalculateCommittedCents(
        IList<BistroBuilderPurchaseOrderRecord> orders,
        string financeCurrencyCode,
        out long committedCents,
        out string error)
    {
        committedCents = 0L;
        error = string.Empty;

        if (orders == null)
        {
            error = "La colección de PurchaseOrders es nula.";
            return false;
        }

        string currency = NormalizeCurrency(financeCurrencyCode);

        try
        {
            for (int index = 0; index < orders.Count; index++)
            {
                BistroBuilderPurchaseOrderRecord order = orders[index];
                if (order == null || !IsCommittedStatus(order.status))
                {
                    continue;
                }

                if (order.totalCents <= 0L)
                {
                    error = "El PurchaseOrder comprometido no tiene un total económico válido.";
                    return false;
                }

                if (!string.Equals(
                        NormalizeCurrency(order.currencyCode),
                        currency,
                        StringComparison.Ordinal))
                {
                    error = "El PurchaseOrder comprometido usa una moneda distinta de Finanzas.";
                    return false;
                }

                committedCents = checked(committedCents + order.totalCents);
            }
        }
        catch (OverflowException)
        {
            error = "Los compromisos de proveedores desbordan el rango monetario soportado.";
            return false;
        }

        return true;
    }

    public static bool TryAuthorizeConfirmation(
        BistroBuilderPurchaseOrderConfirmationPreview preview,
        long currentBalanceCents,
        long committedCents,
        string financeCurrencyCode,
        out string error)
    {
        error = string.Empty;

        if (preview == null || !preview.canConfirm || preview.totalCents <= 0L)
        {
            error = "La cotización del PurchaseOrder no es confirmable.";
            return false;
        }

        if (committedCents < 0L)
        {
            error = "El importe ya comprometido no es válido.";
            return false;
        }

        if (!string.Equals(
                NormalizeCurrency(preview.currencyCode),
                NormalizeCurrency(financeCurrencyCode),
                StringComparison.Ordinal))
        {
            error = "La moneda del PurchaseOrder no coincide con la caja del restaurante.";
            return false;
        }

        long availableCents;
        try
        {
            availableCents = checked(currentBalanceCents - committedCents);
        }
        catch (OverflowException)
        {
            error = "El saldo disponible desborda el rango monetario soportado.";
            return false;
        }

        if (preview.totalCents > availableCents)
        {
            error = "Fondos disponibles insuficientes para confirmar el pedido.";
            return false;
        }

        return true;
    }

    public static string BuildDebitOperationId(string purchaseOrderId)
    {
        string normalized = string.IsNullOrWhiteSpace(purchaseOrderId)
            ? string.Empty
            : purchaseOrderId.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(normalized)
            ? string.Empty
            : "supplier_purchase_" + normalized;
    }

    public static bool TryBuildDebitRequest(
        BistroBuilderPurchaseOrderRecord order,
        int dayIndex,
        int minuteOfDay,
        string financeCurrencyCode,
        out BistroBuilderFinanceTransactionRequest request,
        out string error)
    {
        request = null;
        error = string.Empty;

        if (order == null ||
            order.status != BistroBuilderPurchaseOrderStatus.InDelivery ||
            string.IsNullOrWhiteSpace(order.purchaseOrderId) ||
            order.totalCents <= 0L)
        {
            error = "El PurchaseOrder no representa una compra despachada pagable.";
            return false;
        }

        if (!string.Equals(
                NormalizeCurrency(order.currencyCode),
                NormalizeCurrency(financeCurrencyCode),
                StringComparison.Ordinal))
        {
            error = "La moneda del PurchaseOrder no coincide con la caja del restaurante.";
            return false;
        }

        if (dayIndex < 1 || minuteOfDay < 0 || minuteOfDay > 1439)
        {
            error = "La fecha de juego del pago a proveedor no es válida.";
            return false;
        }

        string operationId = BuildDebitOperationId(order.purchaseOrderId);
        if (string.IsNullOrEmpty(operationId))
        {
            error = "El PurchaseOrder no tiene identidad financiera estable.";
            return false;
        }

        request = new BistroBuilderFinanceTransactionRequest
        {
            operationId = operationId,
            sourceSystemId = SourceSystemId,
            sourceReferenceId = order.purchaseOrderId,
            categoryId = CategoryId,
            kind = BistroBuilderFinanceTransactionKind.Debit,
            amountCents = order.totalCents,
            dayIndex = dayIndex,
            minuteOfDay = minuteOfDay,
            description = string.IsNullOrWhiteSpace(order.displayCode)
                ? "Compra a proveedor " + order.purchaseOrderId
                : "Compra a proveedor " + order.displayCode
        };

        return true;
    }

    private static string NormalizeCurrency(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }
}
