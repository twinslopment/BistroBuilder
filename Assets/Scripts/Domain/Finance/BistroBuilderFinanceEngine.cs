using System;
using System.Collections.Generic;

/// <summary>
/// Reglas puras del libro financiero 3A. No conoce Unity, UI ni sistemas productores.
/// </summary>
public static class BistroBuilderFinanceEngine
{
    private const int MaximumStableIdLength = 128;
    private const int MaximumDescriptionLength = 240;

    public static BistroBuilderFinanceSnapshot CreateInitialSnapshot(
        long openingBalanceCents,
        string currencyCode)
    {
        return new BistroBuilderFinanceSnapshot
        {
            currencyCode = NormalizeCurrency(currencyCode),
            openingBalanceCents = openingBalanceCents,
            currentBalanceCents = openingBalanceCents,
            revision = 1L,
            nextTransactionSequence = 1L
        };
    }

    public static bool TryAppendNewTransaction(
        BistroBuilderFinanceSnapshot snapshot,
        BistroBuilderFinanceTransactionRequest request,
        out BistroBuilderFinanceTransactionRecord posted,
        out string error)
    {
        posted = null;

        if (!TryValidateWritableState(snapshot, out error) ||
            !TryValidateRequest(request, out error))
        {
            return false;
        }

        long sequence = snapshot.nextTransactionSequence;
        long newBalance;
        long newNextSequence;
        long newRevision;

        try
        {
            newBalance = request.kind == BistroBuilderFinanceTransactionKind.Credit
                ? checked(snapshot.currentBalanceCents + request.amountCents)
                : checked(snapshot.currentBalanceCents - request.amountCents);
            newNextSequence = checked(sequence + 1L);
            newRevision = checked(snapshot.revision + 1L);
        }
        catch (OverflowException)
        {
            error = "La operación desborda el rango monetario o de secuencias soportado.";
            return false;
        }

        BistroBuilderFinanceTransactionRecord record =
            new BistroBuilderFinanceTransactionRecord
            {
                sequence = sequence,
                transactionId = BuildTransactionId(sequence),
                operationId = NormalizeStableId(request.operationId),
                sourceSystemId = NormalizeStableId(request.sourceSystemId),
                sourceReferenceId = NormalizeStableId(request.sourceReferenceId),
                categoryId = NormalizeStableId(request.categoryId),
                kind = request.kind,
                amountCents = request.amountCents,
                dayIndex = request.dayIndex,
                minuteOfDay = request.minuteOfDay,
                description = NormalizeDescription(request.description)
            };

        snapshot.transactions.Add(record);
        snapshot.currentBalanceCents = newBalance;
        snapshot.nextTransactionSequence = newNextSequence;
        snapshot.revision = newRevision;
        posted = record.DeepClone();
        return true;
    }

    public static bool TryValidateRequest(
        BistroBuilderFinanceTransactionRequest request,
        out string error)
    {
        error = string.Empty;

        if (request == null)
        {
            error = "La operación financiera es nula.";
            return false;
        }

        if (!IsValidStableId(request.operationId) ||
            !IsValidStableId(request.sourceSystemId) ||
            !IsValidStableId(request.sourceReferenceId) ||
            !IsValidStableId(request.categoryId))
        {
            error = "La operación financiera contiene una identidad no válida.";
            return false;
        }

        if (!Enum.IsDefined(typeof(BistroBuilderFinanceTransactionKind), request.kind))
        {
            error = "El tipo de movimiento financiero no es válido.";
            return false;
        }

        if (request.amountCents <= 0L)
        {
            error = "El importe financiero debe ser positivo.";
            return false;
        }

        if (request.dayIndex < 1 || request.minuteOfDay < 0 || request.minuteOfDay > 1439)
        {
            error = "La fecha de juego de la operación financiera no es válida.";
            return false;
        }

        if ((request.description ?? string.Empty).Trim().Length > MaximumDescriptionLength)
        {
            error = "La descripción financiera supera la longitud permitida.";
            return false;
        }

        return true;
    }

    public static bool IsEquivalent(
        BistroBuilderFinanceTransactionRecord record,
        BistroBuilderFinanceTransactionRequest request)
    {
        return record != null && request != null &&
               string.Equals(record.operationId, NormalizeStableId(request.operationId), StringComparison.Ordinal) &&
               string.Equals(record.sourceSystemId, NormalizeStableId(request.sourceSystemId), StringComparison.Ordinal) &&
               string.Equals(record.sourceReferenceId, NormalizeStableId(request.sourceReferenceId), StringComparison.Ordinal) &&
               string.Equals(record.categoryId, NormalizeStableId(request.categoryId), StringComparison.Ordinal) &&
               record.kind == request.kind &&
               record.amountCents == request.amountCents &&
               record.dayIndex == request.dayIndex &&
               record.minuteOfDay == request.minuteOfDay &&
               string.Equals(record.description, NormalizeDescription(request.description), StringComparison.Ordinal);
    }

    public static string NormalizeOperationId(string value)
    {
        return NormalizeStableId(value);
    }

    public static bool TryValidateSnapshot(
        BistroBuilderFinanceSnapshot snapshot,
        out string error)
    {
        if (!TryValidateWritableState(snapshot, out error))
        {
            return false;
        }

        if (snapshot.openingBalanceCents < 0L)
        {
            error = "El saldo inicial del snapshot financiero no es válido.";
            return false;
        }

        HashSet<string> transactionIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> operationIds = new HashSet<string>(StringComparer.Ordinal);
        long expectedSequence = 1L;
        long calculatedBalance = snapshot.openingBalanceCents;

        for (int index = 0; index < snapshot.transactions.Count; index++)
        {
            BistroBuilderFinanceTransactionRecord record = snapshot.transactions[index];
            if (!TryValidateRecord(record, expectedSequence, out error))
            {
                return false;
            }

            if (!transactionIds.Add(record.transactionId))
            {
                error = "El ledger financiero repite TransactionId.";
                return false;
            }

            if (!operationIds.Add(record.operationId))
            {
                error = "El ledger financiero repite OperationId.";
                return false;
            }

            try
            {
                calculatedBalance = record.kind == BistroBuilderFinanceTransactionKind.Credit
                    ? checked(calculatedBalance + record.amountCents)
                    : checked(calculatedBalance - record.amountCents);
            }
            catch (OverflowException)
            {
                error = "El ledger financiero desborda el rango monetario soportado.";
                return false;
            }

            expectedSequence++;
        }

        if (snapshot.nextTransactionSequence != expectedSequence)
        {
            error = "La siguiente secuencia financiera no es continua.";
            return false;
        }

        if (snapshot.revision != expectedSequence)
        {
            error = "La revisión financiera no coincide con el número de movimientos.";
            return false;
        }

        if (snapshot.currentBalanceCents != calculatedBalance)
        {
            error = "El saldo financiero no coincide con el ledger.";
            return false;
        }

        return true;
    }

    private static bool TryValidateWritableState(
        BistroBuilderFinanceSnapshot snapshot,
        out string error)
    {
        error = string.Empty;

        if (snapshot == null)
        {
            error = "El snapshot financiero es nulo.";
            return false;
        }

        if (!string.Equals(snapshot.schemaId, BistroBuilderFinanceSnapshot.CurrentSchemaId, StringComparison.Ordinal) ||
            snapshot.schemaVersion != BistroBuilderFinanceSnapshot.CurrentSchemaVersion)
        {
            error = "El esquema de finance.runtime no es compatible.";
            return false;
        }

        string normalizedCurrency = NormalizeCurrency(snapshot.currencyCode);
        if (!IsValidCurrency(normalizedCurrency) ||
            !string.Equals(snapshot.currencyCode, normalizedCurrency, StringComparison.Ordinal))
        {
            error = "La moneda del snapshot financiero no es válida.";
            return false;
        }

        if (snapshot.revision < 1L ||
            snapshot.nextTransactionSequence < 1L ||
            snapshot.transactions == null)
        {
            error = "El estado base del snapshot financiero no es válido.";
            return false;
        }

        return true;
    }

    private static bool TryValidateRecord(
        BistroBuilderFinanceTransactionRecord record,
        long expectedSequence,
        out string error)
    {
        error = string.Empty;

        if (record == null || record.sequence != expectedSequence)
        {
            error = "El ledger financiero contiene una secuencia inválida.";
            return false;
        }

        if (!string.Equals(record.transactionId, BuildTransactionId(record.sequence), StringComparison.Ordinal) ||
            !IsCanonicalStableId(record.operationId) ||
            !IsCanonicalStableId(record.sourceSystemId) ||
            !IsCanonicalStableId(record.sourceReferenceId) ||
            !IsCanonicalStableId(record.categoryId))
        {
            error = "El ledger financiero contiene una identidad inválida.";
            return false;
        }

        if (!Enum.IsDefined(typeof(BistroBuilderFinanceTransactionKind), record.kind) ||
            record.amountCents <= 0L ||
            record.dayIndex < 1 ||
            record.minuteOfDay < 0 ||
            record.minuteOfDay > 1439 ||
            !string.Equals(record.description ?? string.Empty, NormalizeDescription(record.description), StringComparison.Ordinal) ||
            (record.description ?? string.Empty).Length > MaximumDescriptionLength)
        {
            error = "El ledger financiero contiene un movimiento inválido.";
            return false;
        }

        return true;
    }

    private static bool IsCanonicalStableId(string value)
    {
        return IsValidStableId(value) &&
               string.Equals(value, NormalizeStableId(value), StringComparison.Ordinal);
    }

    private static bool IsValidStableId(string value)
    {
        string normalized = NormalizeStableId(value);
        if (normalized.Length < 3 || normalized.Length > MaximumStableIdLength)
        {
            return false;
        }

        for (int index = 0; index < normalized.Length; index++)
        {
            char character = normalized[index];
            bool allowed =
                character >= 'a' && character <= 'z' ||
                character >= '0' && character <= '9' ||
                character == '_' ||
                character == '-' ||
                character == '.';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeStableId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static bool IsValidCurrency(string value)
    {
        if (value.Length != 3)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character < 'A' || character > 'Z')
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeCurrency(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static string NormalizeDescription(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string BuildTransactionId(long sequence)
    {
        return "finance_tx_" + sequence.ToString("D10");
    }
}
