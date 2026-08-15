using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderFinance3GSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Finanzas/3G - Autotest",
        false,
        3062)]
    private static void RunFromMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3G",
            "Autotest: " + passed + " correctos, " + failed + " fallos.",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("3G — El autotest ha fallado.");
        }
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        int capturedErrors = 0;
        var builder = new StringBuilder();

        Application.LogCallback handler = (condition, stackTrace, type) =>
        {
            if (type == LogType.Error ||
                type == LogType.Exception ||
                type == LogType.Assert)
            {
                capturedErrors++;
            }
        };

        Application.logMessageReceived += handler;
        try
        {
            RunProjectionTests(ref passed, ref failed, builder);
            RunGuardTests(ref passed, ref failed, builder);
        }
        catch (Exception exception)
        {
            failed++;
            builder.AppendLine(
                "[ERROR] Excepción inesperada: " + exception.Message);
        }
        finally
        {
            Application.logMessageReceived -= handler;
        }

        Check(
            capturedErrors == 0,
            "Console sin Error/Exception/Assert durante autotest.",
            ref passed,
            ref failed,
            builder);

        builder.Insert(
            0,
            "3G — AUTOTEST RESULTADOS POR SERVICIO Y DÍA\n" +
            "Correctos: " + passed +
            "  Fallos: " + failed +
            "  Error/Exception/Assert: " + capturedErrors + "\n\n");
        report = builder.ToString();
        return failed == 0;
    }

    private static void RunProjectionTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        BuildFixture(
            out BistroBuilderFinanceSnapshot finance,
            out BistroBuilderProductCostSnapshot productCost,
            out List<BistroBuilderPurchaseOrderRecord> purchaseOrders);

        Check(
            BistroBuilderFinancialResultsEngine.TryBuildServiceResult(
                finance,
                productCost,
                5,
                BistroBuilderMealServiceAvailability.Lunch,
                out BistroBuilderServiceFinancialResult lunch,
                out _),
            "Resultado de comida se proyecta desde snapshots canónicos.",
            ref passed, ref failed, builder);
        Check(
            lunch.revenueCents == 15000L,
            "Comida suma 150,00 € de ingresos cobrados.",
            ref passed, ref failed, builder);
        Check(
            lunch.tableRevenueCents == 10000L &&
            lunch.barRevenueCents == 5000L,
            "Comida separa mesa y barra sin duplicar ingresos.",
            ref passed, ref failed, builder);
        Check(
            lunch.paidOrderCount == 2,
            "Comida conserva dos cobros canónicos únicos.",
            ref passed, ref failed, builder);
        Check(
            lunch.productCostCents == 4000L &&
            lunch.theoreticalProductCostCents == 3500L,
            "Comida separa COGS reconocido y coste teórico.",
            ref passed, ref failed, builder);
        Check(
            lunch.costedSalesCents == 15000L &&
            lunch.costCoverageGapCents == 0L &&
            lunch.IsCostCoverageComplete,
            "Comida detecta cobertura completa entre cobros y líneas valoradas.",
            ref passed, ref failed, builder);
        Check(
            lunch.consumedLineCount == 2 &&
            lunch.actualLineCount == 1 &&
            lunch.mixedLineCount == 1 &&
            lunch.estimatedLineCount == 0,
            "Comida conserva la composición de calidad de coste.",
            ref passed, ref failed, builder);
        Check(
            lunch.costQuality == BistroBuilderFinancialResultCostQuality.Mixed,
            "Calidad agregada de comida es Mixed.",
            ref passed, ref failed, builder);
        Check(
            lunch.grossProfitCents == 11000L &&
            lunch.grossMarginBasisPoints == 7333,
            "Comida calcula 110,00 € y 73,33 % de margen bruto.",
            ref passed, ref failed, builder);

        Check(
            BistroBuilderFinancialResultsEngine.TryBuildServiceResult(
                finance,
                productCost,
                5,
                BistroBuilderMealServiceAvailability.Breakfast,
                out BistroBuilderServiceFinancialResult breakfast,
                out _),
            "Resultado de desayuno se proyecta correctamente.",
            ref passed, ref failed, builder);
        Check(
            breakfast.revenueCents == 2000L &&
            breakfast.productCostCents == 600L &&
            breakfast.grossProfitCents == 1400L &&
            breakfast.grossMarginBasisPoints == 7000,
            "Desayuno calcula ingreso, COGS y margen esperados.",
            ref passed, ref failed, builder);
        Check(
            breakfast.costQuality ==
                BistroBuilderFinancialResultCostQuality.Estimated,
            "Desayuno expone coste Estimated sin presentarlo como Actual.",
            ref passed, ref failed, builder);

        Check(
            BistroBuilderFinancialResultsEngine.TryBuildServiceResult(
                finance,
                productCost,
                5,
                BistroBuilderMealServiceAvailability.Dinner,
                out BistroBuilderServiceFinancialResult dinner,
                out _),
            "Cena vacía sigue produciendo un resultado válido.",
            ref passed, ref failed, builder);
        Check(
            !dinner.HasActivity &&
            dinner.costQuality == BistroBuilderFinancialResultCostQuality.None,
            "Cena sin actividad no inventa ventas, COGS ni calidad.",
            ref passed, ref failed, builder);

        Check(
            BistroBuilderFinancialResultsEngine.TryBuildDayResult(
                finance,
                productCost,
                purchaseOrders,
                5,
                out BistroBuilderDayFinancialResult day,
                out _),
            "Resultado diario se proyecta con desglose de proveedores.",
            ref passed, ref failed, builder);
        Check(
            day.serviceResults.Count == 3,
            "Resultado diario contiene Breakfast, Lunch y Dinner.",
            ref passed, ref failed, builder);
        Check(
            day.revenueCents == 17000L &&
            day.productCostCents == 4600L &&
            day.theoreticalProductCostCents == 4000L,
            "Día suma ventas y COGS de todos los servicios.",
            ref passed, ref failed, builder);
        Check(
            day.paidOrderCount == 3 &&
            day.consumedLineCount == 3,
            "Día suma cobros y líneas consumidas sin mezclar otro día.",
            ref passed, ref failed, builder);
        Check(
            day.costedSalesCents == 17000L &&
            day.costCoverageGapCents == 0L &&
            day.IsCostCoverageComplete,
            "Día conserva cobertura completa de costes.",
            ref passed, ref failed, builder);
        Check(
            day.costQuality == BistroBuilderFinancialResultCostQuality.Mixed,
            "Calidad diaria refleja la mezcla de costes del día.",
            ref passed, ref failed, builder);
        Check(
            day.grossProfitCents == 12400L &&
            day.grossMarginBasisPoints == 7294,
            "Día calcula margen bruto antes de gastos de periodo.",
            ref passed, ref failed, builder);

        Check(
            day.procurementShippingExpensesCents == 500L &&
            day.supplierPaymentBreakdownMissingCount == 0 &&
            day.HasCompleteSupplierPaymentBreakdown,
            "Portes de proveedor se reconocen como gasto sin entrar en COGS.",
            ref passed, ref failed, builder);
        Check(
            day.recurringOperatingExpensesCents == 1000L &&
            day.payrollExpensesCents == 2000L &&
            day.marketingExpensesCents == 500L,
            "Día separa operativos, nómina y Marketing.",
            ref passed, ref failed, builder);
        Check(
            day.assetDisposalExpensesCents == 300L &&
            day.otherPeriodExpensesCents == 200L,
            "Día separa demolición/retirada y otros gastos de periodo.",
            ref passed, ref failed, builder);
        Check(
            day.totalPeriodExpensesCents == 4500L &&
            day.operatingResultCents == 7900L,
            "Resultado diario resta COGS una vez, portes y gastos de periodo.",
            ref passed, ref failed, builder);

        Check(
            day.supplierPurchaseCashOutCents == 7000L,
            "Compra a proveedor conserva 70,00 € de salida de caja completa.",
            ref passed, ref failed, builder);
        Check(
            day.investmentCashOutCents == 4000L,
            "Inversión se muestra como caja y no como gasto de periodo.",
            ref passed, ref failed, builder);
        Check(
            day.assetResaleCashInCents == 1000L,
            "Reventa de activo se muestra separada del resultado operativo.",
            ref passed, ref failed, builder);
        Check(
            day.otherCashInCents == 300L &&
            day.otherCashOutCents == 100L,
            "Flujos no clasificados permanecen visibles sin contaminar resultado.",
            ref passed, ref failed, builder);
        Check(
            day.totalCashInCents == 18300L &&
            day.totalCashOutCents == 15100L &&
            day.netCashChangeCents == 3200L,
            "Flujo de caja diario cuadra independientemente del beneficio.",
            ref passed, ref failed, builder);
        Check(
            day.operatingResultCents != day.netCashChangeCents,
            "Beneficio y variación de caja no se confunden.",
            ref passed, ref failed, builder);

        Check(
            BistroBuilderFinancialResultsEngine.TryBuildDayResult(
                finance,
                productCost,
                null,
                5,
                out BistroBuilderDayFinancialResult unresolvedDay,
                out _),
            "Resultado diario acepta que el desglose de PurchaseOrders no esté disponible.",
            ref passed, ref failed, builder);
        Check(
            unresolvedDay.procurementShippingExpensesCents == 0L &&
            unresolvedDay.supplierPaymentBreakdownMissingCount == 1 &&
            !unresolvedDay.HasCompleteSupplierPaymentBreakdown,
            "Pago de proveedor sin PO se marca incompleto y no inventa portes.",
            ref passed, ref failed, builder);
        Check(
            unresolvedDay.totalPeriodExpensesCents == 4000L &&
            unresolvedDay.operatingResultCents == 8400L,
            "Sin desglose, el resultado no fabrica un gasto de aprovisionamiento.",
            ref passed, ref failed, builder);
    }

    private static void RunGuardTests(
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        BuildFixture(
            out BistroBuilderFinanceSnapshot finance,
            out BistroBuilderProductCostSnapshot productCost,
            out _);

        Check(
            BistroBuilderFinancialResultsEngine.IsConcreteMealService(
                BistroBuilderMealServiceAvailability.Breakfast) &&
            BistroBuilderFinancialResultsEngine.IsConcreteMealService(
                BistroBuilderMealServiceAvailability.Lunch) &&
            BistroBuilderFinancialResultsEngine.IsConcreteMealService(
                BistroBuilderMealServiceAvailability.Dinner),
            "Solo los tres servicios concretos son válidos.",
            ref passed, ref failed, builder);
        Check(
            !BistroBuilderFinancialResultsEngine.IsConcreteMealService(
                BistroBuilderMealServiceAvailability.All) &&
            !BistroBuilderFinancialResultsEngine.IsConcreteMealService(
                BistroBuilderMealServiceAvailability.None),
            "Máscaras All/None no pueden identificar un resultado de servicio.",
            ref passed, ref failed, builder);

        BistroBuilderFinanceTransactionRecord sale = finance.transactions[0];
        Check(
            BistroBuilderFinancialResultsEngine.TryClassifySalesTransaction(
                sale,
                out BistroBuilderMealServiceAvailability meal,
                out BistroBuilderServiceMode mode) &&
            meal == BistroBuilderMealServiceAvailability.Lunch &&
            mode == BistroBuilderServiceMode.TableService,
            "Clasificador reconoce un cobro canónico de mesa/comida.",
            ref passed, ref failed, builder);

        BistroBuilderFinanceTransactionRecord fakeSale = sale.DeepClone();
        fakeSale.kind = BistroBuilderFinanceTransactionKind.Debit;
        Check(
            !BistroBuilderFinancialResultsEngine.TryClassifySalesTransaction(
                fakeSale,
                out _,
                out _),
            "Una salida con categoría sales.* nunca se interpreta como venta.",
            ref passed, ref failed, builder);

        fakeSale = sale.DeepClone();
        fakeSale.sourceSystemId = "foreign.system";
        Check(
            !BistroBuilderFinancialResultsEngine.TryClassifySalesTransaction(
                fakeSale,
                out _,
                out _),
            "Una categoría sales.* de fuente ajena no se considera cobro canónico.",
            ref passed, ref failed, builder);

        Check(
            !BistroBuilderFinancialResultsEngine.TryBuildServiceResult(
                finance,
                productCost,
                0,
                BistroBuilderMealServiceAvailability.Lunch,
                out _,
                out _),
            "DayIndex cero se rechaza.",
            ref passed, ref failed, builder);
        Check(
            !BistroBuilderFinancialResultsEngine.TryBuildServiceResult(
                finance,
                productCost,
                5,
                BistroBuilderMealServiceAvailability.All,
                out _,
                out _),
            "Un servicio no concreto se rechaza.",
            ref passed, ref failed, builder);
        Check(
            !BistroBuilderFinancialResultsEngine.TryBuildDayResult(
                null,
                productCost,
                5,
                out _,
                out _),
            "Snapshot financiero nulo se rechaza.",
            ref passed, ref failed, builder);

        BistroBuilderFinanceSnapshot incompleteFinance = finance.DeepClone();
        AddSaleToSnapshot(
            incompleteFinance,
            "order_dinner_gap",
            BistroBuilderServiceMode.TableService,
            BistroBuilderMealServiceAvailability.Dinner,
            900L,
            5);
        Check(
            BistroBuilderFinancialResultsEngine.TryBuildServiceResult(
                incompleteFinance,
                productCost,
                5,
                BistroBuilderMealServiceAvailability.Dinner,
                out BistroBuilderServiceFinancialResult gapResult,
                out _) &&
            gapResult.costCoverageGapCents == 900L &&
            !gapResult.IsCostCoverageComplete,
            "3G detecta cobros todavía sin COGS en lugar de ocultarlos.",
            ref passed, ref failed, builder);

        Check(
            BistroBuilderFinancialResultsEngine.CalculateMarginBasisPoints(
                -500L,
                1000L) == -5000,
            "Margen negativo conserva signo y precisión en basis points.",
            ref passed, ref failed, builder);
        Check(
            BistroBuilderFinancialResultsEngine.CalculateMarginBasisPoints(
                500L,
                0L) == 0,
            "Sin ingresos no se inventa porcentaje de margen.",
            ref passed, ref failed, builder);
    }

    private static void BuildFixture(
        out BistroBuilderFinanceSnapshot finance,
        out BistroBuilderProductCostSnapshot productCost,
        out List<BistroBuilderPurchaseOrderRecord> purchaseOrders)
    {
        GameObject go = new GameObject("BB_3G_FinanceFixture");
        try
        {
            BistroBuilderFinanceService service =
                go.AddComponent<BistroBuilderFinanceService>();
            if (!service.TryInitializeFresh(out string initializationError))
            {
                throw new InvalidOperationException(
                    "Finance temporal 3G no pudo inicializarse: " +
                    initializationError);
            }

            PostSale(service, "order_lunch_table",
                BistroBuilderServiceMode.TableService,
                BistroBuilderMealServiceAvailability.Lunch, 10000L, 5);
            PostSale(service, "order_lunch_bar",
                BistroBuilderServiceMode.BarService,
                BistroBuilderMealServiceAvailability.Lunch, 5000L, 5);
            PostSale(service, "order_breakfast_table",
                BistroBuilderServiceMode.TableService,
                BistroBuilderMealServiceAvailability.Breakfast, 2000L, 5);
            PostSale(service, "order_other_day",
                BistroBuilderServiceMode.TableService,
                BistroBuilderMealServiceAvailability.Lunch, 9000L, 6);

            PostFinance(service, Request(
                "supplier_day5",
                BistroBuilderSupplierPurchaseFinancePolicy.SourceSystemId,
                "po_test_3g",
                BistroBuilderSupplierPurchaseFinancePolicy.CategoryId,
                BistroBuilderFinanceTransactionKind.Debit, 7000L, 5));
            PostFinance(service, Request(
                "operating_day5",
                BistroBuilderOperatingExpensePolicy.OperatingSourceSystemId,
                "utilities_test_3g",
                "expense.operating.utilities",
                BistroBuilderFinanceTransactionKind.Debit, 1000L, 5));
            PostFinance(service, Request(
                "payroll_day5",
                BistroBuilderOperatingExpensePolicy.PayrollSourceSystemId,
                "payroll_test_3g",
                BistroBuilderOperatingExpensePolicy.PayrollCategoryId,
                BistroBuilderFinanceTransactionKind.Debit, 2000L, 5));
            PostFinance(service, Request(
                "marketing_day5", "marketing", "campaign_test_3g",
                "expense.marketing.local",
                BistroBuilderFinanceTransactionKind.Debit, 500L, 5));
            PostFinance(service, Request(
                "investment_day5", "placeable.finance", "asset_test_3g",
                "investment.furniture",
                BistroBuilderFinanceTransactionKind.Debit, 4000L, 5));
            PostFinance(service, Request(
                "resale_day5", "placeable.finance", "asset_test_3g",
                "income.asset_resale",
                BistroBuilderFinanceTransactionKind.Credit, 1000L, 5));
            PostFinance(service, Request(
                "demolition_day5", "placeable.finance", "wall_test_3g",
                "expense.demolition",
                BistroBuilderFinanceTransactionKind.Debit, 300L, 5));
            PostFinance(service, Request(
                "period_other_day5", "future.expense", "future_expense_test_3g",
                "expense.other",
                BistroBuilderFinanceTransactionKind.Debit, 200L, 5));
            PostFinance(service, Request(
                "cash_in_other_day5", "future.finance", "future_credit_test_3g",
                "financing.credit",
                BistroBuilderFinanceTransactionKind.Credit, 300L, 5));
            PostFinance(service, Request(
                "cash_out_other_day5", "future.finance", "future_debit_test_3g",
                "financing.debit",
                BistroBuilderFinanceTransactionKind.Debit, 100L, 5));

            finance = service.CreateSnapshot();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }

        productCost = new BistroBuilderProductCostSnapshot();
        AppendCostLine(productCost, "order_lunch_table", "line_lunch_table",
            "dish_lunch_a", BistroBuilderMealServiceAvailability.Lunch,
            BistroBuilderServiceMode.TableService, 5, 10000, 2500L, 3000L,
            BistroBuilderProductCostQuality.Actual);
        AppendCostLine(productCost, "order_lunch_bar", "line_lunch_bar",
            "dish_lunch_b", BistroBuilderMealServiceAvailability.Lunch,
            BistroBuilderServiceMode.BarService, 5, 5000, 1000L, 1000L,
            BistroBuilderProductCostQuality.Mixed);
        AppendCostLine(productCost, "order_breakfast_table", "line_breakfast_table",
            "dish_breakfast_a", BistroBuilderMealServiceAvailability.Breakfast,
            BistroBuilderServiceMode.TableService, 5, 2000, 500L, 600L,
            BistroBuilderProductCostQuality.Estimated);
        AppendCostLine(productCost, "order_other_day", "line_other_day",
            "dish_other_day", BistroBuilderMealServiceAvailability.Lunch,
            BistroBuilderServiceMode.TableService, 6, 9000, 2000L, 2100L,
            BistroBuilderProductCostQuality.Actual);

        purchaseOrders = new List<BistroBuilderPurchaseOrderRecord>
        {
            new BistroBuilderPurchaseOrderRecord
            {
                purchaseOrderId = "po_test_3g",
                displayCode = "PO-3G-TEST",
                status = BistroBuilderPurchaseOrderStatus.InDelivery,
                createdGameDay = 4,
                confirmedGameDay = 4,
                inDeliveryGameDay = 5,
                currencyCode = "EUR",
                subtotalCents = 6500L,
                shippingCostCents = 500L,
                totalCents = 7000L
            }
        };

        string financeError = string.Empty;
        if (finance == null ||
            !BistroBuilderFinanceEngine.TryValidateSnapshot(
                finance,
                out financeError))
        {
            throw new InvalidOperationException(
                "Fixture financiero 3G inválido: " + financeError);
        }

        if (!BistroBuilderProductCostEngine.TryValidateSnapshot(
                productCost,
                out string productError))
        {
            throw new InvalidOperationException(
                "Fixture COGS 3G inválido: " + productError);
        }
    }

    private static void PostSale(
        BistroBuilderFinanceService service,
        string orderId,
        BistroBuilderServiceMode mode,
        BistroBuilderMealServiceAvailability mealService,
        long amountCents,
        int dayIndex)
    {
        if (!BistroBuilderSalesRevenuePolicy.TryBuildRequest(
                orderId, mode, mealService, amountCents, dayIndex, 720,
                out BistroBuilderFinanceTransactionRequest request,
                out string error))
        {
            throw new InvalidOperationException(error);
        }
        PostFinance(service, request);
    }

    private static void AddSaleToSnapshot(
        BistroBuilderFinanceSnapshot snapshot,
        string orderId,
        BistroBuilderServiceMode mode,
        BistroBuilderMealServiceAvailability mealService,
        long amountCents,
        int dayIndex)
    {
        GameObject go = new GameObject("BB_3G_GapFixture");
        try
        {
            BistroBuilderFinanceService service =
                go.AddComponent<BistroBuilderFinanceService>();
            if (!service.TryRestoreSnapshot(snapshot, out string restoreError))
            {
                throw new InvalidOperationException(restoreError);
            }

            PostSale(service, orderId, mode, mealService, amountCents, dayIndex);
            BistroBuilderFinanceSnapshot updated = service.CreateSnapshot();

            snapshot.schemaId = updated.schemaId;
            snapshot.schemaVersion = updated.schemaVersion;
            snapshot.currencyCode = updated.currencyCode;
            snapshot.openingBalanceCents = updated.openingBalanceCents;
            snapshot.currentBalanceCents = updated.currentBalanceCents;
            snapshot.revision = updated.revision;
            snapshot.nextTransactionSequence = updated.nextTransactionSequence;
            snapshot.transactions = updated.transactions;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static BistroBuilderFinanceTransactionRequest Request(
        string operationId,
        string sourceSystemId,
        string sourceReferenceId,
        string categoryId,
        BistroBuilderFinanceTransactionKind kind,
        long amountCents,
        int dayIndex)
    {
        return new BistroBuilderFinanceTransactionRequest
        {
            operationId = operationId,
            sourceSystemId = sourceSystemId,
            sourceReferenceId = sourceReferenceId,
            categoryId = categoryId,
            kind = kind,
            amountCents = amountCents,
            dayIndex = dayIndex,
            minuteOfDay = 720,
            description = "3G autotest"
        };
    }

    private static void PostFinance(
        BistroBuilderFinanceService service,
        BistroBuilderFinanceTransactionRequest request)
    {
        if (!service.TryPostTransaction(request, out _, out string error))
        {
            throw new InvalidOperationException(
                "No se pudo construir fixture financiero 3G: " + error);
        }
    }

    private static void AppendCostLine(
        BistroBuilderProductCostSnapshot snapshot,
        string orderId,
        string lineId,
        string dishId,
        BistroBuilderMealServiceAvailability mealService,
        BistroBuilderServiceMode serviceMode,
        int dayIndex,
        int salePriceCents,
        long theoreticalCostCents,
        long actualCostCents,
        BistroBuilderProductCostQuality quality)
    {
        long sequence = snapshot.nextLineCostSequence;
        long microPerCent = BistroBuilderIngredientDefinition.MicroCentsPerCent;
        long theoreticalMicro = checked(theoreticalCostCents * microPerCent);
        long actualMicro = checked(actualCostCents * microPerCent);
        long theoreticalMargin = salePriceCents - theoreticalCostCents;
        long actualMargin = salePriceCents - actualCostCents;
        int theoreticalBp =
            BistroBuilderProductCostEngine.CalculateMarginBasisPoints(
                salePriceCents, theoreticalCostCents);
        int actualBp =
            BistroBuilderProductCostEngine.CalculateMarginBasisPoints(
                salePriceCents, actualCostCents);

        snapshot.consumedLineCosts.Add(
            new BistroBuilderConsumedLineCostRecord
            {
                sequence = sequence,
                costRecordId = BistroBuilderProductCostEngine.BuildCostRecordId(sequence),
                orderId = orderId,
                lineId = lineId,
                dishId = dishId,
                mealService = mealService,
                serviceMode = serviceMode,
                dayIndex = dayIndex,
                minuteOfDay = 720,
                salePriceCents = salePriceCents,
                theoreticalCostMicroCents = theoreticalMicro,
                theoreticalCostCents = theoreticalCostCents,
                actualCostMicroCents = actualMicro,
                actualCostCents = actualCostCents,
                theoreticalMarginCents = theoreticalMargin,
                theoreticalMarginBasisPoints = theoreticalBp,
                theoreticalMarginBand =
                    BistroBuilderProductCostEngine.ResolveMarginBand(
                        theoreticalMargin, theoreticalBp),
                actualMarginCents = actualMargin,
                actualMarginBasisPoints = actualBp,
                actualMarginBand =
                    BistroBuilderProductCostEngine.ResolveMarginBand(
                        actualMargin, actualBp),
                costQuality = quality
            });

        snapshot.nextLineCostSequence = checked(sequence + 1L);
        snapshot.revision = checked(snapshot.revision + 1L);
    }

    private static void Check(
        bool condition,
        string label,
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        if (condition)
        {
            passed++;
            builder.AppendLine("[OK] " + label);
        }
        else
        {
            failed++;
            builder.AppendLine("[ERROR] " + label);
        }
    }
}
