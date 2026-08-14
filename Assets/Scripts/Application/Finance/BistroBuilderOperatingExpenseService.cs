using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Orquestador 3E de gastos operativos y nóminas.
/// FinanceService continúa siendo la única autoridad de caja y ledger.
/// Este servicio no posee empleados ni duplica movimientos monetarios.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(220)]
[AddComponentMenu("Bistro Builder/Finance/Operating Expenses Service")]
public sealed class BistroBuilderOperatingExpenseService : MonoBehaviour
{
    [SerializeField]
    private BistroBuilderFinanceService financeService;

    [SerializeField]
    private BistroBuilderGeneralGameStateService generalGameStateService;

    [SerializeField]
    private GameClock gameClock;

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [SerializeField]
    private BistroBuilderOperatingExpenseProfile expenseProfile;

    public event Action<BistroBuilderFinanceTransactionRecord>
        OperatingExpensePosted;

    public event Action<BistroBuilderFinanceTransactionRecord>
        PayrollPosted;

    public BistroBuilderOperatingExpenseProfile ExpenseProfile =>
        expenseProfile;

    private bool subscribed;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        if (saveGameService != null && saveGameService.IsBusy)
        {
            return;
        }

        if (!TryProcessCurrentDay(out _, out string error))
        {
            Debug.LogError("3E no pudo procesar gastos del día. " + error, this);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();

        if (financeService == null ||
            generalGameStateService == null ||
            gameClock == null ||
            saveGameService == null ||
            expenseProfile == null)
        {
            error =
                "3E necesita Finanzas, calendario, reloj, guardado y perfil.";
            return false;
        }

        if (!financeService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error) ||
            !expenseProfile.TryValidate(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryProcessCurrentDay(
        out int postedCount,
        out string error)
    {
        postedCount = 0;
        error = string.Empty;

        if (saveGameService != null && saveGameService.IsBusy)
        {
            return true;
        }

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        int dayIndex = Math.Max(1, generalGameStateService.DayIndex);
        int minuteOfDay = CurrentMinuteOfDay;
        var expenses = expenseProfile.Expenses;

        for (int index = 0; index < expenses.Count; index++)
        {
            BistroBuilderRecurringExpenseDefinition expense =
                expenses[index];

            if (!BistroBuilderOperatingExpensePolicy.IsDueOnDay(
                    expense,
                    dayIndex))
            {
                continue;
            }

            if (!BistroBuilderOperatingExpensePolicy
                    .TryBuildOperatingTransactionRequest(
                        expense,
                        dayIndex,
                        minuteOfDay,
                        out BistroBuilderFinanceTransactionRequest request,
                        out error))
            {
                return false;
            }

            if (financeService.TryGetTransactionByOperationId(
                    request.operationId,
                    out _))
            {
                continue;
            }

            if (!financeService.TryPostTransaction(
                    request,
                    out BistroBuilderFinanceTransactionRecord posted,
                    out error))
            {
                error =
                    "No se pudo registrar " + expense.ExpenseId + ". " +
                    error;
                return false;
            }

            postedCount++;
            OperatingExpensePosted?.Invoke(posted);
        }

        return true;
    }

    /// <summary>
    /// Registra una nómina ya calculada por Personal.
    /// 3E no calcula salarios ni conserva un roster laboral.
    /// </summary>
    public bool TryPostPayrollBatch(
        BistroBuilderPayrollBatchRequest payroll,
        out BistroBuilderFinanceTransactionRecord posted,
        out bool wasReplayed,
        out string error)
    {
        posted = null;
        wasReplayed = false;
        error = string.Empty;

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (saveGameService.IsBusy)
        {
            error =
                "No puede registrarse una nómina durante guardado o carga.";
            return false;
        }

        if (!BistroBuilderOperatingExpensePolicy
                .TryBuildPayrollTransactionRequest(
                    payroll,
                    Math.Max(1, generalGameStateService.DayIndex),
                    CurrentMinuteOfDay,
                    out BistroBuilderFinanceTransactionRequest request,
                    out error))
        {
            return false;
        }

        wasReplayed =
            financeService.TryGetTransactionByOperationId(
                request.operationId,
                out _);

        if (!financeService.TryPostTransaction(
                request,
                out posted,
                out error))
        {
            wasReplayed = false;
            return false;
        }

        if (!wasReplayed)
        {
            PayrollPosted?.Invoke(posted);
        }

        return true;
    }

    private int CurrentMinuteOfDay =>
        Mathf.Clamp(gameClock.Hour, 0, 23) * 60 +
        Mathf.Clamp(gameClock.Minute, 0, 59);

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        CacheDependencies();

        if (generalGameStateService == null || saveGameService == null)
        {
            return;
        }

        generalGameStateService.CalendarChanged += HandleCalendarChanged;
        saveGameService.OperationCompleted += HandleSaveOperationCompleted;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (generalGameStateService != null)
        {
            generalGameStateService.CalendarChanged -=
                HandleCalendarChanged;
        }

        if (saveGameService != null)
        {
            saveGameService.OperationCompleted -=
                HandleSaveOperationCompleted;
        }

        subscribed = false;
    }

    private void HandleCalendarChanged()
    {
        if (saveGameService != null && saveGameService.IsBusy)
        {
            return;
        }

        if (!TryProcessCurrentDay(out _, out string error))
        {
            Debug.LogError(
                "3E no pudo procesar el nuevo día. " + error,
                this);
        }
    }

    private void HandleSaveOperationCompleted(
        BistroBuilderSaveOperationResult result)
    {
        if (result == null ||
            !result.Succeeded ||
            result.OperationKind != BistroBuilderSaveOperationKind.Load)
        {
            return;
        }

        StartCoroutine(ReconcileAfterLoad());
    }

    private IEnumerator ReconcileAfterLoad()
    {
        while (saveGameService != null && saveGameService.IsBusy)
        {
            yield return null;
        }

        if (!TryProcessCurrentDay(out _, out string error))
        {
            Debug.LogError(
                "3E no pudo reconciliar gastos después de Load. " + error,
                this);
        }
    }

    private void CacheDependencies()
    {
        if (financeService == null)
        {
            TryGetComponent(out financeService);
        }

        if (generalGameStateService == null)
        {
            TryGetComponent(out generalGameStateService);
        }

        if (gameClock == null)
        {
            TryGetComponent(out gameClock);
        }

        if (saveGameService == null)
        {
            TryGetComponent(out saveGameService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependencies();
    }

    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
