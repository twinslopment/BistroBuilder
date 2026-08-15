using System;
using System.Collections.Generic;

/// <summary>
/// Reglas puras de 3I para deuda, cuotas, liquidez y riesgo financiero.
/// No consulta Unity ni publica movimientos por sí mismo.
/// </summary>
public static class BistroBuilderFinancingEngine
{
    public const string SourceSystemId = "financing";
    public const string LoanProceedsCategoryId = "financing.loan_proceeds";
    public const string PrincipalRepaymentCategoryId = "financing.debt_principal";
    public const string InterestExpenseCategoryId = "expense.financing.interest";

    public static List<BistroBuilderFinancingOfferDefinition> CreateDefaultOffers()
    {
        return new List<BistroBuilderFinancingOfferDefinition>
        {
            new BistroBuilderFinancingOfferDefinition
            {
                offerId = "bridge",
                displayName = "Préstamo puente",
                principalCents = 500000L,
                termDays = 30,
                totalInterestBasisPoints = 500,
                installmentCount = 5,
                active = true
            },
            new BistroBuilderFinancingOfferDefinition
            {
                offerId = "growth",
                displayName = "Financiación de crecimiento",
                principalCents = 1500000L,
                termDays = 60,
                totalInterestBasisPoints = 800,
                installmentCount = 6,
                active = true
            },
            new BistroBuilderFinancingOfferDefinition
            {
                offerId = "expansion",
                displayName = "Financiación de expansión",
                principalCents = 3000000L,
                termDays = 90,
                totalInterestBasisPoints = 1200,
                installmentCount = 9,
                active = true
            }
        };
    }

    public static bool TryValidateOffers(
        IReadOnlyList<BistroBuilderFinancingOfferDefinition> offers,
        out string error)
    {
        if (offers == null || offers.Count == 0)
        {
            error = "3I necesita al menos una oferta de financiación.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < offers.Count; index++)
        {
            BistroBuilderFinancingOfferDefinition offer = offers[index];
            if (offer == null ||
                !IsValidStableId(offer.offerId) ||
                string.IsNullOrWhiteSpace(offer.displayName) ||
                offer.principalCents <= 0L ||
                offer.termDays < 1 ||
                offer.totalInterestBasisPoints < 0 ||
                offer.totalInterestBasisPoints > 10000 ||
                offer.installmentCount < 1 ||
                offer.installmentCount > offer.termDays ||
                !ids.Add(offer.offerId))
            {
                error = "La oferta de financiación " + index + " no es válida.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateSnapshot(
        BistroBuilderFinancingSnapshot snapshot,
        out string error)
    {
        if (snapshot == null ||
            !string.Equals(snapshot.schemaId, BistroBuilderFinancingSnapshot.CurrentSchemaId, StringComparison.Ordinal) ||
            snapshot.schemaVersion != BistroBuilderFinancingSnapshot.CurrentSchemaVersion ||
            snapshot.revision < 1L ||
            snapshot.nextLoanSequence < 1L ||
            snapshot.loans == null)
        {
            error = "El snapshot de financiación no es válido.";
            return false;
        }

        var loanIds = new HashSet<string>(StringComparer.Ordinal);
        var acceptanceIds = new HashSet<string>(StringComparer.Ordinal);
        long expectedNext = 1L;

        for (int index = 0; index < snapshot.loans.Count; index++)
        {
            BistroBuilderLoanRecord loan = snapshot.loans[index];
            if (!TryValidateLoan(loan, out error) ||
                !loanIds.Add(loan.loanId) ||
                !acceptanceIds.Add(loan.acceptanceOperationId))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "El snapshot repite identidad de préstamo.";
                }
                return false;
            }

            string expectedLoanId = BuildLoanId(expectedNext);
            if (!string.Equals(loan.loanId, expectedLoanId, StringComparison.Ordinal))
            {
                error = "La secuencia de LoanId no es continua.";
                return false;
            }
            expectedNext++;
        }

        if (snapshot.nextLoanSequence != expectedNext)
        {
            error = "La siguiente secuencia de préstamos no coincide con el snapshot.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryCreateLoan(
        BistroBuilderFinancingSnapshot snapshot,
        BistroBuilderFinancingOfferDefinition offer,
        string acceptanceOperationId,
        int openedDayIndex,
        out BistroBuilderLoanRecord created,
        out string error)
    {
        created = null;
        if (!TryValidateSnapshot(snapshot, out error) ||
            offer == null ||
            !TryValidateOffers(new[] { offer }, out error) ||
            !IsValidStableId(acceptanceOperationId) ||
            openedDayIndex < 1)
        {
            return false;
        }

        for (int index = 0; index < snapshot.loans.Count; index++)
        {
            BistroBuilderLoanRecord existing = snapshot.loans[index];
            if (string.Equals(existing.acceptanceOperationId, acceptanceOperationId, StringComparison.Ordinal))
            {
                if (!string.Equals(existing.offerId, offer.offerId, StringComparison.Ordinal))
                {
                    error = "El OperationId de aceptación ya pertenece a otra oferta.";
                    return false;
                }
                created = existing.DeepClone();
                error = string.Empty;
                return true;
            }
        }

        long sequence = snapshot.nextLoanSequence;
        long totalInterest = CalculateInterestCents(
            offer.principalCents,
            offer.totalInterestBasisPoints);
        long totalPayable = checked(offer.principalCents + totalInterest);

        var loan = new BistroBuilderLoanRecord
        {
            loanId = BuildLoanId(sequence),
            offerId = offer.offerId,
            acceptanceOperationId = acceptanceOperationId,
            status = BistroBuilderLoanStatus.Active,
            openedDayIndex = openedDayIndex,
            termDays = offer.termDays,
            totalInterestBasisPoints = offer.totalInterestBasisPoints,
            principalCents = offer.principalCents,
            totalInterestCents = totalInterest,
            totalPayableCents = totalPayable,
            hasEverDefaulted = false,
            firstDefaultDayIndex = 0
        };

        long principalBase = offer.principalCents / offer.installmentCount;
        long principalRemainder = offer.principalCents % offer.installmentCount;
        long interestBase = totalInterest / offer.installmentCount;
        long interestRemainder = totalInterest % offer.installmentCount;

        for (int index = 0; index < offer.installmentCount; index++)
        {
            int number = index + 1;
            int dueOffset = (int)Math.Ceiling(
                offer.termDays * number / (decimal)offer.installmentCount);
            loan.installments.Add(new BistroBuilderLoanInstallmentRecord
            {
                installmentNumber = number,
                dueDayIndex = checked(openedDayIndex + dueOffset),
                principalCents = principalBase + (index < principalRemainder ? 1L : 0L),
                interestCents = interestBase + (index < interestRemainder ? 1L : 0L),
                status = BistroBuilderLoanInstallmentStatus.Pending,
                paidDayIndex = 0
            });
        }

        snapshot.loans.Add(loan);
        snapshot.nextLoanSequence = checked(sequence + 1L);
        snapshot.revision = checked(snapshot.revision + 1L);
        created = loan.DeepClone();
        error = string.Empty;
        return true;
    }

    public static BistroBuilderFinanceTransactionRequest BuildDisbursementRequest(
        BistroBuilderLoanRecord loan,
        int minuteOfDay)
    {
        return new BistroBuilderFinanceTransactionRequest
        {
            operationId = BuildDisbursementOperationId(loan.loanId),
            sourceSystemId = SourceSystemId,
            sourceReferenceId = loan.loanId,
            categoryId = LoanProceedsCategoryId,
            kind = BistroBuilderFinanceTransactionKind.Credit,
            amountCents = loan.principalCents,
            dayIndex = loan.openedDayIndex,
            minuteOfDay = minuteOfDay,
            description = "Desembolso de financiación " + loan.loanId
        };
    }

    public static void BuildInstallmentRequests(
        BistroBuilderLoanRecord loan,
        BistroBuilderLoanInstallmentRecord installment,
        int paymentDayIndex,
        int minuteOfDay,
        List<BistroBuilderFinanceTransactionRequest> buffer)
    {
        buffer.Clear();
        AppendInstallmentRequests(
            loan,
            installment,
            paymentDayIndex,
            minuteOfDay,
            buffer);
    }

    public static void AppendInstallmentRequests(
        BistroBuilderLoanRecord loan,
        BistroBuilderLoanInstallmentRecord installment,
        int paymentDayIndex,
        int minuteOfDay,
        List<BistroBuilderFinanceTransactionRequest> buffer)
    {
        if (installment.principalCents > 0L)
        {
            buffer.Add(new BistroBuilderFinanceTransactionRequest
            {
                operationId = BuildPrincipalOperationId(loan.loanId, installment.installmentNumber),
                sourceSystemId = SourceSystemId,
                sourceReferenceId = loan.loanId,
                categoryId = PrincipalRepaymentCategoryId,
                kind = BistroBuilderFinanceTransactionKind.Debit,
                amountCents = installment.principalCents,
                dayIndex = paymentDayIndex,
                minuteOfDay = minuteOfDay,
                description = "Principal cuota " + installment.installmentNumber + " de " + loan.loanId
            });
        }
        if (installment.interestCents > 0L)
        {
            buffer.Add(new BistroBuilderFinanceTransactionRequest
            {
                operationId = BuildInterestOperationId(loan.loanId, installment.installmentNumber),
                sourceSystemId = SourceSystemId,
                sourceReferenceId = loan.loanId,
                categoryId = InterestExpenseCategoryId,
                kind = BistroBuilderFinanceTransactionKind.Debit,
                amountCents = installment.interestCents,
                dayIndex = paymentDayIndex,
                minuteOfDay = minuteOfDay,
                description = "Interés cuota " + installment.installmentNumber + " de " + loan.loanId
            });
        }
    }

    public static void RefreshLoanStatuses(
        BistroBuilderFinancingSnapshot snapshot,
        int dayIndex,
        int delinquencyGraceDays)
    {
        for (int loanIndex = 0; loanIndex < snapshot.loans.Count; loanIndex++)
        {
            BistroBuilderLoanRecord loan = snapshot.loans[loanIndex];
            bool allPaid = true;
            bool hasOverdue = false;
            bool defaultedNow = false;

            for (int installmentIndex = 0; installmentIndex < loan.installments.Count; installmentIndex++)
            {
                BistroBuilderLoanInstallmentRecord installment = loan.installments[installmentIndex];
                if (installment.status == BistroBuilderLoanInstallmentStatus.Paid)
                {
                    continue;
                }

                allPaid = false;
                if (installment.dueDayIndex < dayIndex)
                {
                    installment.status = BistroBuilderLoanInstallmentStatus.Overdue;
                    hasOverdue = true;
                    if (dayIndex - installment.dueDayIndex > delinquencyGraceDays)
                    {
                        defaultedNow = true;
                    }
                }
            }

            if (allPaid)
            {
                loan.status = BistroBuilderLoanStatus.PaidOff;
                if (loan.paidOffDayIndex == 0)
                {
                    loan.paidOffDayIndex = dayIndex;
                }
                continue;
            }

            loan.paidOffDayIndex = 0;
            if (defaultedNow)
            {
                if (!loan.hasEverDefaulted)
                {
                    loan.hasEverDefaulted = true;
                    loan.firstDefaultDayIndex = dayIndex;
                }
                loan.status = BistroBuilderLoanStatus.Defaulted;
            }
            else if (loan.hasEverDefaulted)
            {
                // El default es un hecho histórico y permanece visible hasta
                // liquidar por completo la deuda.
                loan.status = BistroBuilderLoanStatus.Defaulted;
            }
            else if (hasOverdue)
            {
                loan.status = BistroBuilderLoanStatus.Delinquent;
            }
            else
            {
                loan.status = BistroBuilderLoanStatus.Active;
            }
        }
    }

    public static void CalculateDebtTotals(
        BistroBuilderFinancingSnapshot snapshot,
        int dayIndex,
        int horizonDays,
        out long dueToday,
        out long dueWithinHorizon,
        out long overdue,
        out long outstandingPrincipal,
        out long outstandingInterest)
    {
        dueToday = 0L;
        dueWithinHorizon = 0L;
        overdue = 0L;
        outstandingPrincipal = 0L;
        outstandingInterest = 0L;
        long horizonEnd = (long)dayIndex + horizonDays;

        for (int loanIndex = 0; loanIndex < snapshot.loans.Count; loanIndex++)
        {
            BistroBuilderLoanRecord loan = snapshot.loans[loanIndex];
            for (int installmentIndex = 0; installmentIndex < loan.installments.Count; installmentIndex++)
            {
                BistroBuilderLoanInstallmentRecord installment = loan.installments[installmentIndex];
                if (installment.status == BistroBuilderLoanInstallmentStatus.Paid)
                {
                    continue;
                }

                long total = checked(installment.principalCents + installment.interestCents);
                outstandingPrincipal = checked(outstandingPrincipal + installment.principalCents);
                outstandingInterest = checked(outstandingInterest + installment.interestCents);

                if (installment.dueDayIndex < dayIndex)
                {
                    overdue = checked(overdue + total);
                }
                if (installment.dueDayIndex == dayIndex)
                {
                    dueToday = checked(dueToday + total);
                }
                if (installment.dueDayIndex <= horizonEnd)
                {
                    dueWithinHorizon = checked(dueWithinHorizon + total);
                }
            }
        }
    }

    public static BistroBuilderLiquidityStatus ResolveLiquidityStatus(
        long cashBalanceCents,
        long availableAfterCommitmentsCents,
        long knownObligationsWithinHorizonCents,
        long overdueDebtCents,
        out int coverageBasisPoints)
    {
        coverageBasisPoints = 0;
        if (cashBalanceCents < 0L)
        {
            return BistroBuilderLiquidityStatus.Insolvent;
        }
        if (overdueDebtCents > 0L || availableAfterCommitmentsCents < 0L)
        {
            return BistroBuilderLiquidityStatus.Critical;
        }
        if (knownObligationsWithinHorizonCents <= 0L)
        {
            coverageBasisPoints = int.MaxValue;
            return BistroBuilderLiquidityStatus.Healthy;
        }

        decimal raw = availableAfterCommitmentsCents * 10000m /
                      knownObligationsWithinHorizonCents;
        if (raw > int.MaxValue)
        {
            coverageBasisPoints = int.MaxValue;
        }
        else if (raw < int.MinValue)
        {
            coverageBasisPoints = int.MinValue;
        }
        else
        {
            coverageBasisPoints = (int)decimal.Round(
                raw,
                0,
                MidpointRounding.AwayFromZero);
        }

        if (coverageBasisPoints >= 20000)
        {
            return BistroBuilderLiquidityStatus.Healthy;
        }
        if (coverageBasisPoints >= 10000)
        {
            return BistroBuilderLiquidityStatus.Watch;
        }
        if (coverageBasisPoints >= 5000)
        {
            return BistroBuilderLiquidityStatus.Tight;
        }
        return BistroBuilderLiquidityStatus.Critical;
    }

    public static BistroBuilderFinancialRiskLevel ResolveRisk(
        BistroBuilderLiquidityStatus liquidityStatus,
        int consecutiveLossDays,
        long rollingOperatingResultCents,
        long outstandingDebtCents,
        bool hasDefaultedLoan)
    {
        if (hasDefaultedLoan ||
            liquidityStatus == BistroBuilderLiquidityStatus.Insolvent ||
            consecutiveLossDays >= 5)
        {
            return BistroBuilderFinancialRiskLevel.Severe;
        }
        if (liquidityStatus == BistroBuilderLiquidityStatus.Unknown ||
            liquidityStatus == BistroBuilderLiquidityStatus.Critical ||
            consecutiveLossDays >= 3 ||
            (rollingOperatingResultCents < 0L && outstandingDebtCents > 0L))
        {
            return BistroBuilderFinancialRiskLevel.High;
        }
        if (liquidityStatus == BistroBuilderLiquidityStatus.Tight ||
            liquidityStatus == BistroBuilderLiquidityStatus.Watch ||
            consecutiveLossDays > 0)
        {
            return BistroBuilderFinancialRiskLevel.Moderate;
        }
        return BistroBuilderFinancialRiskLevel.Low;
    }

    public static long CalculateInterestCents(long principalCents, int basisPoints)
    {
        decimal raw = principalCents * basisPoints / 10000m;
        return (long)decimal.Round(raw, 0, MidpointRounding.AwayFromZero);
    }

    public static string BuildLoanId(long sequence)
    {
        return "loan_" + sequence.ToString("D8");
    }

    public static string BuildDisbursementOperationId(string loanId)
    {
        return "financing_disbursement_" + loanId;
    }

    public static string BuildPrincipalOperationId(string loanId, int installmentNumber)
    {
        return "financing_repayment_" + loanId + "_i" +
               installmentNumber.ToString("D3") + "_principal";
    }

    public static string BuildInterestOperationId(string loanId, int installmentNumber)
    {
        return "financing_repayment_" + loanId + "_i" +
               installmentNumber.ToString("D3") + "_interest";
    }

    private static bool TryValidateLoan(BistroBuilderLoanRecord loan, out string error)
    {
        if (loan == null ||
            !IsValidStableId(loan.loanId) ||
            !IsValidStableId(loan.offerId) ||
            !IsValidStableId(loan.acceptanceOperationId) ||
            !Enum.IsDefined(typeof(BistroBuilderLoanStatus), loan.status) ||
            loan.openedDayIndex < 1 ||
            loan.termDays < 1 ||
            loan.totalInterestBasisPoints < 0 ||
            loan.principalCents <= 0L ||
            loan.totalInterestCents < 0L ||
            loan.totalPayableCents != checked(loan.principalCents + loan.totalInterestCents) ||
            loan.installments == null ||
            loan.installments.Count == 0 ||
            loan.firstDefaultDayIndex < 0 ||
            (loan.hasEverDefaulted && loan.firstDefaultDayIndex < loan.openedDayIndex) ||
            (!loan.hasEverDefaulted && loan.firstDefaultDayIndex != 0) ||
            (loan.status == BistroBuilderLoanStatus.PaidOff && loan.paidOffDayIndex < 1) ||
            (loan.status != BistroBuilderLoanStatus.PaidOff && loan.paidOffDayIndex != 0))
        {
            error = "El préstamo contiene datos base no válidos.";
            return false;
        }

        long principal = 0L;
        long interest = 0L;
        int previousDueDay = loan.openedDayIndex;
        for (int index = 0; index < loan.installments.Count; index++)
        {
            BistroBuilderLoanInstallmentRecord installment = loan.installments[index];
            if (installment == null ||
                installment.installmentNumber != index + 1 ||
                installment.dueDayIndex <= previousDueDay ||
                installment.principalCents <= 0L ||
                installment.interestCents < 0L ||
                !Enum.IsDefined(typeof(BistroBuilderLoanInstallmentStatus), installment.status) ||
                (installment.status == BistroBuilderLoanInstallmentStatus.Paid && installment.paidDayIndex < 1) ||
                (installment.status != BistroBuilderLoanInstallmentStatus.Paid && installment.paidDayIndex != 0))
            {
                error = "El préstamo contiene una cuota no válida.";
                return false;
            }

            previousDueDay = installment.dueDayIndex;
            principal = checked(principal + installment.principalCents);
            interest = checked(interest + installment.interestCents);
        }

        if (principal != loan.principalCents || interest != loan.totalInterestCents)
        {
            error = "Las cuotas no cuadran con principal e intereses del préstamo.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsValidStableId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        string normalized = value.Trim().ToLowerInvariant();
        if (!string.Equals(value, normalized, StringComparison.Ordinal) ||
            normalized.Length > 128)
        {
            return false;
        }
        for (int index = 0; index < normalized.Length; index++)
        {
            char c = normalized[index];
            bool ok = c >= 'a' && c <= 'z' ||
                      c >= '0' && c <= '9' ||
                      c == '_' || c == '-' || c == '.';
            if (!ok)
            {
                return false;
            }
        }
        return true;
    }
}
