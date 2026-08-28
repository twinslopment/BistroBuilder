using System.Collections.Generic;

/// <summary>
/// Contexto inmutable de un cobro final sobre el que sistemas externos pueden
/// proponer un ajuste comercial sin convertirse en autoridad de Finanzas.
/// </summary>
public sealed class BistroBuilderSalesPaymentAdjustmentContext
{
    public string canonicalOrderId = string.Empty;
    public string customerGroupReferenceId = string.Empty;
    public string acquisitionSegmentId = string.Empty;
    public BistroBuilderServiceMode serviceMode;
    public BistroBuilderMealServiceAvailability mealService;
    public int dayIndex;
    public int minuteOfDay;
    public long baseAmountCents;
    public List<string> orderedDishIds = new List<string>();
}

/// <summary>
/// Puerto universal de ajuste de ticket. 100 puntos básicos equivalen a 1 %.
/// El proveedor no publica dinero: solo propone un porcentaje al puente 3B.
/// </summary>
public interface IBistroBuilderSalesPaymentAdjustmentProvider
{
    string AdjustmentProviderId { get; }

    bool TryGetAdjustmentBasisPoints(
        BistroBuilderSalesPaymentAdjustmentContext context,
        out int adjustmentBasisPoints,
        out string error);
}
