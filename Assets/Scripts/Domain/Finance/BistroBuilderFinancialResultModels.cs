using System;
using System.Collections.Generic;

/// <summary>
/// Calidad agregada del coste de producto utilizado por un resultado.
/// None significa que todavía no existen líneas consumidas valoradas.
/// </summary>
public enum BistroBuilderFinancialResultCostQuality
{
    None = 0,
    Estimated = 1,
    Mixed = 2,
    Actual = 3
}

/// <summary>
/// Resultado económico de un servicio concreto de un día.
///
/// No reparte alquiler, nóminas ni otros gastos generales de forma arbitraria.
/// Por servicio se muestra contribución bruta: ventas cobradas menos COGS.
/// </summary>
[Serializable]
public sealed class BistroBuilderServiceFinancialResult
{
    public int dayIndex = 1;
    public BistroBuilderMealServiceAvailability mealService =
        BistroBuilderMealServiceAvailability.Lunch;

    public long revenueCents;
    public long tableRevenueCents;
    public long barRevenueCents;
    public int paidOrderCount;

    public long costedSalesCents;
    public long productCostCents;
    public long theoreticalProductCostCents;
    public int consumedLineCount;
    public int estimatedLineCount;
    public int mixedLineCount;
    public int actualLineCount;
    public BistroBuilderFinancialResultCostQuality costQuality;

    /// <summary>
    /// Ventas cobradas menos precio de venta de líneas ya valoradas.
    /// Cero significa cobertura completa para el corte consultado.
    /// </summary>
    public long costCoverageGapCents;

    public long grossProfitCents;
    public int grossMarginBasisPoints;

    public bool HasActivity =>
        revenueCents != 0L ||
        productCostCents != 0L ||
        consumedLineCount != 0;

    public bool IsCostCoverageComplete => costCoverageGapCents == 0L;

    public BistroBuilderServiceFinancialResult DeepClone()
    {
        return new BistroBuilderServiceFinancialResult
        {
            dayIndex = dayIndex,
            mealService = mealService,
            revenueCents = revenueCents,
            tableRevenueCents = tableRevenueCents,
            barRevenueCents = barRevenueCents,
            paidOrderCount = paidOrderCount,
            costedSalesCents = costedSalesCents,
            productCostCents = productCostCents,
            theoreticalProductCostCents = theoreticalProductCostCents,
            consumedLineCount = consumedLineCount,
            estimatedLineCount = estimatedLineCount,
            mixedLineCount = mixedLineCount,
            actualLineCount = actualLineCount,
            costQuality = costQuality,
            costCoverageGapCents = costCoverageGapCents,
            grossProfitCents = grossProfitCents,
            grossMarginBasisPoints = grossMarginBasisPoints
        };
    }
}

/// <summary>
/// Resultado completo de un día.
///
/// Separa deliberadamente resultado y caja:
/// - Compras de inventario son caja; el producto se reconoce como COGS al consumir.
/// - Caducidad/merma se reconoce como pérdida cuando el inventario sale sin venta.
/// - Portes de proveedor son gasto de aprovisionamiento del día del pago.
/// - Principal de deuda y desembolsos de préstamos son solo caja.
/// - Intereses de financiación sí son gasto del periodo.
/// - Inversiones son caja, no gasto operativo del día.
/// - Reventa de activos es recuperación de caja independiente.
/// </summary>
[Serializable]
public sealed class BistroBuilderDayFinancialResult
{
    public int dayIndex = 1;

    public List<BistroBuilderServiceFinancialResult> serviceResults =
        new List<BistroBuilderServiceFinancialResult>(3);

    public long revenueCents;
    public long costedSalesCents;
    public long productCostCents;
    public long theoreticalProductCostCents;
    public int paidOrderCount;
    public int consumedLineCount;
    public int estimatedLineCount;
    public int mixedLineCount;
    public int actualLineCount;
    public BistroBuilderFinancialResultCostQuality costQuality;
    public long costCoverageGapCents;

    public long grossProfitCents;
    public int grossMarginBasisPoints;

    public long procurementShippingExpensesCents;
    public int supplierPaymentBreakdownMissingCount;
    public long recurringOperatingExpensesCents;
    public long payrollExpensesCents;
    public long marketingExpensesCents;
    public long assetDisposalExpensesCents;
    public long inventoryWriteOffExpensesCents;
    public long financingInterestExpensesCents;
    public long otherPeriodExpensesCents;
    public long totalPeriodExpensesCents;
    public long operatingResultCents;

    public long supplierPurchaseCashOutCents;
    public long investmentCashOutCents;
    public long debtPrincipalCashOutCents;
    public long loanProceedsCashInCents;
    public long assetResaleCashInCents;
    public long otherCashInCents;
    public long otherCashOutCents;
    public long totalCashInCents;
    public long totalCashOutCents;
    public long netCashChangeCents;

    public bool IsCostCoverageComplete => costCoverageGapCents == 0L;
    public bool HasCompleteSupplierPaymentBreakdown =>
        supplierPaymentBreakdownMissingCount == 0;

    /// <summary>
    /// Actividad operativa real del restaurante. No se activa por préstamos,
    /// inversiones ni simples movimientos de tesorería.
    /// </summary>
    public bool HasServiceActivity =>
        revenueCents != 0L ||
        productCostCents != 0L ||
        consumedLineCount != 0 ||
        paidOrderCount != 0;

    /// <summary>
    /// Día relevante para evaluar beneficio/pérdida. Incluye jornadas sin
    /// ventas que soportan gastos reales, pero excluye financiación pura.
    /// </summary>
    public bool HasOperatingResultActivity =>
        HasServiceActivity || totalPeriodExpensesCents != 0L;

    public bool HasFinancialActivity =>
        HasOperatingResultActivity ||
        totalCashInCents != 0L ||
        totalCashOutCents != 0L;

    public BistroBuilderDayFinancialResult DeepClone()
    {
        var clone = new BistroBuilderDayFinancialResult
        {
            dayIndex = dayIndex,
            revenueCents = revenueCents,
            costedSalesCents = costedSalesCents,
            productCostCents = productCostCents,
            theoreticalProductCostCents = theoreticalProductCostCents,
            paidOrderCount = paidOrderCount,
            consumedLineCount = consumedLineCount,
            estimatedLineCount = estimatedLineCount,
            mixedLineCount = mixedLineCount,
            actualLineCount = actualLineCount,
            costQuality = costQuality,
            costCoverageGapCents = costCoverageGapCents,
            grossProfitCents = grossProfitCents,
            grossMarginBasisPoints = grossMarginBasisPoints,
            procurementShippingExpensesCents = procurementShippingExpensesCents,
            supplierPaymentBreakdownMissingCount = supplierPaymentBreakdownMissingCount,
            recurringOperatingExpensesCents = recurringOperatingExpensesCents,
            payrollExpensesCents = payrollExpensesCents,
            marketingExpensesCents = marketingExpensesCents,
            assetDisposalExpensesCents = assetDisposalExpensesCents,
            inventoryWriteOffExpensesCents = inventoryWriteOffExpensesCents,
            financingInterestExpensesCents = financingInterestExpensesCents,
            otherPeriodExpensesCents = otherPeriodExpensesCents,
            totalPeriodExpensesCents = totalPeriodExpensesCents,
            operatingResultCents = operatingResultCents,
            supplierPurchaseCashOutCents = supplierPurchaseCashOutCents,
            investmentCashOutCents = investmentCashOutCents,
            debtPrincipalCashOutCents = debtPrincipalCashOutCents,
            loanProceedsCashInCents = loanProceedsCashInCents,
            assetResaleCashInCents = assetResaleCashInCents,
            otherCashInCents = otherCashInCents,
            otherCashOutCents = otherCashOutCents,
            totalCashInCents = totalCashInCents,
            totalCashOutCents = totalCashOutCents,
            netCashChangeCents = netCashChangeCents
        };

        if (serviceResults != null)
        {
            for (int index = 0; index < serviceResults.Count; index++)
            {
                BistroBuilderServiceFinancialResult service = serviceResults[index];
                if (service != null)
                {
                    clone.serviceResults.Add(service.DeepClone());
                }
            }
        }

        return clone;
    }
}
