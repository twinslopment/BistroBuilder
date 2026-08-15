using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad de 3I para contratos de deuda y lectura de liquidez.
/// No posee caja: todo desembolso y pago se publica en Finanzas 3A.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Financing Service")]
public sealed class BistroBuilderFinancingService : MonoBehaviour
{
    [SerializeField] private BistroBuilderFinanceService financeService;
    [SerializeField] private BistroBuilderSupplierPurchaseFinanceBridge supplierFinanceBridge;
    [SerializeField] private BistroBuilderFinancialHistoryService financialHistoryService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private GameClock gameClock;

    [SerializeField, Min(1)] private int liquidityHorizonDays = 7;
    [SerializeField, Min(1)] private int lossAnalysisWindowDays = 7;
    [SerializeField, Min(0)] private int delinquencyGraceDays = 7;
    [SerializeField, Min(1)] private int maxActiveLoans = 3;
    [SerializeField, Min(0)] private long maxOutstandingPrincipalCents = 5000000L;
    [SerializeField] private List<BistroBuilderFinancingOfferDefinition> offers =
        BistroBuilderFinancingEngine.CreateDefaultOffers();

    private readonly List<BistroBuilderFinanceTransactionRequest> requestBuffer =
        new List<BistroBuilderFinanceTransactionRequest>(2);
    private BistroBuilderFinancingSnapshot state;

    public event Action FinancingChanged;
    public event Action<BistroBuilderLoanRecord> LoanOpened;
    public event Action<BistroBuilderDebtPaymentProcessResult> DebtPaymentsProcessed;

    public bool IsInitialized => state != null;
    public BistroBuilderFinanceService FinanceService => financeService;
    public BistroBuilderSupplierPurchaseFinanceBridge SupplierFinanceBridge => supplierFinanceBridge;
    public BistroBuilderFinancialHistoryService FinancialHistoryService => financialHistoryService;
    public BistroBuilderGeneralGameStateService GeneralGameStateService => generalGameStateService;
    public int LiquidityHorizonDays => liquidityHorizonDays;
    public int LossAnalysisWindowDays => lossAnalysisWindowDays;
    public int DelinquencyGraceDays => delinquencyGraceDays;
    public int MaxActiveLoans => maxActiveLoans;
    public long MaxOutstandingPrincipalCents => maxOutstandingPrincipalCents;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        if (state == null && !TryInitializeFresh(out string error))
        {
            Debug.LogError("3I no pudo inicializarse. " + error, this);
        }
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleCalendarChanged;
            generalGameStateService.CalendarChanged += HandleCalendarChanged;
        }
    }

    private void OnDisable()
    {
        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleCalendarChanged;
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();
        if (financeService == null || supplierFinanceBridge == null ||
            financialHistoryService == null || generalGameStateService == null ||
            gameClock == null)
        {
            error = "3I necesita Finanzas 3A, compromisos 3C, Históricos 3H, calendario y reloj.";
            return false;
        }

        if (!financeService.ValidateConfiguration(out error) ||
            !supplierFinanceBridge.ValidateConfiguration(out error) ||
            !financialHistoryService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (!ReferenceEquals(financialHistoryService.GeneralGameStateService, generalGameStateService))
        {
            error = "3I y 3H no comparten el calendario canónico.";
            return false;
        }

        if (liquidityHorizonDays < 1 || lossAnalysisWindowDays < 1 ||
            delinquencyGraceDays < 0 || maxActiveLoans < 1 ||
            maxOutstandingPrincipalCents <= 0L)
        {
            error = "Los límites de liquidez/deuda de 3I no son válidos.";
            return false;
        }

        return BistroBuilderFinancingEngine.TryValidateOffers(offers, out error);
    }

    public bool TryInitializeFresh(out string error)
    {
        if (!ValidateConfiguration(out error))
        {
            return false;
        }
        state = new BistroBuilderFinancingSnapshot();
        error = string.Empty;
        return true;
    }

    public BistroBuilderFinancingSnapshot CreateSnapshot()
    {
        return state != null ? state.DeepClone() : null;
    }

    public bool TryRestoreSnapshot(BistroBuilderFinancingSnapshot candidate, out string error)
    {
        if (!ValidateConfiguration(out error) ||
            !BistroBuilderFinancingEngine.TryValidateSnapshot(candidate, out error))
        {
            return false;
        }

        state = candidate.DeepClone();
        BistroBuilderFinancingEngine.RefreshLoanStatuses(
            state,
            generalGameStateService.DayIndex,
            delinquencyGraceDays);
        FinancingChanged?.Invoke();
        return true;
    }

    public int CopyOffers(List<BistroBuilderFinancingOfferDefinition> buffer)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        buffer.Clear();
        for (int index = 0; index < offers.Count; index++)
        {
            if (offers[index] != null)
            {
                buffer.Add(offers[index].DeepClone());
            }
        }
        return buffer.Count;
    }

    public int CopyLoans(List<BistroBuilderLoanRecord> buffer)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        buffer.Clear();
        if (state == null)
        {
            return 0;
        }
        for (int index = 0; index < state.loans.Count; index++)
        {
            if (state.loans[index] != null)
            {
                buffer.Add(state.loans[index].DeepClone());
            }
        }
        return buffer.Count;
    }

    public bool TryGetOfferViews(
        List<BistroBuilderFinancingOfferView> buffer,
        out string error)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        buffer.Clear();
        if (!EnsureInitialized(out error) ||
            !TryGetLiquidityPosition(out BistroBuilderLiquidityPosition liquidity, out error))
        {
            return false;
        }

        CountDebtState(out int activeLoans, out long outstandingPrincipal, out bool hasDefaultedLoan);
        bool hasOverdue = liquidity.overdueDebtCents > 0L;

        for (int index = 0; index < offers.Count; index++)
        {
            BistroBuilderFinancingOfferDefinition offer = offers[index];
            if (offer == null || !offer.active)
            {
                continue;
            }

            long interest = BistroBuilderFinancingEngine.CalculateInterestCents(
                offer.principalCents,
                offer.totalInterestBasisPoints);
            bool eligible = true;
            string reason = string.Empty;

            if (hasDefaultedLoan || hasOverdue)
            {
                eligible = false;
                reason = "Hay deuda vencida o impagada.";
            }
            else if (activeLoans >= maxActiveLoans)
            {
                eligible = false;
                reason = "Se alcanzó el máximo de préstamos activos.";
            }
            else
            {
                long projectedPrincipal;
                try
                {
                    projectedPrincipal = checked(outstandingPrincipal + offer.principalCents);
                }
                catch (OverflowException)
                {
                    projectedPrincipal = long.MaxValue;
                }
                if (projectedPrincipal > maxOutstandingPrincipalCents)
                {
                    eligible = false;
                    reason = "La deuda proyectada supera el límite financiable.";
                }
            }

            buffer.Add(new BistroBuilderFinancingOfferView
            {
                offerId = offer.offerId,
                displayName = offer.displayName,
                principalCents = offer.principalCents,
                termDays = offer.termDays,
                totalInterestBasisPoints = offer.totalInterestBasisPoints,
                totalInterestCents = interest,
                totalPayableCents = checked(offer.principalCents + interest),
                installmentCount = offer.installmentCount,
                eligible = eligible,
                ineligibilityReason = reason
            });
        }

        error = string.Empty;
        return true;
    }

    public bool TryAcceptOffer(
        string offerId,
        string acceptanceOperationId,
        out BistroBuilderLoanRecord loan,
        out string error)
    {
        loan = null;
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        BistroBuilderFinancingOfferDefinition offer = FindOffer(offerId);
        if (offer == null || !offer.active)
        {
            error = "La oferta de financiación no existe o no está activa.";
            return false;
        }

        for (int index = 0; index < state.loans.Count; index++)
        {
            BistroBuilderLoanRecord existing = state.loans[index];
            if (string.Equals(existing.acceptanceOperationId, acceptanceOperationId, StringComparison.Ordinal))
            {
                if (!string.Equals(existing.offerId, offer.offerId, StringComparison.Ordinal))
                {
                    error = "El OperationId ya fue usado para otra financiación.";
                    return false;
                }
                loan = existing.DeepClone();
                error = string.Empty;
                return true;
            }
        }

        var views = new List<BistroBuilderFinancingOfferView>();
        if (!TryGetOfferViews(views, out error))
        {
            return false;
        }
        BistroBuilderFinancingOfferView view = views.Find(
            candidate => string.Equals(candidate.offerId, offer.offerId, StringComparison.Ordinal));
        if (view == null || !view.eligible)
        {
            error = view != null ? view.ineligibilityReason : "La oferta no es financiable.";
            return false;
        }

        BistroBuilderFinancingSnapshot candidateState = state.DeepClone();
        if (!BistroBuilderFinancingEngine.TryCreateLoan(
                candidateState,
                offer,
                acceptanceOperationId,
                generalGameStateService.DayIndex,
                out BistroBuilderLoanRecord created,
                out error))
        {
            return false;
        }

        BistroBuilderFinanceTransactionRequest request =
            BistroBuilderFinancingEngine.BuildDisbursementRequest(
                created,
                gameClock.Hour * 60 + gameClock.Minute);
        if (!financeService.TryPostTransaction(request, out _, out error))
        {
            return false;
        }

        state = candidateState;
        loan = created.DeepClone();
        FinancingChanged?.Invoke();
        LoanOpened?.Invoke(created.DeepClone());
        return true;
    }

    public bool TryProcessDuePayments(
        int dayIndex,
        out BistroBuilderDebtPaymentProcessResult result,
        out string error)
    {
        result = new BistroBuilderDebtPaymentProcessResult();
        if (!EnsureInitialized(out error) || dayIndex < 1 ||
            dayIndex > generalGameStateService.DayIndex)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "3I no puede procesar cuotas de un día futuro o inválido.";
            }
            return false;
        }

        BistroBuilderFinancingSnapshot candidate = state.DeepClone();
        bool changed = false;

        for (int loanIndex = 0; loanIndex < candidate.loans.Count; loanIndex++)
        {
            BistroBuilderLoanRecord loan = candidate.loans[loanIndex];
            for (int installmentIndex = 0; installmentIndex < loan.installments.Count; installmentIndex++)
            {
                BistroBuilderLoanInstallmentRecord installment = loan.installments[installmentIndex];
                if (installment.status == BistroBuilderLoanInstallmentStatus.Paid ||
                    installment.dueDayIndex > dayIndex)
                {
                    continue;
                }

                result.examinedInstallments++;
                long installmentTotal = checked(installment.principalCents + installment.interestCents);
                if (financeService.CurrentBalanceCents < installmentTotal)
                {
                    result.unpaidDueCents = checked(result.unpaidDueCents + installmentTotal);
                    if (installment.status != BistroBuilderLoanInstallmentStatus.Overdue)
                    {
                        installment.status = BistroBuilderLoanInstallmentStatus.Overdue;
                        result.newlyOverdueInstallments++;
                        changed = true;
                    }
                    continue;
                }

                BistroBuilderFinancingEngine.BuildInstallmentRequests(
                    loan,
                    installment,
                    dayIndex,
                    gameClock.Hour * 60 + gameClock.Minute,
                    requestBuffer);
                if (!financeService.TryPostTransactions(requestBuffer, out _, out error))
                {
                    return false;
                }

                installment.status = BistroBuilderLoanInstallmentStatus.Paid;
                installment.paidDayIndex = dayIndex;
                result.paidInstallments++;
                result.principalPaidCents = checked(
                    result.principalPaidCents + installment.principalCents);
                result.interestPaidCents = checked(
                    result.interestPaidCents + installment.interestCents);
                changed = true;
            }
        }

        int defaultedBefore = CountDefaulted(candidate);
        BistroBuilderFinancingEngine.RefreshLoanStatuses(
            candidate,
            dayIndex,
            delinquencyGraceDays);
        int defaultedAfter = CountDefaulted(candidate);
        result.defaultedLoans = Math.Max(0, defaultedAfter - defaultedBefore);

        if (changed || !SnapshotsEquivalentStatus(state, candidate))
        {
            candidate.revision = checked(candidate.revision + 1L);
            state = candidate;
            FinancingChanged?.Invoke();
        }

        DebtPaymentsProcessed?.Invoke(result);
        error = string.Empty;
        return true;
    }

    public bool TryGetLiquidityPosition(
        out BistroBuilderLiquidityPosition position,
        out string error)
    {
        position = null;
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        int dayIndex = generalGameStateService.DayIndex;
        BistroBuilderFinancingSnapshot snapshot = state.DeepClone();
        BistroBuilderFinancingEngine.RefreshLoanStatuses(snapshot, dayIndex, delinquencyGraceDays);
        BistroBuilderFinancingEngine.CalculateDebtTotals(
            snapshot,
            dayIndex,
            liquidityHorizonDays,
            out long dueToday,
            out long dueHorizon,
            out long overdue,
            out long outstandingPrincipal,
            out long outstandingInterest);

        bool commitmentsResolved = supplierFinanceBridge.TryGetFinancialPosition(
            out long committed,
            out _,
            out _);
        if (!commitmentsResolved)
        {
            committed = 0L;
        }

        long available = checked(financeService.CurrentBalanceCents - committed);
        long projected = checked(available - dueHorizon);
        BistroBuilderLiquidityStatus status =
            BistroBuilderFinancingEngine.ResolveLiquidityStatus(
                financeService.CurrentBalanceCents,
                available,
                dueHorizon,
                overdue,
                out int coverageBasisPoints);

        position = new BistroBuilderLiquidityPosition
        {
            dayIndex = dayIndex,
            horizonDays = liquidityHorizonDays,
            cashBalanceCents = financeService.CurrentBalanceCents,
            supplierCommitmentsResolved = commitmentsResolved,
            supplierCommittedCents = committed,
            availableCashAfterSupplierCommitmentsCents = available,
            debtDueTodayCents = dueToday,
            debtDueWithinHorizonCents = dueHorizon,
            overdueDebtCents = overdue,
            outstandingPrincipalCents = outstandingPrincipal,
            outstandingInterestCents = outstandingInterest,
            projectedLiquidityAfterHorizonObligationsCents = projected,
            debtCoverageBasisPoints = coverageBasisPoints,
            status = status
        };
        error = string.Empty;
        return true;
    }

    public bool TryGetFinancialStress(
        out BistroBuilderFinancialStressSnapshot stress,
        out string error)
    {
        stress = null;
        if (!TryGetLiquidityPosition(out BistroBuilderLiquidityPosition liquidity, out error) ||
            !financialHistoryService.TryGetCurrentRollingReport(
                lossAnalysisWindowDays,
                out BistroBuilderFinancialPeriodReport report,
                out error))
        {
            return false;
        }

        int consecutiveLossDays = 0;
        if (report.dailyResults != null)
        {
            for (int index = report.dailyResults.Count - 1; index >= 0; index--)
            {
                BistroBuilderDayFinancialResult day = report.dailyResults[index];
                if (day == null || day.operatingResultCents >= 0L)
                {
                    break;
                }
                consecutiveLossDays++;
            }
        }

        bool hasDefaultedLoan = false;
        for (int index = 0; index < state.loans.Count; index++)
        {
            if (state.loans[index].status == BistroBuilderLoanStatus.Defaulted)
            {
                hasDefaultedLoan = true;
                break;
            }
        }

        long outstandingDebt = checked(
            liquidity.outstandingPrincipalCents + liquidity.outstandingInterestCents);
        stress = new BistroBuilderFinancialStressSnapshot
        {
            dayIndex = generalGameStateService.DayIndex,
            analysisWindowDays = report.dayCount,
            rollingRevenueCents = report.revenueCents,
            rollingOperatingResultCents = report.operatingResultCents,
            rollingLossDayCount = report.lossDayCount,
            consecutiveLossDays = consecutiveLossDays,
            outstandingDebtCents = outstandingDebt,
            overdueDebtCents = liquidity.overdueDebtCents,
            liquidityStatus = liquidity.status,
            riskLevel = BistroBuilderFinancingEngine.ResolveRisk(
                liquidity.status,
                consecutiveLossDays,
                report.operatingResultCents,
                outstandingDebt,
                hasDefaultedLoan)
        };
        error = string.Empty;
        return true;
    }

    public bool TryValidateLedgerConsistency(out string error)
    {
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        for (int loanIndex = 0; loanIndex < state.loans.Count; loanIndex++)
        {
            BistroBuilderLoanRecord loan = state.loans[loanIndex];
            if (!financeService.TryGetTransactionByOperationId(
                    BistroBuilderFinancingEngine.BuildDisbursementOperationId(loan.loanId),
                    out BistroBuilderFinanceTransactionRecord disbursement) ||
                disbursement.kind != BistroBuilderFinanceTransactionKind.Credit ||
                disbursement.amountCents != loan.principalCents)
            {
                error = "El préstamo " + loan.loanId + " no tiene un desembolso financiero coherente.";
                return false;
            }

            for (int installmentIndex = 0; installmentIndex < loan.installments.Count; installmentIndex++)
            {
                BistroBuilderLoanInstallmentRecord installment = loan.installments[installmentIndex];
                if (installment.status != BistroBuilderLoanInstallmentStatus.Paid)
                {
                    continue;
                }

                if (!financeService.TryGetTransactionByOperationId(
                        BistroBuilderFinancingEngine.BuildPrincipalOperationId(
                            loan.loanId,
                            installment.installmentNumber),
                        out BistroBuilderFinanceTransactionRecord principalTx) ||
                    principalTx.kind != BistroBuilderFinanceTransactionKind.Debit ||
                    principalTx.amountCents != installment.principalCents)
                {
                    error = "Falta el pago de principal de una cuota marcada como pagada.";
                    return false;
                }

                if (installment.interestCents > 0L &&
                    (!financeService.TryGetTransactionByOperationId(
                        BistroBuilderFinancingEngine.BuildInterestOperationId(
                            loan.loanId,
                            installment.installmentNumber),
                        out BistroBuilderFinanceTransactionRecord interestTx) ||
                     interestTx.kind != BistroBuilderFinanceTransactionKind.Debit ||
                     interestTx.amountCents != installment.interestCents))
                {
                    error = "Falta el pago de interés de una cuota marcada como pagada.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    private void HandleCalendarChanged()
    {
        if (state == null)
        {
            return;
        }
        if (!TryProcessDuePayments(
                generalGameStateService.DayIndex,
                out _,
                out string error))
        {
            Debug.LogError("3I no pudo procesar vencimientos del nuevo día. " + error, this);
        }
    }

    private bool EnsureInitialized(out string error)
    {
        if (state != null)
        {
            error = string.Empty;
            return true;
        }
        return TryInitializeFresh(out error);
    }

    private BistroBuilderFinancingOfferDefinition FindOffer(string offerId)
    {
        string normalized = string.IsNullOrWhiteSpace(offerId)
            ? string.Empty
            : offerId.Trim().ToLowerInvariant();
        for (int index = 0; index < offers.Count; index++)
        {
            BistroBuilderFinancingOfferDefinition offer = offers[index];
            if (offer != null && string.Equals(offer.offerId, normalized, StringComparison.Ordinal))
            {
                return offer;
            }
        }
        return null;
    }

    private void CountDebtState(
        out int activeLoans,
        out long outstandingPrincipal,
        out bool hasDefaultedLoan)
    {
        activeLoans = 0;
        outstandingPrincipal = 0L;
        hasDefaultedLoan = false;
        for (int loanIndex = 0; loanIndex < state.loans.Count; loanIndex++)
        {
            BistroBuilderLoanRecord loan = state.loans[loanIndex];
            if (loan.status != BistroBuilderLoanStatus.PaidOff)
            {
                activeLoans++;
            }
            if (loan.status == BistroBuilderLoanStatus.Defaulted)
            {
                hasDefaultedLoan = true;
            }
            for (int installmentIndex = 0; installmentIndex < loan.installments.Count; installmentIndex++)
            {
                BistroBuilderLoanInstallmentRecord installment = loan.installments[installmentIndex];
                if (installment.status != BistroBuilderLoanInstallmentStatus.Paid)
                {
                    outstandingPrincipal = checked(outstandingPrincipal + installment.principalCents);
                }
            }
        }
    }

    private static int CountDefaulted(BistroBuilderFinancingSnapshot snapshot)
    {
        int count = 0;
        for (int index = 0; index < snapshot.loans.Count; index++)
        {
            if (snapshot.loans[index].status == BistroBuilderLoanStatus.Defaulted)
            {
                count++;
            }
        }
        return count;
    }

    private static bool SnapshotsEquivalentStatus(
        BistroBuilderFinancingSnapshot a,
        BistroBuilderFinancingSnapshot b)
    {
        if (a.loans.Count != b.loans.Count)
        {
            return false;
        }
        for (int loanIndex = 0; loanIndex < a.loans.Count; loanIndex++)
        {
            BistroBuilderLoanRecord left = a.loans[loanIndex];
            BistroBuilderLoanRecord right = b.loans[loanIndex];
            if (left.status != right.status || left.paidOffDayIndex != right.paidOffDayIndex)
            {
                return false;
            }
            for (int installmentIndex = 0; installmentIndex < left.installments.Count; installmentIndex++)
            {
                BistroBuilderLoanInstallmentRecord li = left.installments[installmentIndex];
                BistroBuilderLoanInstallmentRecord ri = right.installments[installmentIndex];
                if (li.status != ri.status || li.paidDayIndex != ri.paidDayIndex)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (financeService == null)
        {
            financeService = FindFirstObjectByType<BistroBuilderFinanceService>();
        }
        if (supplierFinanceBridge == null)
        {
            supplierFinanceBridge = FindFirstObjectByType<BistroBuilderSupplierPurchaseFinanceBridge>();
        }
        if (financialHistoryService == null)
        {
            financialHistoryService = FindFirstObjectByType<BistroBuilderFinancialHistoryService>();
        }
        if (generalGameStateService == null)
        {
            generalGameStateService = FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        }
        if (gameClock == null)
        {
            gameClock = FindFirstObjectByType<GameClock>();
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
        if (offers == null || offers.Count == 0)
        {
            offers = BistroBuilderFinancingEngine.CreateDefaultOffers();
        }
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
        liquidityHorizonDays = Math.Max(1, liquidityHorizonDays);
        lossAnalysisWindowDays = Math.Max(1, lossAnalysisWindowDays);
        delinquencyGraceDays = Math.Max(0, delinquencyGraceDays);
        maxActiveLoans = Math.Max(1, maxActiveLoans);
        if (maxOutstandingPrincipalCents < 1L)
        {
            maxOutstandingPrincipalCents = 1L;
        }
        if (offers == null || offers.Count == 0)
        {
            offers = BistroBuilderFinancingEngine.CreateDefaultOffers();
        }
    }
#endif
}
