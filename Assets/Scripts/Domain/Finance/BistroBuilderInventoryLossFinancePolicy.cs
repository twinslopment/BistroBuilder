using System;

/// <summary>
/// Contrato financiero de salidas de inventario sin venta.
/// La valoración se congela al producirse la salida física y nunca mueve stock.
/// </summary>
public static class BistroBuilderInventoryLossFinancePolicy
{
    public const string SourceSystemId = "inventory.loss";
    public const string ExpirationCategoryId = "expense.inventory.expiration";
    public const string WasteCategoryId = "expense.inventory.waste";

    public static bool IsRecognizableLoss(
        BistroBuilderInventoryTransactionSnapshot transaction)
    {
        return transaction.TransactionType ==
                   BistroBuilderInventoryTransactionType.Expiration ||
               transaction.TransactionType ==
                   BistroBuilderInventoryTransactionType.Waste;
    }

    public static string ResolveCategoryId(
        BistroBuilderInventoryTransactionType transactionType)
    {
        return transactionType == BistroBuilderInventoryTransactionType.Expiration
            ? ExpirationCategoryId
            : transactionType == BistroBuilderInventoryTransactionType.Waste
                ? WasteCategoryId
                : string.Empty;
    }

    public static string BuildOperationId(string inventoryOperationId)
    {
        string normalized = string.IsNullOrWhiteSpace(inventoryOperationId)
            ? string.Empty
            : inventoryOperationId.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(normalized)
            ? string.Empty
            : "inventory_loss_" + normalized;
    }

    public static bool TryBuildRequest(
        BistroBuilderInventoryTransactionSnapshot transaction,
        long estimatedCostCents,
        int dayIndex,
        int minuteOfDay,
        out BistroBuilderFinanceTransactionRequest request,
        out string error)
    {
        request = null;
        error = string.Empty;

        if (!IsRecognizableLoss(transaction) ||
            transaction.QuantityCanonicalMilliUnits <= 0L ||
            estimatedCostCents < 0L)
        {
            error = "La salida de inventario no representa una pérdida valorable.";
            return false;
        }

        // Una salida de coste cero es válida y no necesita movimiento monetario.
        if (estimatedCostCents == 0L)
        {
            return true;
        }

        string operationId = BuildOperationId(transaction.OperationId);
        string categoryId = ResolveCategoryId(transaction.TransactionType);
        if (string.IsNullOrWhiteSpace(operationId) ||
            string.IsNullOrWhiteSpace(categoryId))
        {
            error = "La pérdida de inventario no tiene identidad financiera estable.";
            return false;
        }

        request = new BistroBuilderFinanceTransactionRequest
        {
            operationId = operationId,
            sourceSystemId = SourceSystemId,
            sourceReferenceId = transaction.TransactionId,
            categoryId = categoryId,
            kind = BistroBuilderFinanceTransactionKind.Debit,
            amountCents = estimatedCostCents,
            dayIndex = dayIndex,
            minuteOfDay = minuteOfDay,
            description = transaction.TransactionType ==
                          BistroBuilderInventoryTransactionType.Expiration
                ? "Baja económica por caducidad de " + transaction.IngredientId
                : "Baja económica por merma de " + transaction.IngredientId
        };

        return BistroBuilderFinanceEngine.TryValidateRequest(request, out error);
    }
}
