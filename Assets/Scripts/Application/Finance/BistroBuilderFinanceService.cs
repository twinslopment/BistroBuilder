using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Única autoridad runtime de caja y movimientos monetarios de Bistro Builder.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Finance Service")]
public sealed class BistroBuilderFinanceService : MonoBehaviour
{
    [SerializeField, Min(0)]
    private long openingBalanceCents = 5000000L;

    [SerializeField]
    private string currencyCode = "EUR";

    private readonly Dictionary<string, BistroBuilderFinanceTransactionRecord>
        byOperationId =
            new Dictionary<string, BistroBuilderFinanceTransactionRecord>(StringComparer.Ordinal);

    private BistroBuilderFinanceSnapshot state;

    public event Action<BistroBuilderFinanceTransactionRecord> TransactionPosted;
    public event Action StateRestored;

    public bool IsInitialized => state != null;
    public long OpeningBalanceCents => openingBalanceCents;
    public long CurrentBalanceCents => state != null ? state.currentBalanceCents : openingBalanceCents;
    public long Revision => state != null ? state.revision : 0L;
    public string CurrencyCode => state != null ? state.currencyCode : NormalizeCurrency(currencyCode);
    public int TransactionCount => state != null && state.transactions != null ? state.transactions.Count : 0;

    private void Awake()
    {
        if (!TryInitializeFresh(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        return ValidateSerializedConfiguration(out error);
    }

    public bool TryInitializeFresh(out string error)
    {
        if (!ValidateSerializedConfiguration(out error))
        {
            return false;
        }

        state = BistroBuilderFinanceEngine.CreateInitialSnapshot(
            openingBalanceCents,
            currencyCode);
        byOperationId.Clear();
        error = string.Empty;
        return true;
    }

    public bool TryPostTransaction(
        BistroBuilderFinanceTransactionRequest request,
        out BistroBuilderFinanceTransactionRecord posted,
        out string error)
    {
        posted = null;

        if (!EnsureInitialized(out error) ||
            !BistroBuilderFinanceEngine.TryValidateRequest(request, out error))
        {
            return false;
        }

        string operationId = BistroBuilderFinanceEngine.NormalizeOperationId(request.operationId);
        if (byOperationId.TryGetValue(operationId, out BistroBuilderFinanceTransactionRecord existing))
        {
            if (!BistroBuilderFinanceEngine.IsEquivalent(existing, request))
            {
                error = "El OperationId ya existe con un contenido financiero distinto.";
                return false;
            }

            posted = existing.DeepClone();
            return true;
        }

        if (!BistroBuilderFinanceEngine.TryAppendNewTransaction(
                state,
                request,
                out posted,
                out error))
        {
            return false;
        }

        BistroBuilderFinanceTransactionRecord stored =
            state.transactions[state.transactions.Count - 1];
        byOperationId.Add(stored.operationId, stored);
        TransactionPosted?.Invoke(posted.DeepClone());
        return true;
    }

    /// <summary>
    /// Publica varias patas monetarias como una sola operación atómica.
    /// Si cualquier solicitud falla, el ledger y el saldo permanecen intactos.
    /// Reintentos íntegramente equivalentes continúan siendo idempotentes.
    /// </summary>
    public bool TryPostTransactions(
        IReadOnlyList<BistroBuilderFinanceTransactionRequest> requests,
        out List<BistroBuilderFinanceTransactionRecord> posted,
        out string error)
    {
        posted = new List<BistroBuilderFinanceTransactionRecord>();

        if (!EnsureInitialized(out error))
        {
            return false;
        }

        if (requests == null || requests.Count == 0)
        {
            error = "El lote financiero no contiene movimientos.";
            return false;
        }

        BistroBuilderFinanceSnapshot candidate = state.DeepClone();
        var candidateByOperation =
            new Dictionary<string, BistroBuilderFinanceTransactionRecord>(
                StringComparer.Ordinal);

        for (int index = 0; index < candidate.transactions.Count; index++)
        {
            BistroBuilderFinanceTransactionRecord record = candidate.transactions[index];
            candidateByOperation.Add(record.operationId, record);
        }

        var newRecords = new List<BistroBuilderFinanceTransactionRecord>(requests.Count);
        var requestIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < requests.Count; index++)
        {
            BistroBuilderFinanceTransactionRequest request = requests[index];
            if (!BistroBuilderFinanceEngine.TryValidateRequest(request, out error))
            {
                posted.Clear();
                return false;
            }

            string operationId =
                BistroBuilderFinanceEngine.NormalizeOperationId(request.operationId);
            if (!requestIds.Add(operationId))
            {
                error = "El lote financiero repite OperationId.";
                posted.Clear();
                return false;
            }

            if (candidateByOperation.TryGetValue(
                    operationId,
                    out BistroBuilderFinanceTransactionRecord existing))
            {
                if (!BistroBuilderFinanceEngine.IsEquivalent(existing, request))
                {
                    error = "El OperationId ya existe con un contenido financiero distinto.";
                    posted.Clear();
                    return false;
                }

                posted.Add(existing.DeepClone());
                continue;
            }

            if (!BistroBuilderFinanceEngine.TryAppendNewTransaction(
                    candidate,
                    request,
                    out BistroBuilderFinanceTransactionRecord appended,
                    out error))
            {
                posted.Clear();
                return false;
            }

            BistroBuilderFinanceTransactionRecord stored =
                candidate.transactions[candidate.transactions.Count - 1];
            candidateByOperation.Add(stored.operationId, stored);
            posted.Add(appended);
            newRecords.Add(stored);
        }

        if (newRecords.Count == 0)
        {
            error = string.Empty;
            return true;
        }

        state = candidate;
        RebuildOperationIndex();

        for (int index = 0; index < newRecords.Count; index++)
        {
            TransactionPosted?.Invoke(newRecords[index].DeepClone());
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetTransactionByOperationId(
        string operationId,
        out BistroBuilderFinanceTransactionRecord transaction)
    {
        transaction = null;
        if (state == null || string.IsNullOrWhiteSpace(operationId))
        {
            return false;
        }

        string normalized = BistroBuilderFinanceEngine.NormalizeOperationId(operationId);
        if (!byOperationId.TryGetValue(
                normalized,
                out BistroBuilderFinanceTransactionRecord stored))
        {
            return false;
        }

        transaction = stored.DeepClone();
        return true;
    }

    public BistroBuilderFinanceSnapshot CreateSnapshot()
    {
        return state != null ? state.DeepClone() : null;
    }

    public bool TryRestoreSnapshot(
        BistroBuilderFinanceSnapshot candidate,
        out string error)
    {
        if (candidate == null)
        {
            error = "El snapshot financiero a restaurar es nulo.";
            return false;
        }

        if (!BistroBuilderFinanceEngine.TryValidateSnapshot(candidate, out error))
        {
            return false;
        }

        string configuredCurrency = NormalizeCurrency(currencyCode);
        if (!string.Equals(candidate.currencyCode, configuredCurrency, StringComparison.Ordinal))
        {
            error = "La moneda del snapshot financiero no coincide con la configuración de la partida.";
            return false;
        }

        state = candidate.DeepClone();
        RebuildOperationIndex();
        StateRestored?.Invoke();
        return true;
    }

    private void RebuildOperationIndex()
    {
        byOperationId.Clear();
        if (state == null || state.transactions == null)
        {
            return;
        }

        for (int index = 0; index < state.transactions.Count; index++)
        {
            BistroBuilderFinanceTransactionRecord record = state.transactions[index];
            byOperationId.Add(record.operationId, record);
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

    private bool ValidateSerializedConfiguration(out string error)
    {
        if (openingBalanceCents < 0L)
        {
            error = "El saldo inicial no puede ser negativo.";
            return false;
        }

        string normalizedCurrency = NormalizeCurrency(currencyCode);
        if (normalizedCurrency.Length != 3)
        {
            error = "La moneda financiera debe tener tres letras.";
            return false;
        }

        for (int index = 0; index < normalizedCurrency.Length; index++)
        {
            char character = normalizedCurrency[index];
            if (character < 'A' || character > 'Z')
            {
                error = "La moneda financiera no es válida.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static string NormalizeCurrency(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (openingBalanceCents < 0L)
        {
            openingBalanceCents = 0L;
        }

        currencyCode = NormalizeCurrency(currencyCode);
    }
#endif
}
