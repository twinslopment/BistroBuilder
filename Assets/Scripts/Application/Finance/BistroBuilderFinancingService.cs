using System;
using System.Collections;
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
    [SerializeField] private BistroBuilderOperatingExpenseService operatingExpenseService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private GameClock gameClock;
    [SerializeField] private BistroBuilderSaveGameService saveGameService;

    [SerializeField, Min(1)] private int liquidityHorizonDays = 7;
    [SerializeField, Min(1)] private int lossAnalysisWindowDays = 7;
    [SerializeField, Min(0)] private int delinquencyGraceDays = 7;
    [SerializeField, Min(1)] private int maxActiveLoans = 3;
    [SerializeField, Min(0)] private long maxOutstandingPrincipalCents = 5000000L;
    [SerializeField] private List<BistroBuilderFinancingOfferDefinition> offers =
        BistroBuilderFinancingEngine.CreateDefaultOffers();

    private readonly List<BistroBuilderFinanceTransactionRequest> requestBuffer =
        new List<BistroBuilderFinanceTransactionRequest>(16);
    private BistroBuilderFinancingSnapshot state;

    public event Action FinancingChanged;
    public event Action<BistroBuilderLoanRecord> LoanOpened;
    public event Action<BistroBuilderDebtPaymentProcessResult> DebtPaymentsProcessed;

    public bool IsInitialized => state != null;
    public BistroBuilderFinanceService FinanceService => financeService;
    public BistroBuilderSupplierPurchaseFinanceBridge SupplierFinanceBridge => supplierFinanceBridge;
    public BistroBuilderFinancialHistoryService FinancialHistoryService => financialHistoryService;
    public BistroBuilderOperatingExpenseService OperatingExpenseService => operatingExpenseService;
    public BistroBuilderGeneralGameStateService GeneralGameStateService => generalGameStateService;
    public BistroBuilderSaveGameService SaveGameService => saveGameService;
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
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();
        if (financeService == null || supplierFinanceBridge == null ||
            financialHistoryService == null || operatingExpenseService == null ||
            generalGameStateService == null || gameClock == null ||
            saveGameService == null)
        {
            error = "3I necesita Finanzas 3A, compromisos 3C, gastos 3E, Históricos 3H, calendario, reloj y SaveGame.";
            return false;
        }

        if (!financeService.ValidateConfiguration(out error) ||
            !supplierFinanceBridge.ValidateConfiguration(out error) ||
            !financialHistoryService.ValidateConfiguration(out error) ||
            !operatingExpenseService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (!ReferenceEquals(
                financialHistoryService.GeneralGameStateService,
                generalGameStateService))
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

    public bool TryRestoreSnapshot(
        BistroBuilderFinancingSnapshot candidate,
        out string error)
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
            !TryGetLiquidityPosition(
                out BistroBuilderLiquidityPosition liquidity,
                out error))
        {
            return false;
        }

        CountDebtState(
            out int activeLoans,
            out long outstandingPrincipal,
            out bool hasDefaultedLoan);
        bool hasOverdue = liquidity.overdueDebtCents > 0L;

        int consecutiveLossDays = 0;
        if (!TryGetCompletedLossMetrics(
                out _,
                out _,
                out consecutiveLossDays,
                out error))
        {
            return false;
        }

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

            if (!liquidity.projectionComplete)
            {
                eligible = false;
                reason = "La proyección de liquidez está incompleta.";
            }
            else if (hasDefaultedLoan || hasOverdue)
            {
                eligible = false;
                reason = "Hay deuda vencida o impagada.";
            }
            else if (consecutiveLossDays >= 5)
            {
                eligible = false;
                reason = "Las pérdidas consecutivas impiden asumir nueva deuda.";
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
                    projectedPrincipal = checked(
                        outstandingPrincipal + offer.principalCents);
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
                else if (liquidity.cashBalanceCents < 0L &&
                         offer.principalCents <= -liquidity.cashBalanceCents)
                {
                    eligible = false;
                    reason = "La financiación no corrige el déficit actual de caja.";
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
            if (string.Equals(
                    existing.acceptanceOperationId,
                    acceptanceOperationId,
                    StringComparison.Ordinal))
            {
                if (!string.Equals(
                        existing.offerId,
                        offer.offerId,
                        StringComparison.Ordinal))
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
            candidate => string.Equals(
                candidate.offerId,
                offer.offerId,
                StringComparison.Ordinal));
        if (view == null || !view.eligible)
        {
            error = view != null
                ? view.ineligibilityReason
                : "La oferta no es financiable.";
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
                CurrentMinuteOfDay);
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

    /// <summary>
    /// Procesa todas las cuotas vencidas del corte indicado. Todas las patas
    /// monetarias nuevas se publican en un único batch atómico. Si el ledger
    /// ya contiene una cuota completa por una interrupción anterior, 3I la
    /// reconcilia de forma idempotente; una media cuota huérfana es error.
    /// </summary>
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

        if (saveGameService != null && saveGameService.IsBusy)
        {
            error = "3I no procesa vencimientos durante una operación de Save/Load.";
            return false;
        }

        BistroBuilderFinancingSnapshot candidate = state.DeepClone();
        bool changed = false;
        long remainingCash = financeService.CurrentBalanceCents;
        requestBuffer.Clear();

        for (int loanIndex = 0; loanIndex < candidate.loans.Count; loanIndex++)
        {
            BistroBuilderLoanRecord loan = candidate.loans[loanIndex];
            for (int installmentIndex = 0;
                 installmentIndex < loan.installments.Count;
                 installmentIndex++)
            {
                BistroBuilderLoanInstallmentRecord installment =
                    loan.installments[installmentIndex];
                if (installment.status == BistroBuilderLoanInstallmentStatus.Paid ||
                    installment.dueDayIndex > dayIndex)
                {
                    continue;
                }

                result.examinedInstallments++;

                if (!TryResolveExistingInstallmentPayment(
                        loan,
                        installment,
                        out bool alreadyPosted,
                        out int postedDayIndex,
                        out error))
                {
                    requestBuffer.Clear();
                    return false;
                }

                if (alreadyPosted)
                {
                    installment.status = BistroBuilderLoanInstallmentStatus.Paid;
                    installment.paidDayIndex = postedDayIndex;
                    result.paidInstallments++;
                    result.principalPaidCents = checked(
                        result.principalPaidCents + installment.principalCents);
                    result.interestPaidCents = checked(
                        result.interestPaidCents + installment.interestCents);
                    changed = true;
                    continue;
                }

                long installmentTotal = checked(
                    installment.principalCents + installment.interestCents);
                if (remainingCash < installmentTotal)
                {
                    result.unpaidDueCents = checked(
                        result.unpaidDueCents + installmentTotal);
                    if (installment.status !=
                        BistroBuilderLoanInstallmentStatus.Overdue)
                    {
                        installment.status =
                            BistroBuilderLoanInstallmentStatus.Overdue;
                        result.newlyOverdueInstallments++;
                        changed = true;
                    }
                    continue;
                }

                BistroBuilderFinancingEngine.AppendInstallmentRequests(
                    loan,
                    installment,
                    dayIndex,
                    CurrentMinuteOfDay,
                    requestBuffer);
                remainingCash = checked(remainingCash - installmentTotal);
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

        if (requestBuffer.Count > 0 &&
            !financeService.TryPostTransactions(requestBuffer, out _, out error))
        {
            requestBuffer.Clear();
            return false;
        }
        requestBuffer.Clear();

        int defaultedBefore = CountDefaulted(state);
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
        BistroBuilderFinancingEngine.RefreshLoanStatuses(
            snapshot,
            dayIndex,
            delinquencyGraceDays);
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

        int horizonEnd;
        long horizonEndLong = (long)dayIndex + liquidityHorizonDays;
        horizonEnd = horizonEndLong > int.MaxValue
            ? int.MaxValue
            : (int)horizonEndLong;
        bool recurringResolved =
            operatingExpenseService.TryCalculateRecurringObligationsCents(
                dayIndex,
                horizonEnd,
                out long recurringOperating,
                out _);

        long available = commitmentsResolved
            ? checked(financeService.CurrentBalanceCents - committed)
            : financeService.CurrentBalanceCents;
        long knownObligations = checked(dueHorizon + recurringOperating);
        long projected = checked(available - knownObligations);
        bool complete = commitmentsResolved && recurringResolved;

        int knownCoverage = 0;
        BistroBuilderLiquidityStatus status;
        if (!complete)
        {
            status = BistroBuilderLiquidityStatus.Unknown;
        }
        else
        {
            status = BistroBuilderFinancingEngine.ResolveLiquidityStatus(
                financeService.CurrentBalanceCents,
                available,
                knownObligations,
                overdue,
                out knownCoverage);
        }

        int debtCoverage = CalculateCoverageBasisPoints(
            available,
            dueHorizon);

        position = new BistroBuilderLiquidityPosition
        {
            dayIndex = dayIndex,
            horizonDays = liquidityHorizonDays,
            cashBalanceCents = financeService.CurrentBalanceCents,
            supplierCommitmentsResolved = commitmentsResolved,
            supplierCommittedCents = commitmentsResolved ? committed : 0L,
            availableCashAfterSupplierCommitmentsCents = available,
            recurringOperatingObligationsResolved = recurringResolved,
            recurringOperatingObligationsWithinHorizonCents =
                recurringResolved ? recurringOperating : 0L,
            debtDueTodayCents = dueToday,
            debtDueWithinHorizonCents = dueHorizon,
            overdueDebtCents = overdue,
            outstandingPrincipalCents = outstandingPrincipal,
            outstandingInterestCents = outstandingInterest,
            totalKnownHorizonObligationsCents = knownObligations,
            projectedLiquidityAfterHorizonObligationsCents = projected,
            debtCoverageBasisPoints = debtCoverage,
            knownObligationCoverageBasisPoints = knownCoverage,
            projectionComplete = complete,
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
        if (!TryGetLiquidityPosition(
                out BistroBuilderLiquidityPosition liquidity,
                out error) ||
            !TryGetCompletedLossMetrics(
                out BistroBuilderFinancialPeriodReport report,
                out int rollingLossDayCount,
                out int consecutiveLossDays,
                out error))
        {
            return false;
        }

        bool hasDefaultedLoan = false;
        for (int index = 0; index < state.loans.Count; index++)
        {
            BistroBuilderLoanRecord loan = state.loans[index];
            if ((loan.hasEverDefaulted ||
                 loan.status == BistroBuilderLoanStatus.Defaulted) &&
                loan.status != BistroBuilderLoanStatus.PaidOff)
            {
                hasDefaultedLoan = true;
                break;
            }
        }

        long outstandingDebt = checked(
            liquidity.outstandingPrincipalCents +
            liquidity.outstandingInterestCents);
        long rollingRevenue = report != null ? report.revenueCents : 0L;
        long rollingResult = report != null ? report.operatingResultCents : 0L;
        int analysisDays = report != null ? report.dayCount : 0;

        stress = new BistroBuilderFinancialStressSnapshot
        {
            dayIndex = generalGameStateService.DayIndex,
            analysisWindowDays = analysisDays,
            rollingRevenueCents = rollingRevenue,
            rollingOperatingResultCents = rollingResult,
            rollingLossDayCount = rollingLossDayCount,
            consecutiveLossDays = consecutiveLossDays,
            outstandingDebtCents = outstandingDebt,
            overdueDebtCents = liquidity.overdueDebtCents,
            liquidityStatus = liquidity.status,
            riskLevel = BistroBuilderFinancingEngine.ResolveRisk(
                liquidity.status,
                consecutiveLossDays,
                rollingResult,
                outstandingDebt,
                hasDefaultedLoan)
        };
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Auditoría bidireccional: cada estado de deuda debe tener exactamente
    /// sus movimientos esperados y ningún movimiento source=financing puede
    /// quedar huérfano en el ledger.
    /// </summary>
    public bool TryValidateLedgerConsistency(out string error)
    {
        if (!EnsureInitialized(out error))
        {
            return false;
        }

        BistroBuilderFinanceSnapshot financeSnapshot =
            financeService.CreateSnapshot();
        if (!BistroBuilderFinanceEngine.TryValidateSnapshot(
                financeSnapshot,
                out error))
        {
            return false;
        }

        var expectedOperations = new HashSet<string>(StringComparer.Ordinal);

        for (int loanIndex = 0; loanIndex < state.loans.Count; loanIndex++)
        {
            BistroBuilderLoanRecord loan = state.loans[loanIndex];
            string disbursementOperation =
                BistroBuilderFinancingEngine.BuildDisbursementOperationId(
                    loan.loanId);
            expectedOperations.Add(disbursementOperation);

            if (!financeService.TryGetTransactionByOperationId(
                    disbursementOperation,
                    out BistroBuilderFinanceTransactionRecord disbursement) ||
                !IsExactFinancingTransaction(
                    disbursement,
                    loan.loanId,
                    BistroBuilderFinancingEngine.LoanProceedsCategoryId,
                    BistroBuilderFinanceTransactionKind.Credit,
                    loan.principalCents))
            {
                error = "El préstamo " + loan.loanId +
                        " no tiene un desembolso financiero coherente.";
                return false;
            }

            for (int installmentIndex = 0;
                 installmentIndex < loan.installments.Count;
                 installmentIndex++)
            {
                BistroBuilderLoanInstallmentRecord installment =
                    loan.installments[installmentIndex];
                string principalOperation =
                    BistroBuilderFinancingEngine.BuildPrincipalOperationId(
                        loan.loanId,
                        installment.installmentNumber);
                string interestOperation =
                    BistroBuilderFinancingEngine.BuildInterestOperationId(
                        loan.loanId,
                        installment.installmentNumber);

                bool hasPrincipal = financeService.TryGetTransactionByOperationId(
                    principalOperation,
                    out BistroBuilderFinanceTransactionRecord principalTx);
                bool hasInterest = installment.interestCents > 0L &&
                    financeService.TryGetTransactionByOperationId(
                        interestOperation,
                        out BistroBuilderFinanceTransactionRecord interestTx);

                if (installment.status == BistroBuilderLoanInstallmentStatus.Paid)
                {
                    expectedOperations.Add(principalOperation);
                    if (!hasPrincipal ||
                        !IsExactFinancingTransaction(
                            principalTx,
                            loan.loanId,
                            BistroBuilderFinancingEngine.PrincipalRepaymentCategoryId,
                            BistroBuilderFinanceTransactionKind.Debit,
                            installment.principalCents))
                    {
                        error = "Falta o es incoherente el principal de una cuota pagada.";
                        return false;
                    }

                    if (installment.interestCents > 0L)
                    {
                        expectedOperations.Add(interestOperation);
                        if (!hasInterest ||
                            !IsExactFinancingTransaction(
                                interestTx,
                                loan.loanId,
                                BistroBuilderFinancingEngine.InterestExpenseCategoryId,
                                BistroBuilderFinanceTransactionKind.Debit,
                                installment.interestCents) ||
                            interestTx.dayIndex != principalTx.dayIndex)
                        {
                            error = "Falta o es incoherente el interés de una cuota pagada.";
                            return false;
                        }
                    }
                }
                else if (hasPrincipal || hasInterest)
                {
                    error = "El ledger contiene un pago de cuota que 3I no marca como pagado.";
                    return false;
                }
            }
        }

        for (int index = 0; index < financeSnapshot.transactions.Count; index++)
        {
            BistroBuilderFinanceTransactionRecord transaction =
                financeSnapshot.transactions[index];
            if (transaction != null &&
                string.Equals(
                    transaction.sourceSystemId,
                    BistroBuilderFinancingEngine.SourceSystemId,
                    StringComparison.Ordinal) &&
                !expectedOperations.Contains(transaction.operationId))
            {
                error = "El ledger contiene un movimiento de financiación huérfano: " +
                        transaction.operationId + ".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private bool TryResolveExistingInstallmentPayment(
        BistroBuilderLoanRecord loan,
        BistroBuilderLoanInstallmentRecord installment,
        out bool alreadyPosted,
        out int paymentDayIndex,
        out string error)
    {
        alreadyPosted = false;
        paymentDayIndex = 0;

        bool hasPrincipal = financeService.TryGetTransactionByOperationId(
            BistroBuilderFinancingEngine.BuildPrincipalOperationId(
                loan.loanId,
                installment.installmentNumber),
            out BistroBuilderFinanceTransactionRecord principalTx);
        bool interestRequired = installment.interestCents > 0L;
        bool hasInterest = interestRequired &&
            financeService.TryGetTransactionByOperationId(
                BistroBuilderFinancingEngine.BuildInterestOperationId(
                    loan.loanId,
                    installment.installmentNumber),
                out BistroBuilderFinanceTransactionRecord interestTx);

        if (!hasPrincipal && !hasInterest)
        {
            error = string.Empty;
            return true;
        }

        if (!hasPrincipal || (interestRequired && !hasInterest))
        {
            error = "El ledger contiene una cuota de financiación parcialmente publicada.";
            return false;
        }

        if (!IsExactFinancingTransaction(
                principalTx,
                loan.loanId,
                BistroBuilderFinancingEngine.PrincipalRepaymentCategoryId,
                BistroBuilderFinanceTransactionKind.Debit,
                installment.principalCents))
        {
            error = "El principal ya publicado no coincide con la cuota de 3I.";
            return false;
        }

        if (interestRequired &&
            (!IsExactFinancingTransaction(
                interestTx,
                loan.loanId,
                BistroBuilderFinancingEngine.InterestExpenseCategoryId,
                BistroBuilderFinanceTransactionKind.Debit,
                installment.interestCents) ||
             interestTx.dayIndex != principalTx.dayIndex))
        {
            error = "El interés ya publicado no coincide con la cuota de 3I.";
            return false;
        }

        alreadyPosted = true;
        paymentDayIndex = principalTx.dayIndex;
        error = string.Empty;
        return true;
    }

    private static bool IsExactFinancingTransaction(
        BistroBuilderFinanceTransactionRecord transaction,
        string loanId,
        string categoryId,
        BistroBuilderFinanceTransactionKind kind,
        long amountCents)
    {
        return transaction != null &&
               transaction.kind == kind &&
               transaction.amountCents == amountCents &&
               string.Equals(
                   transaction.sourceSystemId,
                   BistroBuilderFinancingEngine.SourceSystemId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   transaction.sourceReferenceId,
                   loanId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   transaction.categoryId,
                   categoryId,
                   StringComparison.Ordinal);
    }

    private bool TryGetCompletedLossMetrics(
        out BistroBuilderFinancialPeriodReport report,
        out int rollingLossDayCount,
        out int consecutiveLossDays,
        out string error)
    {
        report = null;
        rollingLossDayCount = 0;
        consecutiveLossDays = 0;

        if (generalGameStateService.DayIndex <= 1)
        {
            error = string.Empty;
            return true;
        }

        if (!financialHistoryService.TryGetCompletedRollingReport(
                lossAnalysisWindowDays,
                out report,
                out error))
        {
            return false;
        }

        rollingLossDayCount = report.lossDayCount;
        if (report.dailyResults != null)
        {
            for (int index = report.dailyResults.Count - 1; index >= 0; index--)
            {
                BistroBuilderDayFinancialResult day = report.dailyResults[index];
                if (day == null || !day.HasOperatingResultActivity)
                {
                    continue;
                }

                if (day.operatingResultCents < 0L)
                {
                    consecutiveLossDays++;
                    continue;
                }
                break;
            }
        }

        error = string.Empty;
        return true;
    }

    private void HandleCalendarChanged()
    {
        if (state == null ||
            (saveGameService != null && saveGameService.IsBusy))
        {
            return;
        }

        if (!TryProcessDuePayments(
                generalGameStateService.DayIndex,
                out _,
                out string error))
        {
            Debug.LogError(
                "3I no pudo procesar vencimientos del nuevo día. " + error,
                this);
        }
    }

    private void HandleSaveOperationCompleted(
        BistroBuilderSaveOperationResult result)
    {
        if (result == null ||
            !result.Succeeded ||
            result.OperationKind != BistroBuilderSaveOperationKind.Load)
        {
            return;
        }

        StartCoroutine(ReconcileAfterLoad());
    }

    private IEnumerator ReconcileAfterLoad()
    {
        while (saveGameService != null && saveGameService.IsBusy)
        {
            yield return null;
        }

        if (!TryProcessDuePayments(
                generalGameStateService.DayIndex,
                out _,
                out string paymentError))
        {
            Debug.LogError(
                "3I no pudo reconciliar vencimientos después de Load. " +
                paymentError,
                this);
            yield break;
        }

        if (!TryValidateLedgerConsistency(out string consistencyError))
        {
            Debug.LogError(
                "3I detectó inconsistencia deuda/ledger después de Load. " +
                consistencyError,
                this);
        }
    }

    private void Subscribe()
    {
        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleCalendarChanged;
            generalGameStateService.CalendarChanged += HandleCalendarChanged;
        }
        if (saveGameService != null)
        {
            saveGameService.OperationCompleted -= HandleSaveOperationCompleted;
            saveGameService.OperationCompleted += HandleSaveOperationCompleted;
        }
    }

    private void Unsubscribe()
    {
        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -= HandleCalendarChanged;
        }
        if (saveGameService != null)
        {
            saveGameService.OperationCompleted -= HandleSaveOperationCompleted;
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
            if (offer != null &&
                string.Equals(
                    offer.offerId,
                    normalized,
                    StringComparison.Ordinal))
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
            if ((loan.hasEverDefaulted ||
                 loan.status == BistroBuilderLoanStatus.Defaulted) &&
                loan.status != BistroBuilderLoanStatus.PaidOff)
            {
                hasDefaultedLoan = true;
            }
            for (int installmentIndex = 0;
                 installmentIndex < loan.installments.Count;
                 installmentIndex++)
            {
                BistroBuilderLoanInstallmentRecord installment =
                    loan.installments[installmentIndex];
                if (installment.status != BistroBuilderLoanInstallmentStatus.Paid)
                {
                    outstandingPrincipal = checked(
                        outstandingPrincipal + installment.principalCents);
                }
            }
        }
    }

    private static int CountDefaulted(BistroBuilderFinancingSnapshot snapshot)
    {
        int count = 0;
        for (int index = 0; index < snapshot.loans.Count; index++)
        {
            BistroBuilderLoanRecord loan = snapshot.loans[index];
            if (loan.status == BistroBuilderLoanStatus.Defaulted &&
                loan.status != BistroBuilderLoanStatus.PaidOff)
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
            if (left.status != right.status ||
                left.paidOffDayIndex != right.paidOffDayIndex ||
                left.hasEverDefaulted != right.hasEverDefaulted ||
                left.firstDefaultDayIndex != right.firstDefaultDayIndex)
            {
                return false;
            }
            for (int installmentIndex = 0;
                 installmentIndex < left.installments.Count;
                 installmentIndex++)
            {
                BistroBuilderLoanInstallmentRecord li =
                    left.installments[installmentIndex];
                BistroBuilderLoanInstallmentRecord ri =
                    right.installments[installmentIndex];
                if (li.status != ri.status ||
                    li.paidDayIndex != ri.paidDayIndex)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static int CalculateCoverageBasisPoints(
        long availableCents,
        long obligationsCents)
    {
        if (obligationsCents <= 0L)
        {
            return int.MaxValue;
        }
        decimal raw = (decimal)availableCents * 10000m / obligationsCents;
        if (raw > int.MaxValue)
        {
            return int.MaxValue;
        }
        if (raw < int.MinValue)
        {
            return int.MinValue;
        }
        return (int)decimal.Round(
            raw,
            0,
            MidpointRounding.AwayFromZero);
    }

    private int CurrentMinuteOfDay =>
        Mathf.Clamp(gameClock.Hour, 0, 23) * 60 +
        Mathf.Clamp(gameClock.Minute, 0, 59);

    private void CacheDependenciesIfNeeded()
    {
        if (financeService == null)
        {
            financeService = FindFirstObjectByType<BistroBuilderFinanceService>();
        }
        if (supplierFinanceBridge == null)
        {
            supplierFinanceBridge =
                FindFirstObjectByType<BistroBuilderSupplierPurchaseFinanceBridge>();
        }
        if (financialHistoryService == null)
        {
            financialHistoryService =
                FindFirstObjectByType<BistroBuilderFinancialHistoryService>();
        }
        if (operatingExpenseService == null)
        {
            operatingExpenseService =
                FindFirstObjectByType<BistroBuilderOperatingExpenseService>();
        }
        if (generalGameStateService == null)
        {
            generalGameStateService =
                FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        }
        if (gameClock == null)
        {
            gameClock = FindFirstObjectByType<GameClock>();
        }
        if (saveGameService == null)
        {
            saveGameService = FindFirstObjectByType<BistroBuilderSaveGameService>();
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
