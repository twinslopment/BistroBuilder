using System;

/// <summary>
/// Solicitud de gasto discrecional procedente de un sistema externo.
/// Finanzas no interpreta campañas, obras ni mejoras; solo identidad,
/// categoría e importe.
/// </summary>
public sealed class BistroBuilderDiscretionaryExpenseRequest
{
    public string operationId;
    public string sourceSystemId;
    public string sourceReferenceId;
    public string categoryId;
    public long amountCents;
    public string description;
}

public static class BistroBuilderDiscretionaryFinancePolicy
{
    public static bool TryValidateExpense(
        BistroBuilderDiscretionaryExpenseRequest request,
        out string error)
    {
        if (request == null)
        {
            error = "La solicitud de gasto discrecional es nula.";
            return false;
        }

        if (request.amountCents <= 0L)
        {
            error = "El gasto discrecional debe ser positivo.";
            return false;
        }

        string category = Normalize(request.categoryId);
        if (!IsAllowedCategory(category))
        {
            error = "La categoría no pertenece al contrato financiero de 3F.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool IsAllowedCategory(string categoryId)
    {
        string category = Normalize(categoryId);
        return category == "expense.marketing" ||
               category.StartsWith("expense.marketing.", StringComparison.Ordinal) ||
               category == "investment.renovation" ||
               category.StartsWith("investment.renovation.", StringComparison.Ordinal) ||
               category == "investment.improvement" ||
               category.StartsWith("investment.improvement.", StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
