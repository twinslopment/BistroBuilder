using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fachada de lectura/acciones permitidas para la UI jugable 3J.
///
/// No posee caja, resultados, históricos ni deuda. Compone exclusivamente
/// 3A/3G/3H/3I y canaliza la aceptación de financiación por la API pública 3I.
/// No tiene sección Save.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Finance Dashboard Service 3J")]
public sealed class BistroBuilderFinanceDashboardService : MonoBehaviour
{
    [SerializeField] private BistroBuilderFinanceService financeService;
    [SerializeField] private BistroBuilderFinancialResultsService resultsService;
    [SerializeField] private BistroBuilderFinancialHistoryService historyService;
    [SerializeField] private BistroBuilderFinancingService financingService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;

    private readonly List<BistroBuilderFinancingOfferView> offerBuffer =
        new List<BistroBuilderFinancingOfferView>(8);
    private readonly List<BistroBuilderLoanRecord> loanBuffer =
        new List<BistroBuilderLoanRecord>(8);

    private long presentationRevision = 1L;
    private bool subscribed;

    public event Action DashboardChanged;

    public BistroBuilderFinanceService FinanceService => financeService;
    public BistroBuilderFinancialResultsService ResultsService => resultsService;
    public BistroBuilderFinancialHistoryService HistoryService => historyService;
    public BistroBuilderFinancingService FinancingService => financingService;
    public BistroBuilderGeneralGameStateService GeneralGameStateService =>
        generalGameStateService;
    public long PresentationRevision => presentationRevision;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (financeService == null ||
            resultsService == null ||
            historyService == null ||
            financingService == null ||
            generalGameStateService == null)
        {
            error =
                "3J necesita Finanzas 3A, Resultados 3G, Históricos 3H, " +
                "Financiación 3I y calendario canónico.";
            return false;
        }

        if (!financeService.ValidateConfiguration(out error) ||
            !resultsService.ValidateConfiguration(out error) ||
            !historyService.ValidateConfiguration(out error) ||
            !financingService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (!ReferenceEquals(resultsService.FinanceService, financeService) ||
            !ReferenceEquals(historyService.FinancialResultsService, resultsService) ||
            !ReferenceEquals(financingService.FinanceService, financeService) ||
            !ReferenceEquals(resultsService.GeneralGameStateService, generalGameStateService) ||
            !ReferenceEquals(historyService.GeneralGameStateService, generalGameStateService) ||
            !ReferenceEquals(financingService.GeneralGameStateService, generalGameStateService))
        {
            error = "3J no comparte exactamente las autoridades canónicas de 3A/3G/3H/3I.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Construye en una sola operación el read-model necesario por la vista.
    /// Las colecciones devueltas son copias y nunca referencias mutables a las
    /// autoridades subyacentes.
    /// </summary>
    public bool TryBuildDashboard(
        BistroBuilderFinanceDashboardPeriod period,
        int maximumRecentMovements,
        out BistroBuilderFinanceDashboardSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        if (!ValidateConfiguration(out error))
        {
            return false;
        }
        if (!Enum.IsDefined(typeof(BistroBuilderFinanceDashboardPeriod), period))
        {
            error = "El periodo solicitado por 3J no existe.";
            return false;
        }
        if (maximumRecentMovements < 0 || maximumRecentMovements > 500)
        {
            error = "El límite de movimientos recientes debe estar entre 0 y 500.";
            return false;
        }

        int currentDay = Math.Max(1, generalGameStateService.DayIndex);
        ResolvePeriodRange(period, currentDay, out int startDay, out int endDay);

        if (!resultsService.TryGetDayResult(
                currentDay,
                out BistroBuilderDayFinancialResult today,
                out error) ||
            !historyService.TryGetPeriodReport(
                startDay,
                endDay,
                out BistroBuilderFinancialPeriodReport periodReport,
                out error) ||
            !financingService.TryGetLiquidityPosition(
                out BistroBuilderLiquidityPosition liquidity,
                out error) ||
            !financingService.TryGetFinancialStress(
                out BistroBuilderFinancialStressSnapshot stress,
                out error))
        {
            return false;
        }

        BistroBuilderFinancialPeriodComparison comparison = null;
        long periodLength = (long)endDay - startDay + 1L;
        if (period != BistroBuilderFinanceDashboardPeriod.AllTime &&
            (long)startDay - periodLength >= 1L)
        {
            if (!historyService.TryCompareWithPreviousPeriod(
                    startDay,
                    endDay,
                    out comparison,
                    out error))
            {
                return false;
            }
        }

        var built = new BistroBuilderFinanceDashboardSnapshot
        {
            dayIndex = currentDay,
            currencyCode = financeService.CurrencyCode,
            financeRevision = financeService.Revision,
            financingRevision = financingService.CreateSnapshot() != null
                ? financingService.CreateSnapshot().revision
                : 0L,
            period = period,
            periodStartDayIndex = startDay,
            periodEndDayIndex = endDay,
            cashBalanceCents = financeService.CurrentBalanceCents,
            currentDay = today,
            periodReport = periodReport,
            periodComparison = comparison,
            liquidity = liquidity,
            stress = stress
        };

        if (!CopyRecentMovements(
                maximumRecentMovements,
                built.recentMovements,
                out error))
        {
            return false;
        }

        offerBuffer.Clear();
        if (!financingService.TryGetOfferViews(offerBuffer, out error))
        {
            return false;
        }
        for (int index = 0; index < offerBuffer.Count; index++)
        {
            if (offerBuffer[index] != null)
            {
                built.financingOffers.Add(offerBuffer[index].DeepClone());
            }
        }

        loanBuffer.Clear();
        financingService.CopyLoans(loanBuffer);
        for (int index = 0; index < loanBuffer.Count; index++)
        {
            if (loanBuffer[index] != null)
            {
                built.loans.Add(loanBuffer[index].DeepClone());
            }
        }

        snapshot = built;
        error = string.Empty;
        return true;
    }

    public bool CopyRecentMovements(
        int maximumRows,
        List<BistroBuilderFinanceMovementView> destination,
        out string error)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }
        destination.Clear();

        if (maximumRows < 0 || maximumRows > 500)
        {
            error = "El límite de movimientos debe estar entre 0 y 500.";
            return false;
        }

        BistroBuilderFinanceSnapshot finance = financeService != null
            ? financeService.CreateSnapshot()
            : null;
        if (finance == null ||
            !BistroBuilderFinanceEngine.TryValidateSnapshot(finance, out error))
        {
            return false;
        }

        int added = 0;
        for (int index = finance.transactions.Count - 1;
             index >= 0 && added < maximumRows;
             index--)
        {
            BistroBuilderFinanceTransactionRecord transaction =
                finance.transactions[index];
            if (transaction == null)
            {
                continue;
            }

            destination.Add(BuildMovementView(transaction));
            added++;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// La vista genera el token al abrir la confirmación y lo conserva hasta
    /// resolverla. Un doble callback con el mismo token es idempotente en 3I.
    /// </summary>
    public bool TryAcceptFinancingOffer(
        string offerId,
        string confirmationToken,
        out BistroBuilderLoanRecord loan,
        out string error)
    {
        loan = null;
        if (!ValidateConfiguration(out error))
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(offerId) ||
            string.IsNullOrWhiteSpace(confirmationToken))
        {
            error = "La aceptación de financiación necesita oferta y token de confirmación.";
            return false;
        }

        string token = NormalizeToken(confirmationToken);
        if (token.Length < 8 || token.Length > 80)
        {
            error = "El token de confirmación de financiación no es válido.";
            return false;
        }

        string normalizedOffer = NormalizeToken(offerId);
        string operationId = "finance_ui_accept_" + normalizedOffer + "_" + token;
        return financingService.TryAcceptOffer(
            offerId,
            operationId,
            out loan,
            out error);
    }

    public static string CreateAcceptanceToken()
    {
        return Guid.NewGuid().ToString("N").ToLowerInvariant();
    }

    public static void ResolvePeriodRange(
        BistroBuilderFinanceDashboardPeriod period,
        int currentDay,
        out int startDay,
        out int endDay)
    {
        endDay = Math.Max(1, currentDay);
        int requestedDays;
        switch (period)
        {
            case BistroBuilderFinanceDashboardPeriod.Last30Days:
                requestedDays = 30;
                break;
            case BistroBuilderFinanceDashboardPeriod.Last90Days:
                requestedDays = 90;
                break;
            case BistroBuilderFinanceDashboardPeriod.AllTime:
                startDay = 1;
                return;
            default:
                requestedDays = 7;
                break;
        }

        long start = (long)endDay - requestedDays + 1L;
        startDay = start < 1L ? 1 : (int)start;
    }

    private static BistroBuilderFinanceMovementView BuildMovementView(
        BistroBuilderFinanceTransactionRecord transaction)
    {
        BistroBuilderFinanceMovementGroup group = ResolveMovementGroup(
            transaction.categoryId);
        return new BistroBuilderFinanceMovementView
        {
            sequence = transaction.sequence,
            transactionId = transaction.transactionId,
            operationId = transaction.operationId,
            sourceReferenceId = transaction.sourceReferenceId,
            categoryId = transaction.categoryId,
            kind = transaction.kind,
            group = group,
            amountCents = transaction.amountCents,
            dayIndex = transaction.dayIndex,
            minuteOfDay = transaction.minuteOfDay,
            displayLabel = ResolveMovementLabel(group, transaction.categoryId),
            description = transaction.description ?? string.Empty
        };
    }

    private static BistroBuilderFinanceMovementGroup ResolveMovementGroup(
        string categoryId)
    {
        string category = NormalizeCategory(categoryId);
        if (category.StartsWith("sales.", StringComparison.Ordinal))
        {
            return BistroBuilderFinanceMovementGroup.Sales;
        }
        if (category == BistroBuilderSupplierPurchaseFinancePolicy.CategoryId)
        {
            return BistroBuilderFinanceMovementGroup.Supplier;
        }
        if (category.StartsWith("expense.operating.", StringComparison.Ordinal))
        {
            return BistroBuilderFinanceMovementGroup.Operating;
        }
        if (category == BistroBuilderOperatingExpensePolicy.PayrollCategoryId)
        {
            return BistroBuilderFinanceMovementGroup.Payroll;
        }
        if (category == "expense.marketing" ||
            category.StartsWith("expense.marketing.", StringComparison.Ordinal))
        {
            return BistroBuilderFinanceMovementGroup.Marketing;
        }
        if (category.StartsWith("investment.", StringComparison.Ordinal))
        {
            return BistroBuilderFinanceMovementGroup.Investment;
        }
        if (category == BistroBuilderFinancingEngine.LoanProceedsCategoryId ||
            category == BistroBuilderFinancingEngine.PrincipalRepaymentCategoryId ||
            category == BistroBuilderFinancingEngine.InterestExpenseCategoryId)
        {
            return BistroBuilderFinanceMovementGroup.Financing;
        }
        if (category == "income.asset_resale" ||
            category == "expense.demolition" ||
            category == "expense.asset_removal")
        {
            return BistroBuilderFinanceMovementGroup.Asset;
        }
        return BistroBuilderFinanceMovementGroup.Other;
    }

    private static string ResolveMovementLabel(
        BistroBuilderFinanceMovementGroup group,
        string categoryId)
    {
        string category = NormalizeCategory(categoryId);
        switch (group)
        {
            case BistroBuilderFinanceMovementGroup.Sales:
                return "Venta cobrada";
            case BistroBuilderFinanceMovementGroup.Supplier:
                return "Compra a proveedor";
            case BistroBuilderFinanceMovementGroup.Operating:
                return "Gasto operativo";
            case BistroBuilderFinanceMovementGroup.Payroll:
                return "Nómina";
            case BistroBuilderFinanceMovementGroup.Marketing:
                return "Marketing";
            case BistroBuilderFinanceMovementGroup.Investment:
                return "Inversión";
            case BistroBuilderFinanceMovementGroup.Financing:
                if (category == BistroBuilderFinancingEngine.LoanProceedsCategoryId)
                {
                    return "Financiación recibida";
                }
                if (category == BistroBuilderFinancingEngine.PrincipalRepaymentCategoryId)
                {
                    return "Amortización de deuda";
                }
                return "Intereses de financiación";
            case BistroBuilderFinanceMovementGroup.Asset:
                return category == "income.asset_resale"
                    ? "Reventa de activo"
                    : "Baja / retirada de activo";
            default:
                return "Movimiento de caja";
        }
    }

    private void NotifyChanged()
    {
        if (presentationRevision < long.MaxValue)
        {
            presentationRevision++;
        }
        DashboardChanged?.Invoke();
    }

    private void HandleFinanceTransaction(BistroBuilderFinanceTransactionRecord _)
    {
        NotifyChanged();
    }

    private void HandleFinanceRestored()
    {
        NotifyChanged();
    }

    private void HandleHistoryChanged()
    {
        NotifyChanged();
    }

    private void HandleFinancingChanged()
    {
        NotifyChanged();
    }

    private void HandleCalendarChanged()
    {
        NotifyChanged();
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }
        CacheDependencies();
        if (financeService != null)
        {
            financeService.TransactionPosted += HandleFinanceTransaction;
            financeService.StateRestored += HandleFinanceRestored;
        }
        if (historyService != null)
        {
            historyService.HistoryChanged += HandleHistoryChanged;
        }
        if (financingService != null)
        {
            financingService.FinancingChanged += HandleFinancingChanged;
        }
        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged += HandleCalendarChanged;
        }
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }
        if (financeService != null)
        {
            financeService.TransactionPosted -= HandleFinanceTransaction;
            financeService.StateRestored -= HandleFinanceRestored;
        }
        if (historyService != null)
        {
            historyService.HistoryChanged -= HandleHistoryChanged;
        }
        if (financingService != null)
        {
            financingService.FinancingChanged -= HandleFinancingChanged;
        }
        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleCalendarChanged;
        }
        subscribed = false;
    }

    private void CacheDependencies()
    {
        if (financeService == null)
        {
            financeService = FindFirstObjectByType<BistroBuilderFinanceService>();
        }
        if (resultsService == null)
        {
            resultsService = FindFirstObjectByType<BistroBuilderFinancialResultsService>();
        }
        if (historyService == null)
        {
            historyService = FindFirstObjectByType<BistroBuilderFinancialHistoryService>();
        }
        if (financingService == null)
        {
            financingService = FindFirstObjectByType<BistroBuilderFinancingService>();
        }
        if (generalGameStateService == null)
        {
            generalGameStateService =
                FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        }
    }

    private static string NormalizeToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("/", "_")
                .Replace("\\", "_");
    }

    private static string NormalizeCategory(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependencies();
    }

    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
