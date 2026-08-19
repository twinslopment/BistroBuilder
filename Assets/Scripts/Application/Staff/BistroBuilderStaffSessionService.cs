using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 4D — Binding Employee persistente ↔ Waiter operativo existente.
///
/// No crea ni destruye camareros. No reparte tareas. WaiterTaskCoordinator
/// continúa siendo la autoridad operacional y StaffService la autoridad de
/// empleado. Esta capa solo controla elegibilidad, asociación de sesión y
/// captura conservadora de rendimiento observable.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(180)]
[AddComponentMenu("Bistro Builder/Staff/Staff Session Service")]
public sealed class BistroBuilderStaffSessionService :
    MonoBehaviour,
    IBistroBuilderStaffSessionAssignmentQuery,
    IBistroBuilderStaffRuntimeMutationGuard
{
    private sealed class RuntimeBinding
    {
        public BistroBuilderStaffSessionBindingRecord record;
        public Waiter waiter;
        public readonly Dictionary<int, WaiterTask> observedTasks =
            new Dictionary<int, WaiterTask>();
        public double observedCycleStartedRealtime = -1d;
    }

    [SerializeField] private BistroBuilderStaffService staffService;
    [SerializeField] private BistroBuilderStaffDevelopmentService developmentService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private RestaurantServiceStateService restaurantServiceStateService;
    [SerializeField] private WaiterTaskCoordinator waiterTaskCoordinator;
    [SerializeField] private BistroBuilderStaffRecruitmentProfile recruitmentProfile;

    [Header("Compatibilidad V1")]
    [Tooltip(
        "Convierte una sola vez los agentes Waiter del prototipo en una " +
        "plantilla generada cuando staff.state todavía está completamente vacío.")]
    [SerializeField] private bool enableLegacyRosterBootstrap = true;

    private BistroBuilderStaffSessionSnapshot sessionState;
    private readonly Dictionary<int, Waiter> waitersById =
        new Dictionary<int, Waiter>();
    private readonly Dictionary<string, RuntimeBinding> bindingsByEmployeeId =
        new Dictionary<string, RuntimeBinding>(StringComparer.Ordinal);
    private readonly Dictionary<int, RuntimeBinding> bindingsByWaiterId =
        new Dictionary<int, RuntimeBinding>();
    private readonly HashSet<Waiter> subscribedWaiters = new HashSet<Waiter>();
    private readonly List<BistroBuilderEmployeeRecord> employeeBuffer =
        new List<BistroBuilderEmployeeRecord>();
    private readonly List<BistroBuilderStaffRoleDefinition> roleBuffer =
        new List<BistroBuilderStaffRoleDefinition>();

    private bool serviceEventsSubscribed;
    private bool hasStarted;
    private bool suspendedForRuntimeLoad;

    public event Action<string> SessionStarted;
    public event Action<string> SessionEnded;
    public event Action<string, int> EmployeeBoundToService;
    public event Action<string, int> EmployeeReleasedFromService;
    public event Action<string> AssignmentChanged;
    public event Action SessionRestored;

    public bool HasActiveSession => sessionState != null && sessionState.active;
    public string ActiveSessionId => HasActiveSession
        ? sessionState.sessionId
        : string.Empty;
    public int BindingCount => bindingsByEmployeeId.Count;

    private void Awake()
    {
        CacheDependencies();
        if (sessionState == null)
        {
            sessionState = BistroBuilderStaffSessionEngine.CreateInactiveSnapshot();
        }
    }

    private void OnEnable()
    {
        CacheDependencies();
        SubscribeServiceEvents();
        TryRegisterMutationGuard();

        if (hasStarted && !suspendedForRuntimeLoad)
        {
            if (!TryRehydrateRuntimeFromCurrentState(out string error))
            {
                Debug.LogError("4D no pudo rehidratar bindings. " + error, this);
            }
        }
    }

    private void Start()
    {
        hasStarted = true;
        if (!ValidateConfiguration(out string error) ||
            !RefreshWaiterIndex(out error))
        {
            Debug.LogError("4D no pudo iniciar. " + error, this);
            return;
        }

        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
        {
            return;
        }

        if (restaurantServiceStateService.IsClosed)
        {
            if (!TrySetAllWaitersEligible(false, out error))
            {
                Debug.LogError(
                    "4D no pudo dejar los agentes en espera. " + error,
                    this);
            }
            return;
        }

        if (!TryEnsureSessionStarted(out error))
        {
            Debug.LogError(
                "4D no pudo reconstruir/iniciar la sesión activa. " + error,
                this);
        }
    }

    private void OnDisable()
    {
        UnsubscribeServiceEvents();
        staffService?.UnregisterRuntimeMutationGuard(this);
        UnsubscribeAllWaiters();

        // Desactivar 4D nunca debe dejar el sistema legacy inutilizable.
        foreach (KeyValuePair<int, Waiter> pair in waitersById)
        {
            pair.Value?.TrySetStaffServiceEligibility(true);
        }
        bindingsByEmployeeId.Clear();
        bindingsByWaiterId.Clear();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (staffService == null || developmentService == null ||
            generalGameStateService == null ||
            restaurantServiceStateService == null ||
            waiterTaskCoordinator == null || recruitmentProfile == null)
        {
            error =
                "4D necesita Staff, Desarrollo, calendario, estado de servicio, " +
                "coordinador de tareas y perfil de contratación.";
            return false;
        }

        if (!staffService.ValidateConfiguration(out error) ||
            !developmentService.ValidateConfiguration(out error) ||
            generalGameStateService.DayIndex < 1 ||
            !recruitmentProfile.TryValidate(staffService.RoleCatalog, out error))
        {
            if (generalGameStateService.DayIndex < 1 &&
                string.IsNullOrWhiteSpace(error))
            {
                error = "El calendario no expone un DayIndex válido.";
            }
            return false;
        }

        if (sessionState != null &&
            !BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                sessionState,
                staffService.CreateSnapshot(),
                out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetActiveAssignment(
        string employeeId,
        out string assignmentReference)
    {
        assignmentReference = string.Empty;
        string normalized = BistroBuilderEmployeeIdUtility.Normalize(employeeId);
        if (!BistroBuilderEmployeeIdUtility.IsValid(normalized) ||
            !bindingsByEmployeeId.TryGetValue(
                normalized,
                out RuntimeBinding binding) ||
            binding == null || binding.waiter == null)
        {
            return false;
        }

        assignmentReference = "waiter:" + binding.waiter.WaiterId;
        return true;
    }

    public bool CanDismissEmployee(string employeeId, out string error)
    {
        if (TryGetActiveAssignment(employeeId, out string assignment))
        {
            error =
                "El empleado está ligado actualmente a " + assignment +
                " y no puede despedirse durante esa sesión.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool CanChangeAvailability(
        string employeeId,
        BistroBuilderEmployeeAvailability requestedAvailability,
        out string error)
    {
        if (!staffService.TryGetEmployee(
                employeeId,
                out BistroBuilderEmployeeRecord employee) ||
            employee == null)
        {
            error = "No existe el empleado solicitado.";
            return false;
        }

        if (employee.availability == requestedAvailability)
        {
            error = string.Empty;
            return true;
        }

        if (TryGetActiveAssignment(employeeId, out string assignment))
        {
            error =
                "La disponibilidad no puede cambiar mientras el empleado " +
                "está ligado a " + assignment + ".";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Inicia la sesión si todavía no existe. Se usa desde Preparing o justo
    /// antes de Open; nunca desde Update.
    /// </summary>
    public bool TryEnsureSessionStarted(out string error)
    {
        if (suspendedForRuntimeLoad ||
            BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
        {
            error = "4D está suspendido durante una reconstrucción de Save/Load.";
            return false;
        }

        if (!ValidateConfiguration(out error) || !RefreshWaiterIndex(out error))
        {
            return false;
        }

        if (HasActiveSession)
        {
            return TryRehydrateRuntimeFromCurrentState(out error);
        }

        if (!EnsureLegacyRosterBootstrap(out error))
        {
            return false;
        }

        employeeBuffer.Clear();
        staffService.CopyEmployees(employeeBuffer, false);
        FilterAndSortWaiterEmployees(employeeBuffer);

        if (waitersById.Count == 0)
        {
            error = "La escena no contiene agentes Waiter operativos.";
            return false;
        }
        if (employeeBuffer.Count == 0)
        {
            error =
                "No hay empleados Camarero/a activos y disponibles para abrir el servicio.";
            return false;
        }

        if (!TrySetAllWaitersEligible(false, out error))
        {
            return false;
        }

        Waiter[] orderedWaiters = BuildOrderedWaiterArray();
        int bindCount = Math.Min(orderedWaiters.Length, employeeBuffer.Count);
        var candidate = new BistroBuilderStaffSessionSnapshot
        {
            schemaId = BistroBuilderStaffSessionSnapshot.CurrentSchemaId,
            schemaVersion = BistroBuilderStaffSessionSnapshot.CurrentSchemaVersion,
            revision = 1L,
            active = true,
            sessionId = BistroBuilderStaffSessionIdUtility.CreateNew(),
            dayIndex = Math.Max(1, generalGameStateService.DayIndex),
            bindings = new List<BistroBuilderStaffSessionBindingRecord>()
        };

        for (int index = 0; index < bindCount; index++)
        {
            candidate.bindings.Add(new BistroBuilderStaffSessionBindingRecord
            {
                employeeId = employeeBuffer[index].employeeId,
                waiterId = orderedWaiters[index].WaiterId,
                completedTasks = 0,
                failedTasks = 0,
                totalTaskDurationMilliseconds = 0L,
                handledTableIds = new List<int>()
            });
        }

        if (!BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                candidate,
                staffService.CreateSnapshot(),
                out error))
        {
            TrySetAllWaitersEligible(true, out _);
            return false;
        }

        sessionState = candidate;
        if (!TryRehydrateRuntimeFromCurrentState(out error))
        {
            sessionState = BistroBuilderStaffSessionEngine.CreateInactiveSnapshot();
            TrySetAllWaitersEligible(true, out _);
            return false;
        }

        SessionStarted?.Invoke(sessionState.sessionId);
        return true;
    }

    /// <summary>
    /// Asignación explícita preparada para Horarios. No cambia tareas ni rutas.
    /// Solo puede utilizar un empleado y agente libres/no ligados.
    /// </summary>
    public bool TryAssignEmployeeToWaiter(
        string employeeId,
        int waiterId,
        out string error)
    {
        if (!HasActiveSession || suspendedForRuntimeLoad)
        {
            error = "No existe una sesión de Personal activa.";
            return false;
        }

        string normalizedEmployee =
            BistroBuilderEmployeeIdUtility.Normalize(employeeId);
        if (!staffService.TryGetEmployee(
                normalizedEmployee,
                out BistroBuilderEmployeeRecord employee) ||
            employee == null ||
            employee.employmentStatus != BistroBuilderEmploymentStatus.Active ||
            employee.availability != BistroBuilderEmployeeAvailability.Available ||
            !IsWaiterRole(employee.roleId))
        {
            error = "El empleado no es un camarero activo y disponible.";
            return false;
        }
        if (bindingsByEmployeeId.ContainsKey(normalizedEmployee))
        {
            error = "El EmployeeId ya está ligado a otro agente.";
            return false;
        }
        if (!waitersById.TryGetValue(waiterId, out Waiter waiter) || waiter == null ||
            bindingsByWaiterId.ContainsKey(waiterId))
        {
            error = "El WaiterId no existe o ya está ligado.";
            return false;
        }
        if (waiter.CurrentState != WaiterState.Idle)
        {
            error = "El agente operativo no está libre para recibir un binding.";
            return false;
        }

        BistroBuilderStaffSessionSnapshot candidate = sessionState.DeepClone();
        candidate.bindings.Add(new BistroBuilderStaffSessionBindingRecord
        {
            employeeId = normalizedEmployee,
            waiterId = waiterId,
            handledTableIds = new List<int>()
        });
        try
        {
            candidate.revision = checked(candidate.revision + 1L);
        }
        catch (OverflowException)
        {
            error = "La revisión de la sesión no puede incrementarse.";
            return false;
        }

        if (!BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                candidate,
                staffService.CreateSnapshot(),
                out error) ||
            !waiter.TrySetStaffServiceEligibility(true))
        {
            return false;
        }

        sessionState = candidate;
        RebuildBindingDictionariesFromState();
        EmployeeBoundToService?.Invoke(normalizedEmployee, waiterId);
        AssignmentChanged?.Invoke(normalizedEmployee);
        error = string.Empty;
        return true;
    }

    public bool TryReleaseEmployeeFromService(
        string employeeId,
        out string error)
    {
        string normalized = BistroBuilderEmployeeIdUtility.Normalize(employeeId);
        if (!HasActiveSession ||
            !bindingsByEmployeeId.TryGetValue(
                normalized,
                out RuntimeBinding binding) ||
            binding == null || binding.waiter == null)
        {
            error = "El empleado no tiene un binding de servicio activo.";
            return false;
        }

        RestaurantServiceState state = restaurantServiceStateService.CurrentState;
        if (state == RestaurantServiceState.Open ||
            state == RestaurantServiceState.Closing)
        {
            error =
                "Un empleado no puede liberarse durante servicio abierto/cierre.";
            return false;
        }
        if (!binding.waiter.IsAvailable)
        {
            error = "El agente todavía está ejecutando una tarea.";
            return false;
        }
        if (sessionState.bindings.Count <= 1)
        {
            error = "La última asignación se libera al cerrar/cancelar la sesión.";
            return false;
        }

        BistroBuilderStaffSessionSnapshot candidate = sessionState.DeepClone();
        RemoveBindingRecord(candidate, normalized);
        try
        {
            candidate.revision = checked(candidate.revision + 1L);
        }
        catch (OverflowException)
        {
            error = "La revisión de la sesión no puede incrementarse.";
            return false;
        }

        if (!BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                candidate,
                staffService.CreateSnapshot(),
                out error) ||
            !binding.waiter.TrySetStaffServiceEligibility(false))
        {
            return false;
        }

        int releasedWaiterId = binding.waiter.WaiterId;
        sessionState = candidate;
        RebuildBindingDictionariesFromState();
        EmployeeReleasedFromService?.Invoke(normalized, releasedWaiterId);
        AssignmentChanged?.Invoke(normalized);
        error = string.Empty;
        return true;
    }

    public bool TryGetAssignmentView(
        string employeeId,
        out BistroBuilderEmployeeSessionAssignmentView view)
    {
        view = null;
        string normalized = BistroBuilderEmployeeIdUtility.Normalize(employeeId);
        if (!bindingsByEmployeeId.TryGetValue(
                normalized,
                out RuntimeBinding binding) ||
            binding == null || binding.waiter == null)
        {
            return false;
        }

        view = new BistroBuilderEmployeeSessionAssignmentView
        {
            employeeId = normalized,
            waiterId = binding.waiter.WaiterId,
            status = binding.waiter.CurrentState == WaiterState.Idle
                ? BistroBuilderEmployeeSessionStatus.Assigned
                : BistroBuilderEmployeeSessionStatus.Working,
            waiterState = binding.waiter.CurrentState,
            completedTasks = binding.record.completedTasks,
            failedTasks = binding.record.failedTasks,
            tablesHandled = binding.record.handledTableIds.Count,
            totalTaskDurationMilliseconds =
                binding.record.totalTaskDurationMilliseconds
        };
        return true;
    }

    public BistroBuilderStaffCoverageSnapshot CreateCoverageSnapshot()
    {
        employeeBuffer.Clear();
        staffService?.CopyEmployees(employeeBuffer, false);

        int activeWaiterEmployees = 0;
        int availableWaiterEmployees = 0;
        for (int index = 0; index < employeeBuffer.Count; index++)
        {
            BistroBuilderEmployeeRecord employee = employeeBuffer[index];
            if (employee == null || !IsWaiterRole(employee.roleId))
            {
                continue;
            }
            activeWaiterEmployees++;
            if (employee.availability == BistroBuilderEmployeeAvailability.Available)
            {
                availableWaiterEmployees++;
            }
        }

        int slots = waitersById.Count;
        int bound = bindingsByEmployeeId.Count;
        return new BistroBuilderStaffCoverageSnapshot
        {
            operationalWaiterSlots = slots,
            activeWaiterEmployees = activeWaiterEmployees,
            availableWaiterEmployees = availableWaiterEmployees,
            boundWaiterEmployees = bound,
            unfilledWaiterSlots = Math.Max(0, slots - bound),
            unassignedAvailableWaiterEmployees =
                Math.Max(0, availableWaiterEmployees - bound),
            hasFullCurrentCoverage = slots > 0 && bound >= slots
        };
    }

    public BistroBuilderStaffSessionSnapshot CreateSessionSnapshot()
    {
        return sessionState != null ? sessionState.DeepClone() : null;
    }

    /// <summary>
    /// 4E lo llamará después de que service.runtime haya limpiado tareas y
    /// asignaciones transitorias (PrepareOrder posterior a 9000).
    /// </summary>
    public bool PrepareForRuntimeLoad(out string error)
    {
        suspendedForRuntimeLoad = true;
        ClearObservedTaskTracking();
        bindingsByEmployeeId.Clear();
        bindingsByWaiterId.Clear();
        if (!RefreshWaiterIndex(out error))
        {
            return false;
        }
        return TrySetAllWaitersEligible(false, out error);
    }

    /// <summary>
    /// Restaura exactamente EmployeeId ↔ WaiterId desde datos validados por
    /// 4E. service.runtime continúa restaurando transform/comandas/tareas.
    /// </summary>
    public bool TryRestoreSessionSnapshot(
        BistroBuilderStaffSessionSnapshot candidate,
        out string error)
    {
        if (candidate == null || !RefreshWaiterIndex(out error))
        {
            if (candidate == null)
            {
                error = "El snapshot de sesión de Personal es nulo.";
            }
            return false;
        }

        if (!BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                candidate,
                staffService.CreateSnapshot(),
                out error))
        {
            return false;
        }

        if (!TrySetAllWaitersEligible(false, out error))
        {
            return false;
        }

        if (candidate.active)
        {
            for (int index = 0; index < candidate.bindings.Count; index++)
            {
                BistroBuilderStaffSessionBindingRecord record =
                    candidate.bindings[index];
                if (!waitersById.TryGetValue(
                        record.waiterId,
                        out Waiter waiter) || waiter == null)
                {
                    error =
                        "staff.session.runtime referencia WaiterId inexistente " +
                        record.waiterId + ".";
                    TrySetAllWaitersEligible(true, out _);
                    return false;
                }
                if (!waiter.TrySetStaffServiceEligibility(true))
                {
                    error =
                        "El WaiterId " + record.waiterId +
                        " no puede activarse durante restauración.";
                    TrySetAllWaitersEligible(true, out _);
                    return false;
                }
            }
        }

        sessionState = candidate.DeepClone();
        RebuildBindingDictionariesFromState();
        suspendedForRuntimeLoad = false;
        SessionRestored?.Invoke();
        error = string.Empty;
        return true;
    }

    public bool TryResumeAfterRuntimeLoad(out string error)
    {
        suspendedForRuntimeLoad = false;
        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        bool serviceActive = restaurantServiceStateService.IsServiceInProgress;
        if (serviceActive != HasActiveSession)
        {
            error =
                "El estado de servicio y staff.session.runtime no coinciden tras Load.";
            return false;
        }

        return TryRehydrateRuntimeFromCurrentState(out error);
    }

    public bool TryFinalizeClosedSession(out string error)
    {
        if (!HasActiveSession)
        {
            error = string.Empty;
            return true;
        }
        if (!restaurantServiceStateService.IsClosed)
        {
            error = "La sesión de Personal solo finaliza cuando el servicio está Closed.";
            return false;
        }

        // Cierra cualquier ciclo observable que ya haya terminado antes del
        // último cambio de estado del camarero.
        foreach (KeyValuePair<string, RuntimeBinding> pair in bindingsByEmployeeId)
        {
            FinalizeObservedWorkCycle(pair.Value);
        }

        // Aplicación reentrante segura: 4C deduplica cada empleado por un
        // operationId derivado de sessionId + EmployeeId.
        for (int index = 0; index < sessionState.bindings.Count; index++)
        {
            BistroBuilderStaffSessionBindingRecord binding =
                sessionState.bindings[index];
            string operationId =
                BistroBuilderStaffSessionEngine.BuildServiceResultOperationId(
                    sessionState.sessionId,
                    binding.employeeId);
            if (!BistroBuilderStaffDevelopmentOperationIdUtility.IsValid(operationId))
            {
                error = "No se pudo construir operationId de rendimiento 4D.";
                return false;
            }

            var report = new BistroBuilderEmployeeServicePerformanceReport
            {
                operationId = operationId,
                serviceCompleted = true,
                completedTasks = binding.completedTasks,
                failedTasks = binding.failedTasks,
                tablesHandled = binding.handledTableIds.Count,
                totalTaskDurationMilliseconds =
                    binding.totalTaskDurationMilliseconds
            };

            if (!developmentService.TryApplyServiceResult(
                    binding.employeeId,
                    report,
                    out _,
                    out _,
                    out error))
            {
                return false;
            }
        }

        string endedSessionId = sessionState.sessionId;
        var released = new List<BistroBuilderStaffSessionBindingRecord>();
        for (int index = 0; index < sessionState.bindings.Count; index++)
        {
            released.Add(sessionState.bindings[index].DeepClone());
        }

        if (!TrySetAllWaitersEligible(false, out error))
        {
            return false;
        }

        sessionState = BistroBuilderStaffSessionEngine.CreateInactiveSnapshot();
        bindingsByEmployeeId.Clear();
        bindingsByWaiterId.Clear();

        for (int index = 0; index < released.Count; index++)
        {
            EmployeeReleasedFromService?.Invoke(
                released[index].employeeId,
                released[index].waiterId);
        }
        SessionEnded?.Invoke(endedSessionId);
        error = string.Empty;
        return true;
    }

    private bool EnsureLegacyRosterBootstrap(out string error)
    {
        if (staffService.OperationalBootstrapCompleted)
        {
            error = string.Empty;
            return true;
        }

        if (!enableLegacyRosterBootstrap || staffService.EmployeeCount > 0)
        {
            return staffService.TryMarkOperationalBootstrapCompleted(out error);
        }

        if (waitersById.Count == 0)
        {
            error = "No existen Waiter legacy con los que inicializar la plantilla.";
            return false;
        }

        if (!TryGetWaiterRoleId(out string waiterRoleId, out error))
        {
            return false;
        }

        BistroBuilderStaffSnapshot rollback = staffService.CreateSnapshot();
        Waiter[] waiters = BuildOrderedWaiterArray();
        long salary = recruitmentProfile.MinimumSalaryCentsPerService +
            (recruitmentProfile.MaximumSalaryCentsPerService -
             recruitmentProfile.MinimumSalaryCentsPerService) / 2L;
        int skill = recruitmentProfile.MinimumSkill +
            (recruitmentProfile.MaximumSkill - recruitmentProfile.MinimumSkill) / 2;

        for (int index = 0; index < waiters.Length; index++)
        {
            string firstName = recruitmentProfile.FirstNames[
                index % recruitmentProfile.FirstNames.Count];
            int lastIndex =
                (index * 7 + index / recruitmentProfile.FirstNames.Count) %
                recruitmentProfile.LastNames.Count;
            string lastName = recruitmentProfile.LastNames[lastIndex];

            var request = new BistroBuilderEmployeeCreateRequest
            {
                firstName = firstName,
                lastName = lastName,
                roleId = waiterRoleId,
                salaryCentsPerService = salary,
                hiredDayIndex = Math.Max(1, generalGameStateService.DayIndex),
                initialExperiencePoints = recruitmentProfile.MinimumExperiencePoints,
                initialSkills = new BistroBuilderEmployeeSkillSet
                {
                    speed = skill,
                    attentiveness = skill,
                    organization = skill,
                    hospitality = skill
                },
                availability = BistroBuilderEmployeeAvailability.Available,
                responsibilities =
                    new BistroBuilderEmployeeResponsibilitySettings()
            };

            if (!staffService.TryCreateEmployee(
                    request,
                    out _,
                    out error))
            {
                staffService.TryRestoreSnapshot(rollback, out _);
                return false;
            }
        }

        if (!staffService.TryMarkOperationalBootstrapCompleted(out error))
        {
            staffService.TryRestoreSnapshot(rollback, out _);
            return false;
        }

        return true;
    }

    private void HandleServiceOpeningRequested()
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring ||
            suspendedForRuntimeLoad)
        {
            return;
        }

        if (!TryEnsureSessionStarted(out string error))
        {
            Debug.LogError(
                "4D no pudo preparar Personal antes de abrir. " + error,
                this);
        }
    }

    private void HandleServiceStateChanged(
        RestaurantServiceState previous,
        RestaurantServiceState next)
    {
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring ||
            suspendedForRuntimeLoad)
        {
            return;
        }

        if (next == RestaurantServiceState.Preparing ||
            next == RestaurantServiceState.Open)
        {
            if (!TryEnsureSessionStarted(out string error))
            {
                Debug.LogError(
                    "4D no pudo iniciar Personal para " + next + ". " + error,
                    this);
            }
            return;
        }

        if (next == RestaurantServiceState.Closed && HasActiveSession &&
            !TryFinalizeClosedSession(out string closeError))
        {
            Debug.LogError(
                "4D no pudo finalizar la sesión de Personal. " + closeError,
                this);
        }
    }

    private void HandleWaiterStateChanged(
        Waiter waiter,
        WaiterState newState)
    {
        if (waiter == null || !HasActiveSession || suspendedForRuntimeLoad ||
            !bindingsByWaiterId.TryGetValue(
                waiter.WaiterId,
                out RuntimeBinding binding) ||
            binding == null)
        {
            return;
        }

        if (newState == WaiterState.Idle)
        {
            FinalizeObservedWorkCycle(binding);

            // Otro listener puede haber reasignado el camarero de forma
            // reentrante durante StateChanged(Idle).
            if (waiter.CurrentState != WaiterState.Idle)
            {
                ObserveAssignedTasks(binding);
            }
            return;
        }

        ObserveAssignedTasks(binding);
    }

    /// <summary>
    /// Consulta ActiveTasks solo como reacción a un evento de Waiter. No hay
    /// polling. Conserva referencias a las tareas ya asignadas para poder leer
    /// su estado terminal incluso después de que la cola las retire.
    /// </summary>
    private void ObserveAssignedTasks(RuntimeBinding binding)
    {
        if (binding == null || binding.waiter == null || waiterTaskCoordinator == null)
        {
            return;
        }

        bool found = false;
        IReadOnlyList<WaiterTask> tasks = waiterTaskCoordinator.ActiveTasks;
        for (int index = 0; index < tasks.Count; index++)
        {
            WaiterTask task = tasks[index];
            if (task == null ||
                !ReferenceEquals(task.AssignedWaiter, binding.waiter) ||
                (task.State != WaiterTaskState.Assigned &&
                 task.State != WaiterTaskState.InProgress))
            {
                continue;
            }

            if (!binding.observedTasks.ContainsKey(task.TaskId))
            {
                binding.observedTasks.Add(task.TaskId, task);
            }
            found = true;
        }

        if (found && binding.observedCycleStartedRealtime < 0d)
        {
            binding.observedCycleStartedRealtime = Time.realtimeSinceStartupAsDouble;
        }
    }

    private void FinalizeObservedWorkCycle(RuntimeBinding binding)
    {
        if (binding == null || binding.record == null ||
            binding.observedTasks.Count == 0)
        {
            if (binding != null)
            {
                binding.observedCycleStartedRealtime = -1d;
            }
            return;
        }

        int completedCount = 0;
        var newTableIds = new HashSet<int>();
        foreach (KeyValuePair<int, WaiterTask> pair in binding.observedTasks)
        {
            WaiterTask task = pair.Value;
            if (task == null || task.State != WaiterTaskState.Completed)
            {
                continue;
            }

            completedCount++;
            if (task.Table != null && task.Table.TableId > 0)
            {
                newTableIds.Add(task.Table.TableId);
            }
        }

        if (completedCount > 0)
        {
            try
            {
                binding.record.completedTasks = checked(
                    binding.record.completedTasks + completedCount);

                if (binding.observedCycleStartedRealtime >= 0d)
                {
                    double seconds = Math.Max(
                        0d,
                        Time.realtimeSinceStartupAsDouble -
                        binding.observedCycleStartedRealtime);
                    long milliseconds = seconds >= long.MaxValue / 1000d
                        ? long.MaxValue
                        : (long)Math.Round(seconds * 1000d);
                    binding.record.totalTaskDurationMilliseconds = checked(
                        binding.record.totalTaskDurationMilliseconds + milliseconds);
                }

                foreach (int tableId in newTableIds)
                {
                    if (!binding.record.handledTableIds.Contains(tableId))
                    {
                        binding.record.handledTableIds.Add(tableId);
                    }
                }
                binding.record.handledTableIds.Sort();
                sessionState.revision = checked(sessionState.revision + 1L);
                AssignmentChanged?.Invoke(binding.record.employeeId);
            }
            catch (OverflowException)
            {
                Debug.LogError(
                    "4D no pudo acumular rendimiento por desbordamiento.",
                    this);
            }
        }

        binding.observedTasks.Clear();
        binding.observedCycleStartedRealtime = -1d;
    }

    private bool TryRehydrateRuntimeFromCurrentState(out string error)
    {
        if (sessionState == null)
        {
            sessionState = BistroBuilderStaffSessionEngine.CreateInactiveSnapshot();
        }

        if (!RefreshWaiterIndex(out error))
        {
            return false;
        }

        if (!BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                sessionState,
                staffService.CreateSnapshot(),
                out error))
        {
            return false;
        }

        bindingsByEmployeeId.Clear();
        bindingsByWaiterId.Clear();

        if (!sessionState.active)
        {
            return TrySetAllWaitersEligible(false, out error);
        }

        if (!TrySetAllWaitersEligible(false, out error))
        {
            return false;
        }

        for (int index = 0; index < sessionState.bindings.Count; index++)
        {
            BistroBuilderStaffSessionBindingRecord record =
                sessionState.bindings[index];
            if (!waitersById.TryGetValue(
                    record.waiterId,
                    out Waiter waiter) || waiter == null)
            {
                error = "No existe WaiterId " + record.waiterId + " para 4D.";
                return false;
            }
            if (!waiter.TrySetStaffServiceEligibility(true))
            {
                error =
                    "WaiterId " + record.waiterId +
                    " está ocupado y no puede rehidratar el binding.";
                return false;
            }

            var runtime = new RuntimeBinding
            {
                record = record,
                waiter = waiter
            };
            bindingsByEmployeeId.Add(
                BistroBuilderEmployeeIdUtility.Normalize(record.employeeId),
                runtime);
            bindingsByWaiterId.Add(record.waiterId, runtime);
            EmployeeBoundToService?.Invoke(record.employeeId, record.waiterId);
        }

        error = string.Empty;
        return true;
    }

    private void RebuildBindingDictionariesFromState()
    {
        bindingsByEmployeeId.Clear();
        bindingsByWaiterId.Clear();
        if (sessionState == null || !sessionState.active)
        {
            return;
        }

        for (int index = 0; index < sessionState.bindings.Count; index++)
        {
            BistroBuilderStaffSessionBindingRecord record = sessionState.bindings[index];
            if (record == null ||
                !waitersById.TryGetValue(record.waiterId, out Waiter waiter) ||
                waiter == null)
            {
                continue;
            }
            var runtime = new RuntimeBinding
            {
                record = record,
                waiter = waiter
            };
            bindingsByEmployeeId[
                BistroBuilderEmployeeIdUtility.Normalize(record.employeeId)] = runtime;
            bindingsByWaiterId[record.waiterId] = runtime;
        }
    }

    private bool RefreshWaiterIndex(out string error)
    {
        Waiter[] sceneWaiters = FindObjectsByType<Waiter>(
            FindObjectsSortMode.InstanceID);
        var current = new HashSet<Waiter>();
        var nextById = new Dictionary<int, Waiter>();

        for (int index = 0; index < sceneWaiters.Length; index++)
        {
            Waiter waiter = sceneWaiters[index];
            if (waiter == null || waiter.WaiterId < 1 ||
                !nextById.TryAdd(waiter.WaiterId, waiter))
            {
                error = "La escena contiene WaiterId inválidos o duplicados.";
                return false;
            }
            current.Add(waiter);
        }

        var stale = new List<Waiter>();
        foreach (Waiter subscribed in subscribedWaiters)
        {
            if (subscribed == null || !current.Contains(subscribed))
            {
                stale.Add(subscribed);
            }
        }
        for (int index = 0; index < stale.Count; index++)
        {
            if (stale[index] != null)
            {
                stale[index].StateChanged -= HandleWaiterStateChanged;
            }
            subscribedWaiters.Remove(stale[index]);
        }

        for (int index = 0; index < sceneWaiters.Length; index++)
        {
            Waiter waiter = sceneWaiters[index];
            if (waiter != null && subscribedWaiters.Add(waiter))
            {
                waiter.StateChanged += HandleWaiterStateChanged;
            }
        }

        waitersById.Clear();
        foreach (KeyValuePair<int, Waiter> pair in nextById)
        {
            waitersById.Add(pair.Key, pair.Value);
        }
        error = string.Empty;
        return true;
    }

    private bool TrySetAllWaitersEligible(bool eligible, out string error)
    {
        foreach (KeyValuePair<int, Waiter> pair in waitersById)
        {
            Waiter waiter = pair.Value;
            if (waiter != null && !waiter.TrySetStaffServiceEligibility(eligible))
            {
                error =
                    "WaiterId " + pair.Key +
                    " está ocupado y no puede cambiar elegibilidad.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    private Waiter[] BuildOrderedWaiterArray()
    {
        var result = new List<Waiter>(waitersById.Values);
        result.Sort((left, right) => left.WaiterId.CompareTo(right.WaiterId));
        return result.ToArray();
    }

    private void FilterAndSortWaiterEmployees(
        List<BistroBuilderEmployeeRecord> employees)
    {
        for (int index = employees.Count - 1; index >= 0; index--)
        {
            BistroBuilderEmployeeRecord employee = employees[index];
            if (employee == null ||
                employee.employmentStatus != BistroBuilderEmploymentStatus.Active ||
                employee.availability != BistroBuilderEmployeeAvailability.Available ||
                !IsWaiterRole(employee.roleId))
            {
                employees.RemoveAt(index);
            }
        }

        employees.Sort((left, right) =>
        {
            int day = left.hiredDayIndex.CompareTo(right.hiredDayIndex);
            if (day != 0)
            {
                return day;
            }
            return string.CompareOrdinal(left.employeeId, right.employeeId);
        });
    }

    private bool IsWaiterRole(string roleId)
    {
        return staffService != null &&
               staffService.TryGetRoleDefinition(
                   roleId,
                   out BistroBuilderStaffRoleDefinition role) &&
               role != null && role.active &&
               string.Equals(
                   BistroBuilderStaffStableIdUtility.Normalize(
                       role.operationalAdapterId),
                   BistroBuilderStaffOperationalAdapterIds.WaiterAgent,
                   StringComparison.Ordinal);
    }

    private bool TryGetWaiterRoleId(out string roleId, out string error)
    {
        roleId = string.Empty;
        roleBuffer.Clear();
        staffService.RoleCatalog.CopyRoles(roleBuffer);
        for (int index = 0; index < roleBuffer.Count; index++)
        {
            BistroBuilderStaffRoleDefinition role = roleBuffer[index];
            if (role != null && role.active &&
                string.Equals(
                    BistroBuilderStaffStableIdUtility.Normalize(
                        role.operationalAdapterId),
                    BistroBuilderStaffOperationalAdapterIds.WaiterAgent,
                    StringComparison.Ordinal))
            {
                roleId = BistroBuilderStaffStableIdUtility.Normalize(role.roleId);
                error = string.Empty;
                return true;
            }
        }

        error = "El catálogo no contiene un rol activo para waiter.agent.";
        return false;
    }

    private static void RemoveBindingRecord(
        BistroBuilderStaffSessionSnapshot snapshot,
        string employeeId)
    {
        string normalized = BistroBuilderEmployeeIdUtility.Normalize(employeeId);
        for (int index = snapshot.bindings.Count - 1; index >= 0; index--)
        {
            BistroBuilderStaffSessionBindingRecord record = snapshot.bindings[index];
            if (record != null && string.Equals(
                    BistroBuilderEmployeeIdUtility.Normalize(record.employeeId),
                    normalized,
                    StringComparison.Ordinal))
            {
                snapshot.bindings.RemoveAt(index);
                return;
            }
        }
    }

    private void ClearObservedTaskTracking()
    {
        foreach (KeyValuePair<string, RuntimeBinding> pair in bindingsByEmployeeId)
        {
            if (pair.Value == null)
            {
                continue;
            }
            pair.Value.observedTasks.Clear();
            pair.Value.observedCycleStartedRealtime = -1d;
        }
    }

    private void SubscribeServiceEvents()
    {
        if (serviceEventsSubscribed || restaurantServiceStateService == null)
        {
            return;
        }
        restaurantServiceStateService.ServiceOpeningRequested +=
            HandleServiceOpeningRequested;
        restaurantServiceStateService.StateChanged += HandleServiceStateChanged;
        serviceEventsSubscribed = true;
    }

    private void UnsubscribeServiceEvents()
    {
        if (!serviceEventsSubscribed || restaurantServiceStateService == null)
        {
            return;
        }
        restaurantServiceStateService.ServiceOpeningRequested -=
            HandleServiceOpeningRequested;
        restaurantServiceStateService.StateChanged -= HandleServiceStateChanged;
        serviceEventsSubscribed = false;
    }

    private void TryRegisterMutationGuard()
    {
        if (staffService != null &&
            !staffService.TryRegisterRuntimeMutationGuard(this, out string error))
        {
            Debug.LogError("4D no pudo registrar su guardia. " + error, this);
        }
    }

    private void UnsubscribeAllWaiters()
    {
        foreach (Waiter waiter in subscribedWaiters)
        {
            if (waiter != null)
            {
                waiter.StateChanged -= HandleWaiterStateChanged;
            }
        }
        subscribedWaiters.Clear();
    }

    private void CacheDependencies()
    {
        if (staffService == null) TryGetComponent(out staffService);
        if (developmentService == null) TryGetComponent(out developmentService);
        if (generalGameStateService == null)
            TryGetComponent(out generalGameStateService);
        if (restaurantServiceStateService == null)
            TryGetComponent(out restaurantServiceStateService);
        if (waiterTaskCoordinator == null)
            TryGetComponent(out waiterTaskCoordinator);
        if (recruitmentProfile == null)
        {
            recruitmentProfile = Resources.Load<BistroBuilderStaffRecruitmentProfile>(
                "BistroBuilder/Staff/StaffRecruitmentProfile");
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
