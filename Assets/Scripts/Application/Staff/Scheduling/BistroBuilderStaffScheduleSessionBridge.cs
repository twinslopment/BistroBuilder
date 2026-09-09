using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adaptador 5C entre el plan de turnos y la sesión 4D.
///
/// No crea Waiter ni tareas y no modifica Staff. Cuando existe un plan para el
/// día/servicio actual, reconstruye de forma atómica el snapshot 4D usando los
/// WaiterId que 4D ya resolvió. TryRestoreSessionSnapshot conserva la autoridad
/// de elegibilidad y rollback transaccional de 4D.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(190)]
[AddComponentMenu("Bistro Builder/Staff/Staff Schedule Session Bridge")]
public sealed class BistroBuilderStaffScheduleSessionBridge : MonoBehaviour
{
    [SerializeField] private BistroBuilderStaffScheduleService scheduleService;
    [SerializeField] private BistroBuilderStaffService staffService;
    [SerializeField] private BistroBuilderStaffSessionService sessionService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private BistroBuilderCanonicalOrderIntegrationService orderIntegration;
    [SerializeField] private RestaurantServiceStateService serviceStateService;

    private readonly List<string> scheduledIds = new List<string>();
    private readonly List<string> eligibleIds = new List<string>();
    private readonly List<int> waiterIds = new List<int>();
    private bool subscribed;

    public event Action<int, BistroBuilderMealServiceAvailability, int> SessionScheduleApplied;

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (scheduleService == null || staffService == null || sessionService == null ||
            generalGameStateService == null || orderIntegration == null ||
            serviceStateService == null)
        {
            error = "5C necesita Schedule, Staff, Session 4D, calendario, servicio gastronómico y estado de servicio.";
            return false;
        }

        if (!scheduleService.ValidateConfiguration(out error) ||
            !staffService.ValidateConfiguration(out error) ||
            !sessionService.ValidateConfiguration(out error) ||
            generalGameStateService.DayIndex < 1)
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Reemplaza el binding recién creado por 4D solo si hay un plan explícito
    /// para el servicio actual. Un servicio sin plan conserva compatibilidad 4D.
    /// </summary>
    public bool TryApplyCurrentSchedule(out string error)
    {
        if (!ValidateConfiguration(out error))
            return false;

        if (!sessionService.HasActiveSession)
        {
            error = "No existe una sesión 4D activa que filtrar por horario.";
            return false;
        }

        BistroBuilderMealServiceAvailability mealService = orderIntegration.CurrentMealService;
        if (mealService == BistroBuilderMealServiceAvailability.None)
        {
            error = "El servicio gastronómico actual no está definido.";
            return false;
        }

        int dayIndex = Math.Max(1, generalGameStateService.DayIndex);
        scheduledIds.Clear();
        scheduleService.CopyScheduledEmployeeIds(dayIndex, mealService, scheduledIds);

        // Sin plan explícito se mantiene el comportamiento legacy de 4D.
        if (scheduledIds.Count == 0)
        {
            error = string.Empty;
            return true;
        }

        BistroBuilderStaffSessionSnapshot current = sessionService.CreateSessionSnapshot();
        if (current == null || !current.active || current.bindings == null)
        {
            error = "4D no expone un snapshot activo reconciliable.";
            return false;
        }

        if (!HasPristineMetrics(current))
        {
            error = "El horario no puede cambiar bindings después de observar trabajo real.";
            return false;
        }

        eligibleIds.Clear();
        for (int index = 0; index < scheduledIds.Count; index++)
        {
            string employeeId = scheduledIds[index];
            if (!staffService.TryGetEmployee(employeeId, out BistroBuilderEmployeeRecord employee) ||
                employee == null ||
                employee.employmentStatus != BistroBuilderEmploymentStatus.Active ||
                employee.availability != BistroBuilderEmployeeAvailability.Available ||
                !staffService.TryGetRoleDefinition(
                    employee.roleId,
                    out BistroBuilderStaffRoleDefinition role) ||
                role == null ||
                !string.Equals(
                    role.operationalAdapterId,
                    BistroBuilderStaffOperationalAdapterIds.WaiterAgent,
                    StringComparison.Ordinal))
            {
                error = "Un empleado programado ya no es un camarero activo y disponible: " + employeeId;
                return false;
            }
            eligibleIds.Add(employeeId);
        }
        eligibleIds.Sort(StringComparer.Ordinal);

        waiterIds.Clear();
        for (int index = 0; index < current.bindings.Count; index++)
        {
            BistroBuilderStaffSessionBindingRecord binding = current.bindings[index];
            if (binding != null) waiterIds.Add(binding.waiterId);
        }
        waiterIds.Sort();

        if (eligibleIds.Count > waiterIds.Count)
        {
            error = "El turno programa " + eligibleIds.Count +
                " camareros pero solo hay " + waiterIds.Count + " agentes Waiter resolubles por 4D.";
            return false;
        }

        var candidate = new BistroBuilderStaffSessionSnapshot
        {
            schemaId = BistroBuilderStaffSessionSnapshot.CurrentSchemaId,
            schemaVersion = BistroBuilderStaffSessionSnapshot.CurrentSchemaVersion,
            active = true,
            sessionId = current.sessionId,
            dayIndex = current.dayIndex,
            bindings = new List<BistroBuilderStaffSessionBindingRecord>()
        };
        try
        {
            candidate.revision = checked(current.revision + 1L);
        }
        catch (OverflowException)
        {
            error = "La revisión de staff.session.runtime no puede incrementarse.";
            return false;
        }

        for (int index = 0; index < eligibleIds.Count; index++)
        {
            candidate.bindings.Add(new BistroBuilderStaffSessionBindingRecord
            {
                employeeId = eligibleIds[index],
                waiterId = waiterIds[index],
                completedTasks = 0,
                failedTasks = 0,
                totalTaskDurationMilliseconds = 0L,
                handledTableIds = new List<int>()
            });
        }

        if (!sessionService.TryRestoreSessionSnapshot(candidate, out error))
            return false;

        SessionScheduleApplied?.Invoke(dayIndex, mealService, eligibleIds.Count);
        error = string.Empty;
        return true;
    }

    public bool IsCurrentSessionAligned(out string error)
    {
        if (!ValidateConfiguration(out error) || !sessionService.HasActiveSession)
            return false;

        BistroBuilderMealServiceAvailability mealService = orderIntegration.CurrentMealService;
        int dayIndex = Math.Max(1, generalGameStateService.DayIndex);
        scheduledIds.Clear();
        scheduleService.CopyScheduledEmployeeIds(dayIndex, mealService, scheduledIds);
        if (scheduledIds.Count == 0)
        {
            error = string.Empty;
            return true;
        }

        BistroBuilderStaffSessionSnapshot session = sessionService.CreateSessionSnapshot();
        var bound = new HashSet<string>(StringComparer.Ordinal);
        if (session != null && session.bindings != null)
        {
            for (int index = 0; index < session.bindings.Count; index++)
            {
                if (session.bindings[index] != null)
                    bound.Add(session.bindings[index].employeeId);
            }
        }

        for (int index = 0; index < scheduledIds.Count; index++)
        {
            if (!bound.Contains(scheduledIds[index]))
            {
                error = "La sesión 4D no contiene todo el turno programado.";
                return false;
            }
        }
        if (bound.Count != scheduledIds.Count)
        {
            error = "La sesión 4D contiene empleados no programados.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void HandleSessionStarted(string _)
    {
        if (!TryApplyCurrentSchedule(out string error) && !string.IsNullOrWhiteSpace(error))
            Debug.LogError("5C no pudo aplicar el horario a 4D. " + error, this);
    }

    private void HandleServiceOpeningRequested()
    {
        if (!sessionService.HasActiveSession) return;
        if (!IsCurrentSessionAligned(out _) &&
            !TryApplyCurrentSchedule(out string error))
        {
            Debug.LogError("5C detectó un horario no aplicado antes de Open. " + error, this);
        }
    }

    private static bool HasPristineMetrics(BistroBuilderStaffSessionSnapshot snapshot)
    {
        for (int index = 0; index < snapshot.bindings.Count; index++)
        {
            BistroBuilderStaffSessionBindingRecord binding = snapshot.bindings[index];
            if (binding == null || binding.completedTasks != 0 || binding.failedTasks != 0 ||
                binding.totalTaskDurationMilliseconds != 0L ||
                (binding.handledTableIds != null && binding.handledTableIds.Count != 0))
                return false;
        }
        return true;
    }

    private void Subscribe()
    {
        if (subscribed || sessionService == null || serviceStateService == null) return;
        sessionService.SessionStarted += HandleSessionStarted;
        serviceStateService.ServiceOpeningRequested += HandleServiceOpeningRequested;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (sessionService != null) sessionService.SessionStarted -= HandleSessionStarted;
        if (serviceStateService != null)
            serviceStateService.ServiceOpeningRequested -= HandleServiceOpeningRequested;
        subscribed = false;
    }

    private void CacheDependencies()
    {
        if (scheduleService == null) scheduleService = GetComponent<BistroBuilderStaffScheduleService>();
        if (staffService == null) staffService = GetComponent<BistroBuilderStaffService>();
        if (sessionService == null) sessionService = GetComponent<BistroBuilderStaffSessionService>();
        if (generalGameStateService == null)
            generalGameStateService = GetComponent<BistroBuilderGeneralGameStateService>();
        if (orderIntegration == null)
            orderIntegration = GetComponent<BistroBuilderCanonicalOrderIntegrationService>();
        if (serviceStateService == null)
            serviceStateService = GetComponent<RestaurantServiceStateService>();
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
