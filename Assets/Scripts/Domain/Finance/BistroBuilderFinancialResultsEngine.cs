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
    /// Construye un resultado diario a partir de los snapshots canónicos.
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
        var buffer = new List<BistroBuilderDayFinancialResult>(1);
        if (!TryBuildDayResultsRange(
                financeSnapshot,
                productCostSnapshot,
                purchaseOrders,
                dayIndex,
                dayIndex,
                buffer,
                out error))
        {
            return false;
        }

        result = buffer[0];
        return true;
    }

    /// <summary>
    /// Construye un intervalo completo en una única pasada por ledger y costes.
    /// Evita el patrón histórico O(días * movimientos) de 3H y permite que 3J
    /// consulte ventanas largas sin clonar/recorrer todo el estado por día.
    /// </summary>
    public static bool TryBuildDayResultsRange(
        BistroBuilderFinanceSnapshot financeSnapshot,
        BistroBuilderProductCostSnapshot productCostSnapshot,
        IReadOnlyList<BistroBuilderPurchaseOrderRecord> purchaseOrders,
        int startDayIndex,
        int endDayIndex,
        List<BistroBuilderDayFinancialResult> destination,
        out string error)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }
        destination.Clear();

        if (startDayIndex < 1 || endDayIndex < startDayIndex)
        {
            error = "El intervalo de resultados diarios no es válido.";
            return false;
        }

        BistroBuilderProductCostService.NormalizeCompatibleSnapshot(
            productCostSnapshot);
        if (!BistroBuilderFinanceEngine.TryValidateSnapshot(
                financeSnapshot,
                out error) ||
            !BistroBuilderProductCostEngine.TryValidateSnapshot(
                productCostSnapshot,
                out error))
        {
            return false;
        }

        long countLong = (long)endDayIndex - startDayIndex + 1L;
        if (countLong > int.MaxValue)
        {
            error = "El intervalo de resultados es demasiado grande.";
            return false;
        }

        try
        {
            int count = (int)countLong;
            var paidOrderIds = new HashSet<string>[count * 3];

            for (int dayOffset = 0; dayOffset < count; dayOffset++)
            {
                int dayIndex = checked(startDayIndex + dayOffset);
                var day = new BistroBuilderDayFinancialResult
                {
                    dayIndex = dayIndex
                };
                day.serviceResults.Add(NewServiceResult(
                    dayIndex,
                    BistroBuilderMealServiceAvailability.Breakfast));
                day.serviceResults.Add(NewServiceResult(
                    dayIndex,
                    BistroBuilderMealServiceAvailability.Lunch));
                day.serviceResults.Add(NewServiceResult(
                    dayIndex,
                    BistroBuilderMealServiceAvailability.Dinner));
                destination.Add(day);
            }

            for (int index = 0; index < financeSnapshot.transactions.Count; index++)
            {
                BistroBuilderFinanceTransactionRecord transaction =
                    financeSnapshot.transactions[index];
                if (transaction == null ||
                    transaction.dayIndex < startDayIndex ||
                    transaction.dayIndex > endDayIndex)
                {
                    continue;
                }

                int dayOffset = transaction.dayIndex - startDayIndex;
                BistroBuilderDayFinancialResult day = destination[dayOffset];

                if (TryClassifySalesTransaction(
                        transaction,
                        out BistroBuilderMealServiceAvailability mealService,
                        out BistroBuilderServiceMode serviceMode))
                {
                    int serviceIndex = ResolveServiceIndex(mealService);
                    BistroBuilderServiceFinancialResult service =
                        day.serviceResults[serviceIndex];
                    service.revenueCents = checked(
                        service.revenueCents + transaction.amountCents);
                    if (serviceMode == BistroBuilderServiceMode.TableService)
                    {
                        service.tableRevenueCents = checked(
                            service.tableRevenueCents + transaction.amountCents);
                    }
                    else
                    {
                        service.barRevenueCents = checked(
                            service.barRevenueCents + transaction.amountCents);
                    }

                    if (!string.IsNullOrWhiteSpace(transaction.sourceReferenceId))
                    {
                        int orderSetIndex = dayOffset * 3 + serviceIndex;
                        if (paidOrderIds[orderSetIndex] == null)
                        {
                            paidOrderIds[orderSetIndex] =
                                new HashSet<string>(StringComparer.Ordinal);
                        }
                        paidOrderIds[orderSetIndex].Add(
                            transaction.sourceReferenceId);
                    }
                }

                ClassifyTransactionCashAndExpenses(
                    transaction,
                    purchaseOrders,
                    day);
            }

            for (int index = 0;
                 index < productCostSnapshot.consumedLineCosts.Count;
                 index++)
            {
                BistroBuilderConsumedLineCostRecord line =
                    productCostSnapshot.consumedLineCosts[index];
                if (line == null ||
                    line.dayIndex < startDayIndex ||
                    line.dayIndex > endDayIndex ||
                    !IsConcreteMealService(line.mealService))
                {
                    continue;
                }

                int dayOffset = line.dayIndex - startDayIndex;
                int serviceIndex = ResolveServiceIndex(line.mealService);
                BistroBuilderServiceFinancialResult service =
                    destination[dayOffset].serviceResults[serviceIndex];

                service.costedSalesCents = checked(
                    service.costedSalesCents + line.salePriceCents);
                service.productCostCents = checked(
                    service.productCostCents + line.actualCostCents);
                service.theoreticalProductCostCents = checked(
                    service.theoreticalProductCostCents +
                    line.theoreticalCostCents);
                service.consumedLineCount++;

                switch (line.costQuality)
                {
                    case BistroBuilderProductCostQuality.Actual:
                        service.actualLineCount++;
                        break;
                    case BistroBuilderProductCostQuality.Mixed:
                        service.mixedLineCount++;
                        break;
                    default:
                        service.estimatedLineCount++;
                        break;
                }
            }

            // Caducidad/merma son pérdidas de resultado NO monetarias. La
            // compra ya afectó caja; aquí solo se reconoce el valor consumido
            // sin venta y nunca se incrementa totalCashOutCents.
            for (int index = 0;
                 index < productCostSnapshot.inventoryLossCosts.Count;
                 index++)
            {
                BistroBuilderInventoryLossCostRecord loss =
                    productCostSnapshot.inventoryLossCosts[index];
                if (loss == null ||
                    loss.dayIndex < startDayIndex ||
                    loss.dayIndex > endDayIndex)
                {
                    continue;
                }

                int dayOffset = loss.dayIndex - startDayIndex;
                BistroBuilderDayFinancialResult day = destination[dayOffset];
                day.inventoryWriteOffExpensesCents = checked(
                    day.inventoryWriteOffExpensesCents + loss.costCents);
            }

            for (int dayOffset = 0; dayOffset < count; dayOffset++)
            {
                BistroBuilderDayFinancialResult day = destination[dayOffset];
                for (int serviceIndex = 0; serviceIndex < 3; serviceIndex++)
                {
                    BistroBuilderServiceFinancialResult service =
                        day.serviceResults[serviceIndex];
                    HashSet<string> orders =
                        paidOrderIds[dayOffset * 3 + serviceIndex];
                    service.paidOrderCount = orders != null ? orders.Count : 0;
                    FinalizeService(service);
                    AddServiceToDay(day, service);
                }

                FinalizeDay(day);
            }

            error = string.Empty;
            return true;
        }
        catch (OverflowException)
        {
            destination.Clear();
            error = "El intervalo de resultados financieros desborda el rango monetario soportado.";
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

        switch (Normalize(record.categoryId))
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

        BistroBuilderProductCostService.NormalizeCompatibleSnapshot(
            productCostSnapshot);
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
        var result = NewServiceResult(dayIndex, mealService);
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

        FinalizeService(result);
        return result;
    }

    private static BistroBuilderServiceFinancialResult NewServiceResult(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService)
    {
        return new BistroBuilderServiceFinancialResult
        {
            dayIndex = dayIndex,
            mealService = mealService
        };
    }

    private static int ResolveServiceIndex(
        BistroBuilderMealServiceAvailability mealService)
    {
        switch (mealService)
        {
            case BistroBuilderMealServiceAvailability.Breakfast:
                return 0;
            case BistroBuilderMealServiceAvailability.Lunch:
                return 1;
            case BistroBuilderMealServiceAvailability.Dinner:
                return 2;
            default:
                throw new ArgumentOutOfRangeException(nameof(mealService));
        }
    }

    private static void FinalizeService(
        BistroBuilderServiceFinancialResult result)
    {
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

    private static void FinalizeDay(BistroBuilderDayFinancialResult day)
    {
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
        day.totalPeriodExpensesCents = checked(
            day.procurementShippingExpensesCents +
            day.recurringOperatingExpensesCents +
            day.payrollExpensesCents +
            day.marketingExpensesCents +
            day.assetDisposalExpensesCents +
            day.inventoryWriteOffExpensesCents +
            day.financingInterestExpensesCents +
            day.otherPeriodExpensesCents);
        day.operatingResultCents = checked(
            day.grossProfitCents - day.totalPeriodExpensesCents);
        day.netCashChangeCents = checked(
            day.totalCashInCents - day.totalCashOutCents);
    }

    private static void ClassifyTransactionCashAndExpenses(
        BistroBuilderFinanceTransactionRecord transaction,
        IReadOnlyList<BistroBuilderPurchaseOrderRecord> purchaseOrders,
        BistroBuilderDayFinancialResult day)
    {
        if (transaction.kind == BistroBuilderFinanceTransactionKind.Credit)
        {
            day.totalCashInCents = checked(
                day.totalCashInCents + transaction.amountCents);

            if (TryClassifySalesTransaction(transaction, out _, out _))
            {
                return;
            }

            string category = Normalize(transaction.categoryId);
            if (category == BistroBuilderFinancingEngine.LoanProceedsCategoryId)
            {
                day.loanProceedsCashInCents = checked(
                    day.loanProceedsCashInCents + transaction.amountCents);
            }
            else if (category == "income.asset_resale")
            {
                day.assetResaleCashInCents = checked(
                    day.assetResaleCashInCents + transaction.amountCents);
            }
            else
            {
                day.otherCashInCents = checked(
                    day.otherCashInCents + transaction.amountCents);
            }
            return;
        }

        day.totalCashOutCents = checked(
            day.totalCashOutCents + transaction.amountCents);

        string debitCategory = Normalize(transaction.categoryId);
        if (debitCategory == BistroBuilderSupplierPurchaseFinancePolicy.CategoryId)
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
        else if (debitCategory ==
                 BistroBuilderFinancingEngine.PrincipalRepaymentCategoryId)
        {
            day.debtPrincipalCashOutCents = checked(
                day.debtPrincipalCashOutCents + transaction.amountCents);
        }
        else if (debitCategory ==
                 BistroBuilderFinancingEngine.InterestExpenseCategoryId)
        {
            day.financingInterestExpensesCents = checked(
                day.financingInterestExpensesCents + transaction.amountCents);
        }
        else if (debitCategory.StartsWith(
                     InvestmentPrefix,
                     StringComparison.Ordinal))
        {
            day.investmentCashOutCents = checked(
                day.investmentCashOutCents + transaction.amountCents);
        }
        else if (debitCategory.StartsWith(
                     OperatingExpensePrefix,
                     StringComparison.Ordinal))
        {
            day.recurringOperatingExpensesCents = checked(
                day.recurringOperatingExpensesCents + transaction.amountCents);
        }
        else if (debitCategory == BistroBuilderOperatingExpensePolicy.PayrollCategoryId)
        {
            day.payrollExpensesCents = checked(
                day.payrollExpensesCents + transaction.amountCents);
        }
        else if (debitCategory == MarketingExpensePrefix ||
                 debitCategory.StartsWith(
                     MarketingExpensePrefix + ".",
                     StringComparison.Ordinal))
        {
            day.marketingExpensesCents = checked(
                day.marketingExpensesCents + transaction.amountCents);
        }
        else if (debitCategory == "expense.demolition" ||
                 debitCategory == "expense.asset_removal")
        {
            day.assetDisposalExpensesCents = checked(
                day.assetDisposalExpensesCents + transaction.amountCents);
        }
        else if (debitCategory.StartsWith(ExpensePrefix, StringComparison.Ordinal))
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

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
