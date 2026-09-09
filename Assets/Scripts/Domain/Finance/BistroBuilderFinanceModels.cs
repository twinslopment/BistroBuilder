using System;
using System.Collections.Generic;

public enum BistroBuilderFinanceTransactionKind
{
    Credit = 0,
    Debit = 1
}

[Serializable]
public sealed class BistroBuilderFinanceTransactionRecord
{
    public long sequence;
    public string transactionId;
    public string operationId;
    public string sourceSystemId;
    public string sourceReferenceId;
    public string categoryId;
    public BistroBuilderFinanceTransactionKind kind;
    public long amountCents;
    public int dayIndex;
    public int minuteOfDay;
    public string description;

    public BistroBuilderFinanceTransactionRecord DeepClone()
    {
        return new BistroBuilderFinanceTransactionRecord
        {
            sequence = sequence,
            transactionId = transactionId,
            operationId = operationId,
            sourceSystemId = sourceSystemId,
            sourceReferenceId = sourceReferenceId,
            categoryId = categoryId,
            kind = kind,
            amountCents = amountCents,
            dayIndex = dayIndex,
            minuteOfDay = minuteOfDay,
            description = description
        };
    }
}

[Serializable]
public sealed class BistroBuilderFinanceSnapshot
{
    public const string CurrentSchemaId = "finance.runtime";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public string currencyCode = "EUR";
    public long openingBalanceCents;
    public long currentBalanceCents;
    public long revision = 1L;
    public long nextTransactionSequence = 1L;
    public List<BistroBuilderFinanceTransactionRecord> transactions =
        new List<BistroBuilderFinanceTransactionRecord>();

    public BistroBuilderFinanceSnapshot DeepClone()
    {
        BistroBuilderFinanceSnapshot clone = new BistroBuilderFinanceSnapshot
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            currencyCode = currencyCode,
            openingBalanceCents = openingBalanceCents,
            currentBalanceCents = currentBalanceCents,
            revision = revision,
            nextTransactionSequence = nextTransactionSequence
        };

        if (transactions == null)
        {
            clone.transactions = null;
            return clone;
        }

        for (int index = 0; index < transactions.Count; index++)
        {
            BistroBuilderFinanceTransactionRecord record = transactions[index];
            clone.transactions.Add(record != null ? record.DeepClone() : null);
        }

        return clone;
    }
}

public sealed class BistroBuilderFinanceTransactionRequest
{
    public string operationId;
    public string sourceSystemId;
    public string sourceReferenceId;
    public string categoryId;
    public BistroBuilderFinanceTransactionKind kind;
    public long amountCents;
    public int dayIndex;
    public int minuteOfDay;
    public string description;
}
