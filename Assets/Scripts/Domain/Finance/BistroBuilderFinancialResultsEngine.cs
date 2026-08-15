using System;
using System.Collections.Generic;

/// <summary>
/// Proyección pura de resultados financieros.
///
/// No publica movimientos ni posee estado. Lee snapshots canónicos de 3A y 3D
/// y separa contabilidad de resultado y flujo de caja para evitar dobles cargos.
/// </summary>
public static class BistroBuilderFinancialResultsEngine
{
    private const string InvestmentPrefix = "investment.";
    private const string ExpensePrefix = "expense.";
    private const string OperatingExpensePrefix = "expense.operating.";
    private const string MarketingExpensePrefix = "expense.marketing";

    public static bool TryBuildServiceResult(
        BistroBuilderFinanceSnapshot financeSnapshot,
        BistroBuilderProductCostSnapshot productCostSnapshot,
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        out BistroBuilderServiceFinancialResult result,
        out string error)
    {
        result = null;

        if (!TryValidateInputs(
                financeSnapshot,
                productCostSnapshot,
                dayIndex,
                out error) ||
            !IsConcreteMealService(mealService))
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "El resultado necesita Breakfast, Lunch o Dinner.";
            }
            return false;
        }

        try
        {
            result = BuildServiceResultCore(
                financeSnapshot,
                productCostSnapshot,
                dayIndex,
                mealService);
            error = string.Empty;
            return true;
        }
        catch (OverflowException)
        {
            result = null;
            error = "El resultado del servicio desborda el rango monetario soportado.";
            return false;
        }
    }

    public static bool TryBuildDayResult(
        BistroBuilderFinanceSnapshot financeSnapshot,
        BistroBuilderProductCostSnapshot productCostSnapshot,
        int dayIndex,
        out BistroBuilderDayFinancialResult result,
        out string error)
    {
        return TryBuildDayResult(
            financeSnapshot,
            productCostSnapshot,
            null,
            dayIndex,
            out result,
            out error);
    }

    /// <summary>
    /// Construye el resultado diario. PurchaseOrders es una fuente opcional de
    /// desglose para separar portes del pago total a proveedor. Si un pago no
    /// puede enlazarse, se conserva íntegro en caja y se marca el desglose como
    /// incompleto; nunca se inventa un porte.
    /// </summary>
    public static bool TryBuildDayResult(
        BistroBuilderFinanceSnapshot financeSnapshot,
        BistroBuilderProductCostSnapshot productCostSnapshot,
        IReadOnlyList<BistroBuilderPurchaseOrderRecord> purchaseOrders,
        int dayIndex,
        out BistroBuilderDayFinancialResult result,
        out string error)
    {
        result = null;

        if (!TryValidateInputs(
                financeSnapshot,
                productCostSnapshot,
                dayIndex,
                out error))
        {
            return false;
        }

        try
        {
            var day = new BistroBuilderDayFinancialResult
            {
                dayIndex = dayIndex
            };

            BistroBuilderServiceFinancialResult breakfast =
                BuildServiceResultCore(
                    financeSnapshot,
                    productCostSnapshot,
                    dayIndex,
                    BistroBuilderMealServiceAvailability.Breakfast);
            BistroBuilderServiceFinancialResult lunch =
                BuildServiceResultCore(
                    financeSnapshot,
                    productCostSnapshot,
                    dayIndex,
                    BistroBuilderMealServiceAvailability.Lunch);
            BistroBuilderServiceFinancialResult dinner =
                BuildServiceResultCore(
                    financeSnapshot,
                    productCostSnapshot,
                    dayIndex,
                    BistroBuilderMealServiceAvailability.Dinner);

            day.serviceResults.Add(breakfast);
            day.serviceResults.Add(lunch);
            day.serviceResults.Add(dinner);

            AddServiceToDay(day, breakfast);
            AddServiceToDay(day, lunch);
            AddServiceToDay(day, dinner);

            day.costQuality = ResolveCostQuality(
                day.consumedLineCount,
                day.estimatedLineCount,
                day.mixedLineCount,
                day.actualLineCount);
            day.costCoverageGapCents = checked(
                day.revenueCents - day.costedSalesCents);
            day.grossProfitCents = checked(
                day.revenueCents - day.productCostCents);
            day.grossMarginBasisPoints = CalculateMarginBasisPoints(
                day.grossProfitCents,
                day.revenueCents);

            ClassifyDayCashAndExpenses(
                financeSnapshot,
                purchaseOrders,
                dayIndex,
                day);

            day.totalPeriodExpensesCents = checked(
                day.procurementShippingExpensesCents +
                day.recurringOperatingExpensesCents +
                day.payrollExpensesCents +
                day.marketingExpensesCents +
                day.assetDisposalExpensesCents +
                day.otherPeriodExpensesCents);
            day.operatingResultCents = checked(
                day.grossProfitCents - day.totalPeriodExpensesCents);
            day.netCashChangeCents = checked(
                day.totalCashInCents - day.totalCashOutCents);

            result = day;
            error = string.Empty;
            return true;
        }
        catch (OverflowException)
        {
            result = null;
            error = "El resultado diario desborda el rango monetario soportado.";
            return false;
        }
    }

    public static bool TryClassifySalesTransaction(
        BistroBuilderFinanceTransactionRecord record,
        out BistroBuilderMealServiceAvailability mealService,
        out BistroBuilderServiceMode serviceMode)
    {
        mealService = BistroBuilderMealServiceAvailability.None;
        serviceMode = default(BistroBuilderServiceMode);

        if (record == null ||
            record.kind != BistroBuilderFinanceTransactionKind.Credit ||
            !string.Equals(
                record.sourceSystemId,
                BistroBuilderSalesRevenuePolicy.SourceSystemId,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(record.categoryId))
        {
            return false;
        }

        string category = Normalize(record.categoryId);
        switch (category)
        {
            case "sales.breakfast.table":
                mealService = BistroBuilderMealServiceAvailability.Breakfast;
                serviceMode = BistroBuilderServiceMode.TableService;
                return true;
            case "sales.breakfast.bar":
                mealService = BistroBuilderMealServiceAvailability.Breakfast;
                serviceMode = BistroBuilderServiceMode.BarService;
                return true;
            case "sales.lunch.table":
                mealService = BistroBuilderMealServiceAvailability.Lunch;
                serviceMode = BistroBuilderServiceMode.TableService;
                return true;
            case "sales.lunch.bar":
                mealService = BistroBuilderMealServiceAvailability.Lunch;
                serviceMode = BistroBuilderServiceMode.BarService;
                return true;
            case "sales.dinner.table":
                mealService = BistroBuilderMealServiceAvailability.Dinner;
                serviceMode = BistroBuilderServiceMode.TableService;
                return true;
            case "sales.dinner.bar":
                mealService = BistroBuilderMealServiceAvailability.Dinner;
                serviceMode = BistroBuilderServiceMode.BarService;
                return true;
            default:
                return false;
        }
    }

    public static bool IsConcreteMealService(
        BistroBuilderMealServiceAvailability mealService)
    {
        return mealService == BistroBuilderMealServiceAvailability.Breakfast ||
               mealService == BistroBuilderMealServiceAvailability.Lunch ||
               mealService == BistroBuilderMealServiceAvailability.Dinner;
    }

    public static int CalculateMarginBasisPoints(
        long profitCents,
        long revenueCents)
    {
        if (revenueCents <= 0L)
        {
            return 0;
        }

        decimal value = decimal.Round(
            profitCents * 10000m / revenueCents,
            0,
            MidpointRounding.AwayFromZero);

        if (value > int.MaxValue)
        {
            return int.MaxValue;
        }
        if (value < int.MinValue)
        {
            return int.MinValue;
        }
        return (int)value;
    }

    private static bool TryValidateInputs(
        BistroBuilderFinanceSnapshot financeSnapshot,
        BistroBuilderProductCostSnapshot productCostSnapshot,
        int dayIndex,
        out string error)
    {
        if (dayIndex < 1)
        {
            error = "El DayIndex del resultado debe ser positivo.";
            return false;
        }

        if (!BistroBuilderFinanceEngine.TryValidateSnapshot(
                financeSnapshot,
                out error))
        {
            return false;
        }

        return BistroBuilderProductCostEngine.TryValidateSnapshot(
            productCostSnapshot,
            out error);
    }

    private static BistroBuilderServiceFinancialResult BuildServiceResultCore(
        BistroBuilderFinanceSnapshot financeSnapshot,
        BistroBuilderProductCostSnapshot productCostSnapshot,
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService)
    {
        var result = new BistroBuilderServiceFinancialResult
        {
            dayIndex = dayIndex,
            mealService = mealService
        };

        var paidOrderIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < financeSnapshot.transactions.Count; index++)
        {
            BistroBuilderFinanceTransactionRecord transaction =
                financeSnapshot.transactions[index];
            if (transaction == null || transaction.dayIndex != dayIndex ||
                !TryClassifySalesTransaction(
                    transaction,
                    out BistroBuilderMealServiceAvailability transactionMeal,
                    out BistroBuilderServiceMode serviceMode) ||
                transactionMeal != mealService)
            {
                continue;
            }

            result.revenueCents = checked(
                result.revenueCents + transaction.amountCents);
            if (serviceMode == BistroBuilderServiceMode.TableService)
            {
                result.tableRevenueCents = checked(
                    result.tableRevenueCents + transaction.amountCents);
            }
            else
            {
                result.barRevenueCents = checked(
                    result.barRevenueCents + transaction.amountCents);
            }

            if (!string.IsNullOrWhiteSpace(transaction.sourceReferenceId))
            {
                paidOrderIds.Add(transaction.sourceReferenceId);
            }
        }

        result.paidOrderCount = paidOrderIds.Count;

        for (int index = 0;
             index < productCostSnapshot.consumedLineCosts.Count;
             index++)
        {
            BistroBuilderConsumedLineCostRecord line =
                productCostSnapshot.consumedLineCosts[index];
            if (line == null ||
                line.dayIndex != dayIndex ||
                line.mealService != mealService)
            {
                continue;
            }

            result.costedSalesCents = checked(
                result.costedSalesCents + line.salePriceCents);
            result.productCostCents = checked(
                result.productCostCents + line.actualCostCents);
            result.theoreticalProductCostCents = checked(
                result.theoreticalProductCostCents +
                line.theoreticalCostCents);
            result.consumedLineCount++;

            switch (line.costQuality)
            {
                case BistroBuilderProductCostQuality.Actual:
                    result.actualLineCount++;
                    break;
                case BistroBuilderProductCostQuality.Mixed:
                    result.mixedLineCount++;
                    break;
                default:
                    result.estimatedLineCount++;
                    break;
            }
        }

        result.costQuality = ResolveCostQuality(
            result.consumedLineCount,
            result.estimatedLineCount,
            result.mixedLineCount,
            result.actualLineCount);
        result.costCoverageGapCents = checked(
            result.revenueCents - result.costedSalesCents);
        result.grossProfitCents = checked(
            result.revenueCents - result.productCostCents);
        result.grossMarginBasisPoints = CalculateMarginBasisPoints(
            result.grossProfitCents,
            result.revenueCents);

        return result;
    }

    private static void AddServiceToDay(
        BistroBuilderDayFinancialResult day,
        BistroBuilderServiceFinancialResult service)
    {
        day.revenueCents = checked(day.revenueCents + service.revenueCents);
        day.costedSalesCents = checked(
            day.costedSalesCents + service.costedSalesCents);
        day.productCostCents = checked(
            day.productCostCents + service.productCostCents);
        day.theoreticalProductCostCents = checked(
            day.theoreticalProductCostCents +
            service.theoreticalProductCostCents);
        day.paidOrderCount = checked(
            day.paidOrderCount + service.paidOrderCount);
        day.consumedLineCount = checked(
            day.consumedLineCount + service.consumedLineCount);
        day.estimatedLineCount = checked(
            day.estimatedLineCount + service.estimatedLineCount);
        day.mixedLineCount = checked(
            day.mixedLineCount + service.mixedLineCount);
        day.actualLineCount = checked(
            day.actualLineCount + service.actualLineCount);
    }

    private static void ClassifyDayCashAndExpenses(
        BistroBuilderFinanceSnapshot financeSnapshot,
        IReadOnlyList<BistroBuilderPurchaseOrderRecord> purchaseOrders,
        int dayIndex,
        BistroBuilderDayFinancialResult day)
    {
        for (int index = 0; index < financeSnapshot.transactions.Count; index++)
        {
            BistroBuilderFinanceTransactionRecord transaction =
                financeSnapshot.transactions[index];
            if (transaction == null || transaction.dayIndex != dayIndex)
            {
                continue;
            }

            if (transaction.kind == BistroBuilderFinanceTransactionKind.Credit)
            {
                day.totalCashInCents = checked(
                    day.totalCashInCents + transaction.amountCents);

                if (TryClassifySalesTransaction(transaction, out _, out _))
                {
                    continue;
                }

                if (IsCategory(transaction, "income.asset_resale"))
                {
                    day.assetResaleCashInCents = checked(
                        day.assetResaleCashInCents + transaction.amountCents);
                }
                else
                {
                    day.otherCashInCents = checked(
                        day.otherCashInCents + transaction.amountCents);
                }
                continue;
            }

            day.totalCashOutCents = checked(
                day.totalCashOutCents + transaction.amountCents);

            string category = Normalize(transaction.categoryId);
            if (category == BistroBuilderSupplierPurchaseFinancePolicy.CategoryId)
            {
                day.supplierPurchaseCashOutCents = checked(
                    day.supplierPurchaseCashOutCents + transaction.amountCents);

                if (TryResolveSupplierShipping(
                        transaction,
                        purchaseOrders,
                        out long shippingCents))
                {
                    day.procurementShippingExpensesCents = checked(
                        day.procurementShippingExpensesCents + shippingCents);
                }
                else
                {
                    day.supplierPaymentBreakdownMissingCount = checked(
                        day.supplierPaymentBreakdownMissingCount + 1);
                }
            }
            else if (category.StartsWith(InvestmentPrefix, StringComparison.Ordinal))
            {
                day.investmentCashOutCents = checked(
                    day.investmentCashOutCents + transaction.amountCents);
            }
            else if (category.StartsWith(
                         OperatingExpensePrefix,
                         StringComparison.Ordinal))
            {
                day.recurringOperatingExpensesCents = checked(
                    day.recurringOperatingExpensesCents + transaction.amountCents);
            }
            else if (category == BistroBuilderOperatingExpensePolicy.PayrollCategoryId)
            {
                day.payrollExpensesCents = checked(
                    day.payrollExpensesCents + transaction.amountCents);
            }
            else if (category == MarketingExpensePrefix ||
                     category.StartsWith(
                         MarketingExpensePrefix + ".",
                         StringComparison.Ordinal))
            {
                day.marketingExpensesCents = checked(
                    day.marketingExpensesCents + transaction.amountCents);
            }
            else if (category == "expense.demolition" ||
                     category == "expense.asset_removal")
            {
                day.assetDisposalExpensesCents = checked(
                    day.assetDisposalExpensesCents + transaction.amountCents);
            }
            else if (category.StartsWith(ExpensePrefix, StringComparison.Ordinal))
            {
                day.otherPeriodExpensesCents = checked(
                    day.otherPeriodExpensesCents + transaction.amountCents);
            }
            else
            {
                day.otherCashOutCents = checked(
                    day.otherCashOutCents + transaction.amountCents);
            }
        }
    }

    private static bool TryResolveSupplierShipping(
        BistroBuilderFinanceTransactionRecord transaction,
        IReadOnlyList<BistroBuilderPurchaseOrderRecord> purchaseOrders,
        out long shippingCents)
    {
        shippingCents = 0L;
        if (transaction == null ||
            purchaseOrders == null ||
            string.IsNullOrWhiteSpace(transaction.sourceReferenceId))
        {
            return false;
        }

        string purchaseOrderId = Normalize(transaction.sourceReferenceId);
        for (int index = 0; index < purchaseOrders.Count; index++)
        {
            BistroBuilderPurchaseOrderRecord order = purchaseOrders[index];
            if (order == null ||
                !string.Equals(
                    Normalize(order.purchaseOrderId),
                    purchaseOrderId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (order.subtotalCents < 0L ||
                order.shippingCostCents < 0L ||
                order.totalCents <= 0L ||
                order.totalCents != transaction.amountCents)
            {
                return false;
            }

            long calculatedTotal;
            try
            {
                calculatedTotal = checked(
                    order.subtotalCents + order.shippingCostCents);
            }
            catch (OverflowException)
            {
                return false;
            }

            if (calculatedTotal != order.totalCents)
            {
                return false;
            }

            shippingCents = order.shippingCostCents;
            return true;
        }

        return false;
    }

    private static BistroBuilderFinancialResultCostQuality ResolveCostQuality(
        int totalLines,
        int estimatedLines,
        int mixedLines,
        int actualLines)
    {
        if (totalLines <= 0)
        {
            return BistroBuilderFinancialResultCostQuality.None;
        }

        if (actualLines == totalLines)
        {
            return BistroBuilderFinancialResultCostQuality.Actual;
        }

        if (estimatedLines == totalLines)
        {
            return BistroBuilderFinancialResultCostQuality.Estimated;
        }

        return BistroBuilderFinancialResultCostQuality.Mixed;
    }

    private static bool IsCategory(
        BistroBuilderFinanceTransactionRecord transaction,
        string categoryId)
    {
        return transaction != null &&
               string.Equals(
                   Normalize(transaction.categoryId),
                   categoryId,
                   StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
