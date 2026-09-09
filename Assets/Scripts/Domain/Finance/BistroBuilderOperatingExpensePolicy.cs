using System;
using System.Collections.Generic;

public static class BistroBuilderOperatingExpensePolicy
{
    public const string OperatingSourceSystemId =
        "finance.operating_expenses";

    public const string PayrollSourceSystemId = "personnel";
    public const string PayrollCategoryId = "expense.payroll";

    private const string OperatingCategoryPrefix = "expense.operating.";
    private const int MaximumStableIdLength = 96;
    private const int MaximumDisplayNameLength = 80;

    public static bool TryValidateProfile(
        BistroBuilderOperatingExpenseProfile profile,
        out string error)
    {
        error = string.Empty;

        if (profile == null)
        {
            error = "Falta el perfil de gastos operativos.";
            return false;
        }

        if (!IsCanonicalStableId(profile.ProfileId))
        {
            error = "El ProfileId de gastos operativos no es válido.";
            return false;
        }

        IReadOnlyList<BistroBuilderRecurringExpenseDefinition> expenses =
            profile.Expenses;

        if (expenses == null || expenses.Count == 0)
        {
            error = "El perfil de gastos operativos no contiene gastos.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < expenses.Count; index++)
        {
            BistroBuilderRecurringExpenseDefinition expense = expenses[index];

            if (!TryValidateExpense(expense, out error))
            {
                error = "Gasto " + index + ": " + error;
                return false;
            }

            if (!ids.Add(expense.ExpenseId))
            {
                error = "El perfil repite ExpenseId " +
                        expense.ExpenseId + ".";
                return false;
            }
        }

        return true;
    }

    public static bool TryValidateExpense(
        BistroBuilderRecurringExpenseDefinition expense,
        out string error)
    {
        error = string.Empty;

        if (expense == null)
        {
            error = "La definición de gasto es nula.";
            return false;
        }

        if (!IsCanonicalStableId(expense.ExpenseId))
        {
            error = "ExpenseId inválido.";
            return false;
        }

        string displayName = expense.DisplayName.Trim();
        if (displayName.Length == 0 ||
            displayName.Length > MaximumDisplayNameLength)
        {
            error = "El nombre visible del gasto no es válido.";
            return false;
        }

        if (!IsCanonicalStableId(expense.CategoryId) ||
            !expense.CategoryId.StartsWith(
                OperatingCategoryPrefix,
                StringComparison.Ordinal))
        {
            error = "La categoría debe pertenecer a expense.operating.";
            return false;
        }

        if (expense.AmountCents <= 0L)
        {
            error = "El importe del gasto debe ser positivo.";
            return false;
        }

        if (expense.FirstDueDayIndex < 1 || expense.IntervalDays < 1)
        {
            error = "El calendario recurrente del gasto no es válido.";
            return false;
        }

        return true;
    }

    public static bool TryValidatePayrollBatch(
        BistroBuilderPayrollBatchRequest request,
        out string error)
    {
        error = string.Empty;

        if (request == null)
        {
            error = "La nómina es nula.";
            return false;
        }

        if (!IsCanonicalStableId(request.payrollRunId))
        {
            error = "PayrollRunId inválido.";
            return false;
        }

        if (request.periodStartDayIndex < 1 ||
            request.periodEndDayIndex < request.periodStartDayIndex)
        {
            error = "El periodo de nómina no es válido.";
            return false;
        }

        if (request.employeeCount < 1)
        {
            error = "La nómina debe incluir al menos una persona.";
            return false;
        }

        if (request.totalCents <= 0L)
        {
            error = "El total de nómina debe ser positivo.";
            return false;
        }

        return true;
    }

    public static bool IsDueOnDay(
        BistroBuilderRecurringExpenseDefinition expense,
        int dayIndex)
    {
        if (expense == null ||
            !expense.Active ||
            dayIndex < expense.FirstDueDayIndex ||
            expense.IntervalDays < 1)
        {
            return false;
        }

        return (dayIndex - expense.FirstDueDayIndex) %
               expense.IntervalDays == 0;
    }

    public static bool TryGetNextDueDay(
        BistroBuilderRecurringExpenseDefinition expense,
        int fromDayIndex,
        out int dueDayIndex)
    {
        dueDayIndex = 0;

        if (expense == null ||
            !expense.Active ||
            expense.FirstDueDayIndex < 1 ||
            expense.IntervalDays < 1 ||
            fromDayIndex < 1)
        {
            return false;
        }

        if (fromDayIndex <= expense.FirstDueDayIndex)
        {
            dueDayIndex = expense.FirstDueDayIndex;
            return true;
        }

        long elapsed =
            (long)fromDayIndex - expense.FirstDueDayIndex;
        long intervals =
            (elapsed + expense.IntervalDays - 1L) /
            expense.IntervalDays;
        long candidate =
            expense.FirstDueDayIndex +
            intervals * expense.IntervalDays;

        if (candidate > int.MaxValue)
        {
            return false;
        }

        dueDayIndex = (int)candidate;
        return true;
    }

    public static bool TryBuildOperatingTransactionRequest(
        BistroBuilderRecurringExpenseDefinition expense,
        int dayIndex,
        int minuteOfDay,
        out BistroBuilderFinanceTransactionRequest request,
        out string error)
    {
        request = null;

        if (!TryValidateExpense(expense, out error))
        {
            return false;
        }

        if (!IsDueOnDay(expense, dayIndex))
        {
            error = "El gasto no vence en el día indicado.";
            return false;
        }

        string operationId = BuildOperatingOperationId(
            expense.ExpenseId,
            dayIndex);

        var candidate = new BistroBuilderFinanceTransactionRequest
        {
            operationId = operationId,
            sourceSystemId = OperatingSourceSystemId,
            sourceReferenceId = expense.ExpenseId,
            categoryId = expense.CategoryId,
            kind = BistroBuilderFinanceTransactionKind.Debit,
            amountCents = expense.AmountCents,
            dayIndex = dayIndex,
            minuteOfDay = minuteOfDay,
            description = expense.DisplayName.Trim() +
                          " (día " + dayIndex + ")."
        };

        if (!BistroBuilderFinanceEngine.TryValidateRequest(
                candidate,
                out error))
        {
            return false;
        }

        request = candidate;
        return true;
    }

    public static bool TryBuildPayrollTransactionRequest(
        BistroBuilderPayrollBatchRequest payroll,
        int paymentDayIndex,
        int minuteOfDay,
        out BistroBuilderFinanceTransactionRequest request,
        out string error)
    {
        request = null;

        if (!TryValidatePayrollBatch(payroll, out error))
        {
            return false;
        }

        string operationId = BuildPayrollOperationId(
            payroll.payrollRunId);

        var candidate = new BistroBuilderFinanceTransactionRequest
        {
            operationId = operationId,
            sourceSystemId = PayrollSourceSystemId,
            sourceReferenceId = payroll.payrollRunId,
            categoryId = PayrollCategoryId,
            kind = BistroBuilderFinanceTransactionKind.Debit,
            amountCents = payroll.totalCents,
            dayIndex = paymentDayIndex,
            minuteOfDay = minuteOfDay,
            description =
                "Nómina días " +
                payroll.periodStartDayIndex + "-" +
                payroll.periodEndDayIndex + ": " +
                payroll.employeeCount + " persona(s)."
        };

        if (!BistroBuilderFinanceEngine.TryValidateRequest(
                candidate,
                out error))
        {
            return false;
        }

        request = candidate;
        return true;
    }

    public static string BuildOperatingOperationId(
        string expenseId,
        int dayIndex)
    {
        return "operating_" + NormalizeStableId(expenseId) +
               "_day_" + dayIndex.ToString("D8");
    }

    public static string BuildPayrollOperationId(string payrollRunId)
    {
        return "payroll_" + NormalizeStableId(payrollRunId);
    }

    private static bool IsCanonicalStableId(string value)
    {
        string normalized = NormalizeStableId(value);

        if (normalized.Length < 3 ||
            normalized.Length > MaximumStableIdLength ||
            !string.Equals(
                value,
                normalized,
                StringComparison.Ordinal))
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
}
