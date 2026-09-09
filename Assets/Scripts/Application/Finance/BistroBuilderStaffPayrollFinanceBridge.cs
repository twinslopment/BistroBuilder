using System;
using UnityEngine;

/// <summary>
/// Integración entre Personal/Horarios y Finanzas 3E.
/// Personal conserva empleados y salarios; 3E conserva la única publicación
/// monetaria. Este puente calcula nómina de la sesión real y proyección de
/// turnos, sin crear una segunda caja ni un segundo roster.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(240)]
[AddComponentMenu("Bistro Builder/Finance/Staff Payroll Finance Bridge")]
public sealed class BistroBuilderStaffPayrollFinanceBridge : MonoBehaviour
{
    [SerializeField] private BistroBuilderOperatingExpenseService operatingExpenseService;
    [SerializeField] private BistroBuilderFinanceService financeService;
    [SerializeField] private BistroBuilderStaffService staffService;
    [SerializeField] private BistroBuilderStaffSessionService sessionService;
    [SerializeField] private BistroBuilderStaffScheduleService scheduleService;
    [SerializeField] private BistroBuilderStaffScheduleSessionBridge scheduleSessionBridge;
    [SerializeField] private BistroBuilderCanonicalOrderIntegrationService orderIntegration;
    [SerializeField] private BistroBuilderSaveGameService saveGameService;

    private string activeSessionId = string.Empty;
    private int activeDayIndex;
    private BistroBuilderMealServiceAvailability activeMealService;
    private int activeEmployeeCount;
    private long activePayrollCents;
    private bool subscribed;
    private bool retryAfterSave;

    public event Action<BistroBuilderFinanceTransactionRecord> StaffPayrollPosted;

    public BistroBuilderOperatingExpenseService OperatingExpenseService =>
        operatingExpenseService;
    public BistroBuilderFinanceService FinanceService => financeService;
    public BistroBuilderStaffService StaffService => staffService;
    public BistroBuilderStaffSessionService SessionService => sessionService;
    public BistroBuilderStaffScheduleService ScheduleService => scheduleService;
    public BistroBuilderStaffScheduleSessionBridge ScheduleSessionBridge =>
        scheduleSessionBridge;
    public BistroBuilderCanonicalOrderIntegrationService OrderIntegration =>
        orderIntegration;
    public int ActiveEmployeeCount => activeEmployeeCount;
    public long ActivePayrollCents => activePayrollCents;
    public string ActiveSessionId => activeSessionId;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
        if (sessionService != null && sessionService.HasActiveSession &&
            !TryRefreshActiveSession(out string error))
        {
            Debug.LogError("Nómina Staff/3E no pudo hidratar sesión activa. " + error, this);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!retryAfterSave || saveGameService == null || saveGameService.IsBusy)
        {
            return;
        }

        retryAfterSave = false;
        if (!TryPostCapturedPayroll(out string error))
        {
            Debug.LogError("Nómina Staff/3E no pudo reintentarse. " + error, this);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (operatingExpenseService == null || financeService == null ||
            staffService == null || sessionService == null || scheduleService == null ||
            scheduleSessionBridge == null || orderIntegration == null || saveGameService == null)
        {
            error = "La integración nómina Staff/3E necesita Finanzas, Staff, sesión, horarios, servicio gastronómico y SaveGame.";
            return false;
        }

        if (!staffService.ValidateConfiguration(out error) ||
            !sessionService.ValidateConfiguration(out error) ||
            !scheduleService.ValidateConfiguration(out error) ||
            !scheduleSessionBridge.ValidateConfiguration(out error))
        {
            return false;
        }

        if (!operatingExpenseService.ValidateConfiguration(out error) ||
            !financeService.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Suma solo turnos explícitamente programados. Un servicio sin turno no
    /// es todavía una obligación salarial comprometida; si llega a abrirse,
    /// la sesión real publicará su nómina al finalizar.
    /// </summary>
    public bool TryCalculateScheduledPayrollObligationsCents(
        int startDayIndex,
        int endDayIndex,
        out long totalCents,
        out int scheduledServiceCount,
        out string error)
    {
        totalCents = 0L;
        scheduledServiceCount = 0;
        if (!ValidateConfiguration(out error))
        {
            return false;
        }
        if (startDayIndex < 1 || endDayIndex < startDayIndex)
        {
            error = "El intervalo de proyección salarial no es válido.";
            return false;
        }

        BistroBuilderMealServiceAvailability[] services =
        {
            BistroBuilderMealServiceAvailability.Breakfast,
            BistroBuilderMealServiceAvailability.Lunch,
            BistroBuilderMealServiceAvailability.Dinner
        };

        try
        {
            for (int day = startDayIndex; day <= endDayIndex; day++)
            {
                for (int index = 0; index < services.Length; index++)
                {
                    BistroBuilderMealServiceAvailability service = services[index];
                    if (!scheduleService.TryBuildCoverage(
                            day,
                            service,
                            out BistroBuilderStaffScheduleCoverage coverage,
                            out error))
                    {
                        totalCents = 0L;
                        scheduledServiceCount = 0;
                        return false;
                    }

                    if (coverage == null || coverage.projectedSalaryCents <= 0L)
                    {
                        continue;
                    }

                    string payrollRunId = BuildScheduledPayrollRunId(day, service);
                    string operationId =
                        BistroBuilderOperatingExpensePolicy.BuildPayrollOperationId(
                            payrollRunId);
                    if (financeService.TryGetTransactionByOperationId(operationId, out _))
                    {
                        continue;
                    }

                    totalCents = checked(totalCents + coverage.projectedSalaryCents);
                    scheduledServiceCount = checked(scheduledServiceCount + 1);
                }
            }
        }
        catch (OverflowException)
        {
            totalCents = 0L;
            scheduledServiceCount = 0;
            error = "La proyección salarial desborda el rango monetario.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryRefreshActiveSession(out string error)
    {
        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (!sessionService.HasActiveSession)
        {
            ClearActiveProjection();
            error = string.Empty;
            return true;
        }

        BistroBuilderStaffSessionSnapshot session = sessionService.CreateSessionSnapshot();
        if (session == null || !session.active || session.bindings == null ||
            !BistroBuilderStaffSessionIdUtility.IsValid(session.sessionId))
        {
            error = "La sesión activa de Personal no expone un snapshot salarial válido.";
            return false;
        }

        int employeeCount = 0;
        long total = 0L;
        try
        {
            for (int index = 0; index < session.bindings.Count; index++)
            {
                BistroBuilderStaffSessionBindingRecord binding = session.bindings[index];
                if (binding == null ||
                    !staffService.TryGetEmployee(
                        binding.employeeId,
                        out BistroBuilderEmployeeRecord employee) ||
                    employee == null)
                {
                    error = "Una asignación activa no resuelve su EmployeeId en Staff.";
                    return false;
                }

                employeeCount = checked(employeeCount + 1);
                total = checked(total + employee.salaryCentsPerService);
            }
        }
        catch (OverflowException)
        {
            error = "La nómina de la sesión desborda el rango monetario.";
            return false;
        }

        activeSessionId = BistroBuilderStaffSessionIdUtility.Normalize(session.sessionId);
        activeDayIndex = Math.Max(1, session.dayIndex);
        activeMealService = orderIntegration.CurrentMealService;
        activeEmployeeCount = employeeCount;
        activePayrollCents = total;
        error = string.Empty;
        return true;
    }

    public static string BuildScheduledPayrollRunId(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService)
    {
        if (dayIndex < 1)
        {
            return string.Empty;
        }

        string suffix;
        switch (mealService)
        {
            case BistroBuilderMealServiceAvailability.Breakfast:
                suffix = "breakfast";
                break;
            case BistroBuilderMealServiceAvailability.Lunch:
                suffix = "lunch";
                break;
            case BistroBuilderMealServiceAvailability.Dinner:
                suffix = "dinner";
                break;
            default:
                return string.Empty;
        }

        return "staffpay_day_" + dayIndex.ToString("D8") + "_" + suffix;
    }

    private string BuildCapturedPayrollRunId()
    {
        string scheduled = BuildScheduledPayrollRunId(
            activeDayIndex,
            activeMealService);
        if (!string.IsNullOrWhiteSpace(scheduled))
        {
            return scheduled;
        }

        string normalizedSession =
            BistroBuilderStaffSessionIdUtility.Normalize(activeSessionId);
        return BistroBuilderStaffSessionIdUtility.IsValid(normalizedSession)
            ? "staffpay_" + normalizedSession
            : string.Empty;
    }

    private bool TryPostCapturedPayroll(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(activeSessionId))
        {
            return true;
        }

        if (activeEmployeeCount <= 0 || activePayrollCents <= 0L)
        {
            ClearActiveProjection();
            return true;
        }

        string payrollRunId = BuildCapturedPayrollRunId();
        if (string.IsNullOrWhiteSpace(payrollRunId))
        {
            error = "No se pudo construir PayrollRunId estable para la sesión.";
            return false;
        }

        var request = new BistroBuilderPayrollBatchRequest
        {
            payrollRunId = payrollRunId,
            periodStartDayIndex = activeDayIndex,
            periodEndDayIndex = activeDayIndex,
            employeeCount = activeEmployeeCount,
            totalCents = activePayrollCents
        };

        if (!operatingExpenseService.TryPostPayrollBatch(
                request,
                out BistroBuilderFinanceTransactionRecord posted,
                out bool wasReplayed,
                out error))
        {
            if (saveGameService != null && saveGameService.IsBusy)
            {
                retryAfterSave = true;
            }
            return false;
        }

        if (!wasReplayed)
        {
            StaffPayrollPosted?.Invoke(posted);
        }
        ClearActiveProjection();
        retryAfterSave = false;
        return true;
    }

    private void HandleSessionStarted(string _)
    {
        if (!TryRefreshActiveSession(out string error))
        {
            Debug.LogError("Nómina Staff/3E no pudo capturar SessionStarted. " + error, this);
        }
    }

    private void HandleSessionRestored()
    {
        if (sessionService != null && !sessionService.HasActiveSession)
        {
            ClearActiveProjection();
            return;
        }

        if (sessionService != null && sessionService.HasActiveSession &&
            !TryRefreshActiveSession(out string error))
        {
            Debug.LogError("Nómina Staff/3E no pudo capturar SessionRestored. " + error, this);
        }
    }

    private void HandleAssignmentChanged(string _)
    {
        if (sessionService != null && sessionService.HasActiveSession &&
            !TryRefreshActiveSession(out string error))
        {
            Debug.LogError("Nómina Staff/3E no pudo actualizar asignaciones. " + error, this);
        }
    }
    private void HandleScheduleApplied(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        int _)
    {
        if (sessionService == null || !sessionService.HasActiveSession)
        {
            return;
        }

        if (!TryRefreshActiveSession(out string error))
        {
            Debug.LogError("Nómina Staff/3E no pudo capturar el turno aplicado. " + error, this);
            return;
        }

        activeDayIndex = Math.Max(1, dayIndex);
        activeMealService = mealService;
    }

    private void HandleSessionEnded(string sessionId)
    {
        string normalized = BistroBuilderStaffSessionIdUtility.Normalize(sessionId);
        if (!string.IsNullOrWhiteSpace(activeSessionId) &&
            !string.Equals(activeSessionId, normalized, StringComparison.Ordinal))
        {
            Debug.LogError("Nómina Staff/3E recibió SessionEnded de otra sesión.", this);
            return;
        }

        if (!TryPostCapturedPayroll(out string error) &&
            !retryAfterSave)
        {
            Debug.LogError("Nómina Staff/3E no pudo contabilizar la sesión. " + error, this);
        }
    }

    private void ClearActiveProjection()
    {
        activeSessionId = string.Empty;
        activeDayIndex = 0;
        activeMealService = BistroBuilderMealServiceAvailability.None;
        activeEmployeeCount = 0;
        activePayrollCents = 0L;
    }

    private void Subscribe()
    {
        if (subscribed || sessionService == null || scheduleSessionBridge == null)
        {
            return;
        }

        sessionService.SessionStarted += HandleSessionStarted;
        sessionService.SessionEnded += HandleSessionEnded;
        sessionService.SessionRestored += HandleSessionRestored;
        sessionService.AssignmentChanged += HandleAssignmentChanged;
        scheduleSessionBridge.SessionScheduleApplied += HandleScheduleApplied;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (sessionService != null)
        {
            sessionService.SessionStarted -= HandleSessionStarted;
            sessionService.SessionEnded -= HandleSessionEnded;
            sessionService.SessionRestored -= HandleSessionRestored;
            sessionService.AssignmentChanged -= HandleAssignmentChanged;
        }

        if (scheduleSessionBridge != null)
        {
            scheduleSessionBridge.SessionScheduleApplied -= HandleScheduleApplied;
        }
        subscribed = false;
    }

    private void CacheDependencies()
    {
        if (operatingExpenseService == null)
            TryGetComponent(out operatingExpenseService);
        if (financeService == null)
            TryGetComponent(out financeService);
        if (staffService == null)
            TryGetComponent(out staffService);
        if (sessionService == null)
            TryGetComponent(out sessionService);
        if (scheduleService == null)
            TryGetComponent(out scheduleService);
        if (scheduleSessionBridge == null)
            TryGetComponent(out scheduleSessionBridge);
        if (orderIntegration == null)
            TryGetComponent(out orderIntegration);
        if (saveGameService == null)
            TryGetComponent(out saveGameService);
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
