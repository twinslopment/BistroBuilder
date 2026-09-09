using UnityEngine;

/// <summary>
/// Contrato financiero para Marketing, obras y mejoras futuras.
/// No posee estado: autoriza contra la liquidez disponible y publica el
/// gasto directamente en finance.runtime.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Discretionary Finance Service")]
public sealed class BistroBuilderDiscretionaryFinanceService : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderFinanceService financeService;

    [SerializeField]
    private BistroBuilderSupplierPurchaseFinanceBridge supplierFinanceBridge;

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [SerializeField]
    private GameClock gameClock;

    public bool IsInitialized =>
        financeService != null && financeService.IsInitialized &&
        generalGameStateService != null && gameClock != null;

    public bool ValidateConfiguration(out string error)
    {
        if (financeService == null ||
            supplierFinanceBridge == null ||
            generalGameStateService == null ||
            gameClock == null)
        {
            error = "3F necesita Finanzas, compromisos de proveedores, estado general y reloj.";
            return false;
        }

        if (!financeService.ValidateConfiguration(out error))
        {
            return false;
        }

        return generalGameStateService.ValidateConfiguration(out error);
    }

    public bool TryGetAvailableCashCents(
        out long availableCents,
        out string error)
    {
        availableCents = 0L;

        if (financeService == null || !financeService.IsInitialized)
        {
            error = "La autoridad financiera no está disponible.";
            return false;
        }

        if (supplierFinanceBridge == null)
        {
            availableCents = financeService.CurrentBalanceCents;
            error = string.Empty;
            return true;
        }

        return supplierFinanceBridge.TryGetFinancialPosition(
            out _,
            out availableCents,
            out error);
    }

    /// <summary>
    /// Autoriza una operación con varias patas usando su efecto neto.
    /// Los créditos del mismo acto pueden financiar sus costes de retirada.
    /// </summary>
    public bool TryAuthorizeNetCashEffect(
        long creditCents,
        long debitCents,
        out string error)
    {
        if (financeService == null || !financeService.IsInitialized)
        {
            error = "La autoridad financiera no está disponible.";
            return false;
        }

        if (creditCents < 0L || debitCents < 0L)
        {
            error = "Los importes de autorización no pueden ser negativos.";
            return false;
        }

        long requiredCents;
        try
        {
            requiredCents = debitCents > creditCents
                ? checked(debitCents - creditCents)
                : 0L;
        }
        catch (System.OverflowException)
        {
            error = "La autorización monetaria queda fuera de rango.";
            return false;
        }

        if (requiredCents == 0L)
        {
            error = string.Empty;
            return true;
        }

        if (!TryGetAvailableCashCents(out long availableCents, out error))
        {
            return false;
        }

        if (availableCents < requiredCents)
        {
            error = "Fondos insuficientes. Disponible: " +
                    FormatMoney(availableCents) +
                    "; necesario: " + FormatMoney(requiredCents) + ".";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryPostExpense(
        BistroBuilderDiscretionaryExpenseRequest request,
        out BistroBuilderFinanceTransactionRecord posted,
        out string error)
    {
        posted = null;

        if (!BistroBuilderDiscretionaryFinancePolicy.TryValidateExpense(
                request,
                out error) ||
            !TryAuthorizeNetCashEffect(0L, request.amountCents, out error))
        {
            return false;
        }

        if (generalGameStateService == null || gameClock == null)
        {
            error = "No existe contexto temporal para registrar el gasto.";
            return false;
        }

        return financeService.TryPostTransaction(
            new BistroBuilderFinanceTransactionRequest
            {
                operationId = request.operationId,
                sourceSystemId = request.sourceSystemId,
                sourceReferenceId = request.sourceReferenceId,
                categoryId = request.categoryId,
                kind = BistroBuilderFinanceTransactionKind.Debit,
                amountCents = request.amountCents,
                dayIndex = generalGameStateService.DayIndex,
                minuteOfDay = gameClock.Hour * 60 + gameClock.Minute,
                description = request.description
            },
            out posted,
            out error);
    }

    private static string FormatMoney(long cents)
    {
        return (cents / 100m).ToString("N2") + " €";
    }
}
