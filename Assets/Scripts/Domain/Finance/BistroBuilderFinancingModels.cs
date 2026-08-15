using System;
using System.Collections.Generic;

public enum BistroBuilderLoanStatus
{
    Active = 0,
    Delinquent = 1,
    Defaulted = 2,
    PaidOff = 3
}

public enum BistroBuilderLoanInstallmentStatus
{
    Pending = 0,
    Overdue = 1,
    Paid = 2
}

public enum BistroBuilderLiquidityStatus
{
    Healthy = 0,
    Watch = 1,
    Tight = 2,
    Critical = 3,
    Insolvent = 4
}

public enum BistroBuilderFinancialRiskLevel
{
    Low = 0,
    Moderate = 1,
    High = 2,
    Severe = 3
}

[Serializable]
public sealed class BistroBuilderFinancingOfferDefinition
{
    public string offerId = string.Empty;
    public string displayName = string.Empty;
    public long principalCents;
    public int termDays;
    public int totalInterestBasisPoints;
    public int installmentCount;
    public bool active = true;

    public BistroBuilderFinancingOfferDefinition DeepClone()
    {
        return (BistroBuilderFinancingOfferDefinition)MemberwiseClone();
    }
}

[Serializable]
public sealed class BistroBuilderFinancingOfferView
{
    public string offerId = string.Empty;
    public string displayName = string.Empty;
    public long principalCents;
    public int termDays;
    public int totalInterestBasisPoints;
    public long totalInterestCents;
    public long totalPayableCents;
    public int installmentCount;
    public bool eligible;
    public string ineligibilityReason = string.Empty;

    public BistroBuilderFinancingOfferView DeepClone()
    {
        return (BistroBuilderFinancingOfferView)MemberwiseClone();
    }
}

[Serializable]
public sealed class BistroBuilderLoanInstallmentRecord
{
    public int installmentNumber;
    public int dueDayIndex;
    public long principalCents;
    public long interestCents;
    public BistroBuilderLoanInstallmentStatus status;
    public int paidDayIndex;

    public long TotalCents => checked(principalCents + interestCents);

    public BistroBuilderLoanInstallmentRecord DeepClone()
    {
        return (BistroBuilderLoanInstallmentRecord)MemberwiseClone();
    }
}

[Serializable]
public sealed class BistroBuilderLoanRecord
{
    public string loanId = string.Empty;
    public string offerId = string.Empty;
    public string acceptanceOperationId = string.Empty;
    public BistroBuilderLoanStatus status = BistroBuilderLoanStatus.Active;
    public int openedDayIndex = 1;
    public int termDays;
    public int totalInterestBasisPoints;
    public long principalCents;
    public long totalInterestCents;
    public long totalPayableCents;
    public int paidOffDayIndex;
    public List<BistroBuilderLoanInstallmentRecord> installments =
        new List<BistroBuilderLoanInstallmentRecord>();

    public BistroBuilderLoanRecord DeepClone()
    {
        var clone = (BistroBuilderLoanRecord)MemberwiseClone();
        clone.installments = new List<BistroBuilderLoanInstallmentRecord>();
        if (installments != null)
        {
            for (int index = 0; index < installments.Count; index++)
            {
                if (installments[index] != null)
                {
                    clone.installments.Add(installments[index].DeepClone());
                }
            }
        }
        return clone;
    }
}

[Serializable]
public sealed class BistroBuilderFinancingSnapshot
{
    public const string CurrentSchemaId = "finance.financing.runtime";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public long revision = 1L;
    public long nextLoanSequence = 1L;
    public List<BistroBuilderLoanRecord> loans = new List<BistroBuilderLoanRecord>();

    public BistroBuilderFinancingSnapshot DeepClone()
    {
        var clone = new BistroBuilderFinancingSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            revision = revision,
            nextLoanSequence = nextLoanSequence
        };
        if (loans != null)
        {
            for (int index = 0; index < loans.Count; index++)
            {
                if (loans[index] != null)
                {
                    clone.loans.Add(loans[index].DeepClone());
                }
            }
        }
        return clone;
    }
}

[Serializable]
public sealed class BistroBuilderDebtPaymentProcessResult
{
    public int examinedInstallments;
    public int paidInstallments;
    public int newlyOverdueInstallments;
    public int defaultedLoans;
    public long principalPaidCents;
    public long interestPaidCents;
    public long unpaidDueCents;
}

[Serializable]
public sealed class BistroBuilderLiquidityPosition
{
    public int dayIndex;
    public int horizonDays;
    public long cashBalanceCents;
    public bool supplierCommitmentsResolved;
    public long supplierCommittedCents;
    public long availableCashAfterSupplierCommitmentsCents;
    public long debtDueTodayCents;
    public long debtDueWithinHorizonCents;
    public long overdueDebtCents;
    public long outstandingPrincipalCents;
    public long outstandingInterestCents;
    public long projectedLiquidityAfterHorizonObligationsCents;
    public int debtCoverageBasisPoints;
    public BistroBuilderLiquidityStatus status;
}

[Serializable]
public sealed class BistroBuilderFinancialStressSnapshot
{
    public int dayIndex;
    public int analysisWindowDays;
    public long rollingRevenueCents;
    public long rollingOperatingResultCents;
    public int rollingLossDayCount;
    public int consecutiveLossDays;
    public long outstandingDebtCents;
    public long overdueDebtCents;
    public BistroBuilderLiquidityStatus liquidityStatus;
    public BistroBuilderFinancialRiskLevel riskLevel;
}
