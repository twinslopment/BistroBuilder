using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Servicio autoritativo de recepción de mercancía 2.2B.
///
/// Responsabilidades:
/// - Validar una recepción completa antes de modificar existencias.
/// - Aplicarla de forma atómica sobre el inventario canónico.
/// - Mantener ReceiptId idempotente mediante el OperationId del inventario.
/// - Publicar un evento para que presentación represente el reparto.
///
/// No modela rutas, vehículos, empleados, turnos ni múltiples almacenes.
/// La animación del repartidor es deliberadamente no autoritativa.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Inventory/Goods Receiving Service")]
public sealed class BistroBuilderGoodsReceivingService : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderInventoryService inventoryService;

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [Header("Depuración")]

    [SerializeField]
    private bool logReceipts = true;

    public event Action<BistroBuilderGoodsReceiptSnapshot> ReceiptAccepted;

    public BistroBuilderInventoryService InventoryService => inventoryService;

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (inventoryService == null)
        {
            error = "Falta BistroBuilderInventoryService.";
            return false;
        }

        if (generalGameStateService == null)
        {
            error = "Falta BistroBuilderGeneralGameStateService.";
            return false;
        }

        if (!inventoryService.ValidateConfiguration(out error))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Acepta una entrega y actualiza el inventario de forma atómica.
    ///
    /// La representación visual se lanza después mediante ReceiptAccepted.
    /// Por tanto, un guardado durante la animación no puede perder ni duplicar
    /// mercancía: el libro de inventario ya contiene la recepción autoritativa.
    /// </summary>
    public bool TryReceiveGoods(
        string receiptId,
        string sourceId,
        IReadOnlyList<BistroBuilderInventoryQuantityLine> lines,
        string reason,
        out BistroBuilderGoodsReceiptSnapshot receipt,
        out string error
    )
    {
        receipt = null;
        error = string.Empty;
        CacheDependenciesIfNeeded();

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        string normalizedReceipt =
            BistroBuilderInventoryRuntimeIdUtility.Normalize(receiptId);
        string normalizedSource =
            BistroBuilderInventoryRuntimeIdUtility.Normalize(sourceId);

        if (!BistroBuilderInventoryRuntimeIdUtility.TryValidateNormalized(
                normalizedReceipt,
                "ReceiptId",
                out error
            ) ||
            !BistroBuilderInventoryRuntimeIdUtility.TryValidateNormalized(
                normalizedSource,
                "SourceId de recepción",
                out error
            ))
        {
            return false;
        }

        if (!TryNormalizeLines(lines, out List<BistroBuilderGoodsReceiptLineSnapshot> normalizedLines, out error))
        {
            return false;
        }

        var inventoryLines = new List<BistroBuilderInventoryQuantityLine>(
            normalizedLines.Count
        );
        for (int index = 0; index < normalizedLines.Count; index++)
        {
            BistroBuilderGoodsReceiptLineSnapshot line = normalizedLines[index];
            inventoryLines.Add(
                new BistroBuilderInventoryQuantityLine(
                    line.IngredientId,
                    line.CanonicalMilliUnits
                )
            );
        }

        if (!inventoryService.TryReceivePurchaseBatch(
                normalizedReceipt,
                normalizedSource,
                inventoryLines,
                string.IsNullOrWhiteSpace(reason)
                    ? "Recepción de mercancía " + normalizedReceipt + "."
                    : reason,
                out bool wasReplayed,
                out error
            ))
        {
            return false;
        }

        receipt = new BistroBuilderGoodsReceiptSnapshot(
            normalizedReceipt,
            normalizedSource,
            Math.Max(1, generalGameStateService.DayIndex),
            inventoryService.RuntimeRevision,
            wasReplayed,
            normalizedLines
        );
        if (wasReplayed)
        {
            if (logReceipts)
            {
                Debug.Log(
                    "Recepción " + normalizedReceipt +
                    " ya aplicada; se devuelve de forma idempotente sin " +
                    "repetir la representación visual.",
                    this
                );
            }

            return true;
        }

        if (logReceipts)
        {
            Debug.Log(
                "Recepción " + normalizedReceipt + " aceptada con " +
                normalizedLines.Count + " ingrediente(s) para " +
                BistroBuilderGoodsReceivingIds.PrimaryWarehouse + ".",
                this
            );
        }

        PublishReceiptAccepted(receipt);
        return true;
    }

    private static bool TryNormalizeLines(
        IReadOnlyList<BistroBuilderInventoryQuantityLine> source,
        out List<BistroBuilderGoodsReceiptLineSnapshot> result,
        out string error
    )
    {
        result = new List<BistroBuilderGoodsReceiptLineSnapshot>();
        error = string.Empty;

        if (source == null || source.Count == 0)
        {
            error = "La recepción debe contener al menos una línea.";
            return false;
        }

        var aggregated = new SortedDictionary<string, long>(
            StringComparer.Ordinal
        );

        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderInventoryQuantityLine line = source[index];
            if (line == null)
            {
                error = "La recepción contiene una línea nula en la posición " +
                        index + ".";
                return false;
            }

            string ingredientId = BistroBuilderMenuIdUtility.NormalizeStableId(
                line.IngredientId
            );
            if (!BistroBuilderMenuIdUtility.IsValidStableId(ingredientId))
            {
                error = "La recepción contiene un IngredientId inválido: " +
                        (line.IngredientId ?? string.Empty) + ".";
                return false;
            }

            if (line.CanonicalMilliUnits <= 0L ||
                line.CanonicalMilliUnits >
                    BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits)
            {
                error = "La recepción de " + ingredientId +
                        " contiene una cantidad inválida.";
                return false;
            }

            aggregated.TryGetValue(ingredientId, out long current);
            try
            {
                long combined = checked(current + line.CanonicalMilliUnits);
                if (combined >
                    BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits)
                {
                    error = "La recepción de " + ingredientId +
                            " excede el rango permitido.";
                    return false;
                }

                aggregated[ingredientId] = combined;
            }
            catch (OverflowException)
            {
                error = "La recepción de " + ingredientId +
                        " excede el rango permitido.";
                return false;
            }
        }

        foreach (KeyValuePair<string, long> pair in aggregated)
        {
            result.Add(
                new BistroBuilderGoodsReceiptLineSnapshot(
                    pair.Key,
                    pair.Value
                )
            );
        }

        return result.Count > 0;
    }

    private void PublishReceiptAccepted(BistroBuilderGoodsReceiptSnapshot receipt)
    {
        Action<BistroBuilderGoodsReceiptSnapshot> handlers = ReceiptAccepted;
        if (handlers == null)
        {
            return;
        }

        Delegate[] invocationList = handlers.GetInvocationList();
        for (int index = 0; index < invocationList.Length; index++)
        {
            try
            {
                ((Action<BistroBuilderGoodsReceiptSnapshot>)
                    invocationList[index]).Invoke(receipt);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (inventoryService == null)
        {
            TryGetComponent(out inventoryService);
        }

        if (generalGameStateService == null)
        {
            TryGetComponent(out generalGameStateService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
