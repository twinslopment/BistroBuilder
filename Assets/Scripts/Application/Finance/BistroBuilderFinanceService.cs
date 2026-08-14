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
        TryInitializeFresh(out _);
    }

    public bool ValidateConfiguration(out string error)
    {
        if (!ValidateSerializedConfiguration(out error))
        {
            return false;
        }

        return state == null || BistroBuilderFinanceEngine.TryValidateSnapshot(state, out error);
    }

    public bool TryInitializeFresh(out string error)
    {
        if (!ValidateSerializedConfiguration(out error))
        {
            return false;
        }

        BistroBuilderFinanceSnapshot candidate =
            BistroBuilderFinanceEngine.CreateInitialSnapshot(
                openingBalanceCents,
                currencyCode);

        if (!BistroBuilderFinanceEngine.TryValidateSnapshot(candidate, out error))
        {
            return false;
        }

        state = candidate;
        byOperationId.Clear();
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

        BistroBuilderFinanceSnapshot clone = candidate.DeepClone();
        Dictionary<string, BistroBuilderFinanceTransactionRecord> rebuilt =
            new Dictionary<string, BistroBuilderFinanceTransactionRecord>(StringComparer.Ordinal);

        for (int index = 0; index < clone.transactions.Count; index++)
        {
            BistroBuilderFinanceTransactionRecord record = clone.transactions[index];
            rebuilt.Add(record.operationId, record);
        }

        state = clone;
        byOperationId.Clear();
        foreach (KeyValuePair<string, BistroBuilderFinanceTransactionRecord> pair in rebuilt)
        {
            byOperationId.Add(pair.Key, pair.Value);
        }

        StateRestored?.Invoke();
        return true;
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
