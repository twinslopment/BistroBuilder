using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderRecurringExpenseDefinition
{
    [SerializeField]
    private string expenseId = string.Empty;

    [SerializeField]
    private string displayName = string.Empty;

    [SerializeField]
    private string categoryId = string.Empty;

    [SerializeField]
    private long amountCents;

    [SerializeField]
    private int firstDueDayIndex = 1;

    [SerializeField]
    private int intervalDays = 1;

    [SerializeField]
    private bool active = true;

    public string ExpenseId => expenseId ?? string.Empty;
    public string DisplayName => displayName ?? string.Empty;
    public string CategoryId => categoryId ?? string.Empty;
    public long AmountCents => amountCents;
    public int FirstDueDayIndex => firstDueDayIndex;
    public int IntervalDays => intervalDays;
    public bool Active => active;

    public BistroBuilderRecurringExpenseDefinition(
        string expenseId,
        string displayName,
        string categoryId,
        long amountCents,
        int firstDueDayIndex,
        int intervalDays,
        bool active = true)
    {
        this.expenseId = expenseId ?? string.Empty;
        this.displayName = displayName ?? string.Empty;
        this.categoryId = categoryId ?? string.Empty;
        this.amountCents = amountCents;
        this.firstDueDayIndex = firstDueDayIndex;
        this.intervalDays = intervalDays;
        this.active = active;
    }

    public bool TryValidate(out string error)
    {
        return BistroBuilderOperatingExpensePolicy.TryValidateExpense(
            this,
            out error);
    }

    public bool IsDueOnDay(int dayIndex)
    {
        return BistroBuilderOperatingExpensePolicy.IsDueOnDay(this, dayIndex);
    }
}

/// <summary>
/// Datos de autoría de los gastos operativos recurrentes.
/// No guarda movimientos ni saldo: la autoridad monetaria sigue siendo
/// finance.runtime.
/// </summary>
[CreateAssetMenu(
    fileName = "OperatingExpenseProfile",
    menuName = "Bistro Builder/Finance/Operating Expense Profile",
    order = 300)]
public sealed class BistroBuilderOperatingExpenseProfile : ScriptableObject
{
    [SerializeField]
    private string profileId = "operating_expenses_v1";

    [SerializeField]
    private List<BistroBuilderRecurringExpenseDefinition> expenses =
        new List<BistroBuilderRecurringExpenseDefinition>();

    public string ProfileId => profileId ?? string.Empty;

    public IReadOnlyList<BistroBuilderRecurringExpenseDefinition> Expenses =>
        expenses;

    public bool TryValidate(out string error)
    {
        return BistroBuilderOperatingExpensePolicy.TryValidateProfile(
            this,
            out error);
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string newProfileId,
        IReadOnlyList<BistroBuilderRecurringExpenseDefinition> definitions)
    {
        profileId = newProfileId ?? string.Empty;
        expenses = definitions != null
            ? new List<BistroBuilderRecurringExpenseDefinition>(definitions)
            : new List<BistroBuilderRecurringExpenseDefinition>();
    }
#endif
}

/// <summary>
/// Resumen de una nómina calculada por la futura autoridad de Personal.
/// Finanzas recibe únicamente el total a pagar y el periodo; no posee
/// empleados, contratos laborales, turnos ni salarios individuales.
/// </summary>
public sealed class BistroBuilderPayrollBatchRequest
{
    public string payrollRunId = string.Empty;
    public int periodStartDayIndex;
    public int periodEndDayIndex;
    public int employeeCount;
    public long totalCents;
}
