using UnityEngine;

/// <summary>
/// Compatibilidad de compilación exclusiva del Editor para una revisión
/// intermedia de la Queen Test que denominaba incorrectamente "FinanceBridge"
/// a la baja de inventario. No se instala en escena, no existe en Player y no
/// publica movimientos de caja. El contrato productivo vive en Product Cost 3D.
///
/// Puede eliminarse en cuanto el archivo histórico de Queen quede sustituido
/// físicamente en todas las copias locales del repositorio.
/// </summary>
internal sealed class BistroBuilderInventoryLossFinanceBridge : ScriptableObject
{
    public bool TryRecognizeLoss(
        BistroBuilderInventoryTransactionSnapshot transaction,
        out string error)
    {
        BistroBuilderProductCostService productCost =
            Object.FindFirstObjectByType<BistroBuilderProductCostService>();
        if (productCost == null)
        {
            error = "Product Cost 3D no está disponible.";
            return false;
        }
        return productCost.TryRecordInventoryLoss(transaction, out error);
    }
}

/// <summary>
/// Identidad legacy únicamente para que una copia antigua de la prueba Editor
/// pueda compilar. No corresponde a ninguna operación de finance.runtime.
/// </summary>
internal static class BistroBuilderInventoryLossFinancePolicy
{
    public static string BuildOperationId(string inventoryOperationId)
    {
        string normalized = string.IsNullOrWhiteSpace(inventoryOperationId)
            ? string.Empty
            : inventoryOperationId.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(normalized)
            ? string.Empty
            : "legacy_non_cash_inventory_loss_" + normalized;
    }
}
