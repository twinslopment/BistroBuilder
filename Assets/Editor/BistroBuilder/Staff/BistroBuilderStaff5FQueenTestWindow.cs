using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 5F — Queen Test reversible de Horarios y Turnos.
/// Usa solo autoridades reales. No crea Waiter, WaiterTask, clientes ni métricas.
/// </summary>
public sealed class BistroBuilderStaff5FQueenTestWindow : EditorWindow
{
    private enum Phase
    {
        Idle,
        SavingRollback,
        SavingOpen,
        WaitingOpenMutation,
        LoadingOpen,
        WaitingClose,
        SavingClosed,
        LoadingClosed,
        LoadingRollbackSuccess,
        LoadingRollbackFailure,
        DeletingCheckpointSuccess,
        DeletingCheckpointFailure,
        DeletingRollbackSuccess,
        DeletingRollbackFailure,
        Completed,
        Failed
    }

    private const float MutationTimeout = 180f;
    private const float CloseTimeout = 180f;

    private Phase phase;
    private Vector2 scroll;
    private string report = "Entra en Play Mode y ejecuta primero el preflight 5F.";
    private MessageType reportType = MessageType.Info;
    private float deadline;
    private bool subscribed;
    private int rollbackSlot = -1;
    private int checkpointSlot = -1;
    private string pendingFailure = string.Empty;

    private BistroBuilderSaveGameService save;
    private BistroBuilderStaffService staff;
    private BistroBuilderStaffScheduleService schedule;
    private BistroBuilderStaffScheduleSessionBridge bridge;
    private BistroBuilderStaffSessionService session;
    private BistroBuilderStaffSchedulePlayerFacade facade;
    private BistroBuilderStaffSchedulePlayerScreen screen;
    private RestaurantServiceStateService serviceState;
    private BistroBuilderCanonicalOrderIntegrationService orderIntegration;
    private WaiterTaskCoordinator waiterCoordinator;

    private string initialStaffJson = string.Empty;
    private string initialScheduleJson = string.Empty;
    private string initialSessionJson = string.Empty;
    private RestaurantServiceState initialServiceState;
    private int initialWaiterCount;

    private string targetEmployeeId = string.Empty;
    private int targetWaiterId;
    private int targetDay;
    private BistroBuilderMealServiceAvailability targetMealService;
    private int openCompletedTasks;
    private string openScheduleJson = string.Empty;
    private string openSessionJson = string.Empty;
    private string closedScheduleJson = string.Empty;

    private readonly List<BistroBuilderEmployeeRecord> employees =
        new List<BistroBuilderEmployeeRecord>();
    private readonly List<string> oneEmployee = new List<string>(1);

    [MenuItem(
        "Tools/Bistro Builder/Personal/5F - QUEEN TEST reversible horarios",
        false,
        3292)]
    private static void Open()
    {
        GetWindow<BistroBuilderStaff5FQueenTestWindow>("Queen Horarios 5F");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "5F — QUEEN TEST REVERSIBLE DE HORARIOS",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Planifica un Employee real, filtra 4D mediante 5C, prueba Save/Load Open " +
            "después de una mutación operativa real, prueba staff.schedule A→B→Load→A " +
            "en Closed y restaura la partida completa.",
            MessageType.Info);

        bool canRun = EditorApplication.isPlaying &&
            (phase == Phase.Idle || phase == Phase.Completed || phase == Phase.Failed);
        using (new EditorGUI.DisabledScope(!canRun))
        {
            if (GUILayout.Button("EJECUTAR QUEEN TEST 5F", GUILayout.Height(38f)))
                Begin();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(report, reportType);
        EditorGUILayout.EndScrollView();
    }

    private void Begin()
    {
        ResetRun();
        if (!Resolve(out string error) || !ValidatePreconditions(out error))
        {
            FailImmediate(error);
            return;
        }

        if (!FindTwoFreeSlots(out rollbackSlot, out checkpointSlot))
        {
            FailImmediate("Se necesitan dos slots libres entre 970 y 979.");
            return;
        }

        CaptureInitialState();
        Subscribe();
        phase = Phase.SavingRollback;
        SetReport("Guardando rollback integral 5F...", MessageType.Info);
        if (!save.TrySaveSlot(
                rollbackSlot,
                "BB 5F SCHEDULE QUEEN ROLLBACK",
                out string rejection))
        {
            FailImmediate("No pudo iniciarse rollback: " + rejection);
        }
    }

    private bool ValidatePreconditions(out string error)
    {
        save.RefreshExtensions();
        if (!save.ValidateConfiguration(out error) ||
            !staff.ValidateConfiguration(out error) ||
            !schedule.ValidateConfiguration(out error) ||
            !bridge.ValidateConfiguration(out error) ||
            !session.ValidateConfiguration(out error) ||
            !facade.ValidateConfiguration(out error) ||
            !screen.ValidateConfiguration(out error) ||
            !orderIntegration.ValidateConfiguration(out error))
            return false;

        if (!save.HasProvider(BistroBuilderStaffScheduleSaveSectionProvider.StableSectionId))
        {
            error = "SaveGame no registra staff.schedule.";
            return false;
        }
        if (!serviceState.IsClosed || session.HasActiveSession)
        {
            error = "5F debe comenzar en Closed y sin sesión 4D activa.";
            return false;
        }
        if (orderIntegration.CurrentMealService == BistroBuilderMealServiceAvailability.None)
        {
            error = "No existe un servicio gastronómico concreto para planificar.";
            return false;
        }
        if (!TryChooseTargetEmployee(out _, out error))
            return false;
        if (CurrentWaiterCount() < 1)
        {
            error = "No existen agentes Waiter reales.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void HandleOperation(BistroBuilderSaveOperationResult result)
    {
        if (result == null)
            return;

        if (phase == Phase.SavingRollback && result.SlotIndex == rollbackSlot)
        {
            if (!result.Succeeded)
            {
                FailImmediate("Rollback no guardado: " + result.Message);
                return;
            }
            ExecutePlannedService();
            return;
        }

        if (phase == Phase.SavingOpen && result.SlotIndex == checkpointSlot)
        {
            if (!result.Succeeded)
            {
                FailAndRollback("Checkpoint Open falló: " + result.Message);
                return;
            }
            deadline = Time.realtimeSinceStartup + MutationTimeout;
            phase = Phase.WaitingOpenMutation;
            SetReport(
                "Checkpoint Open guardado; esperando mutación operativa real...",
                MessageType.Info);
            return;
        }

        if (phase == Phase.LoadingOpen && result.SlotIndex == checkpointSlot)
        {
            if (!result.Succeeded)
            {
                FailAndRollback("Load Open falló: " + result.Message);
                return;
            }
            ValidateOpenLoadAndClose();
            return;
        }

        if (phase == Phase.SavingClosed && result.SlotIndex == checkpointSlot)
        {
            if (!result.Succeeded)
            {
                FailAndRollback("Checkpoint Closed falló: " + result.Message);
                return;
            }
            MutateClosedScheduleAndLoad();
            return;
        }

        if (phase == Phase.LoadingClosed && result.SlotIndex == checkpointSlot)
        {
            if (!result.Succeeded)
            {
                FailAndRollback("Load Closed falló: " + result.Message);
                return;
            }
            ValidateClosedLoad();
            return;
        }

        if ((phase == Phase.LoadingRollbackSuccess ||
             phase == Phase.LoadingRollbackFailure) &&
            result.SlotIndex == rollbackSlot)
        {
            bool failure = phase == Phase.LoadingRollbackFailure;
            if (!result.Succeeded)
            {
                CompleteFailure(
                    PrefixFailure(failure) +
                    "Además falló rollback: " + result.Message);
                return;
            }

            // Secuencial a propósito: evita variables out potencialmente no
            // asignadas por short-circuit y produce un diagnóstico inequívoco.
            if (!Resolve(out string resolveError))
            {
                CompleteFailure(PrefixFailure(failure) + resolveError);
                return;
            }
            if (!ValidateRollbackRestored(out string restoreError))
            {
                CompleteFailure(PrefixFailure(failure) + restoreError);
                return;
            }

            DeleteCheckpoint(failure);
            return;
        }

        if ((phase == Phase.DeletingCheckpointSuccess ||
             phase == Phase.DeletingCheckpointFailure) &&
            result.SlotIndex == checkpointSlot)
        {
            bool failure = phase == Phase.DeletingCheckpointFailure;
            if (!result.Succeeded)
            {
                pendingFailure = PrefixFailure(failure) +
                    "No se pudo eliminar checkpoint: " + result.Message;
                DeleteRollback(true);
                return;
            }
            DeleteRollback(failure);
            return;
        }

        if ((phase == Phase.DeletingRollbackSuccess ||
             phase == Phase.DeletingRollbackFailure) &&
            result.SlotIndex == rollbackSlot)
        {
            bool failure = phase == Phase.DeletingRollbackFailure;
            if (!result.Succeeded)
            {
                CompleteFailure(
                    PrefixFailure(failure) +
                    "No se pudo eliminar rollback: " + result.Message);
                return;
            }
            if (failure) CompleteFailure(pendingFailure);
            else CompleteSuccess();
        }
    }

    private void ExecutePlannedService()
    {
        if (!Resolve(out string error) ||
            !TryChooseTargetEmployee(out targetEmployeeId, out error))
        {
            FailAndRollback(error);
            return;
        }

        targetDay = schedule.CurrentDayIndex;
        targetMealService = orderIntegration.CurrentMealService;
        oneEmployee.Clear();
        oneEmployee.Add(targetEmployeeId);

        if (!schedule.TryReplaceServiceAssignments(
                targetDay,
                targetMealService,
                oneEmployee,
                out error))
        {
            FailAndRollback("No pudo planificarse el turno: " + error);
            return;
        }

        openScheduleJson = JsonUtility.ToJson(schedule.CreateSnapshot());
        if (!schedule.TryBuildCoverage(
                targetDay,
                targetMealService,
                out BistroBuilderStaffScheduleCoverage coverage,
                out error) ||
            coverage == null ||
            coverage.scheduledWaiters != 1 ||
            coverage.projectedSalaryCents <= 0L)
        {
            FailAndRollback("Cobertura prevista incoherente: " + error);
            return;
        }

        screen.Show();
        if (!screen.IsVisible)
        {
            FailAndRollback("La pantalla 5E no se abrió en Play Mode.");
            return;
        }
        screen.Hide();

        if (!serviceState.TryBeginPreparation())
        {
            FailAndRollback("No pudo iniciar Preparing desde Closed.");
            return;
        }
        if (!session.TryEnsureSessionStarted(out error))
        {
            FailAndRollback("4D no pudo iniciar sesión: " + error);
            return;
        }
        if (!bridge.IsCurrentSessionAligned(out error))
        {
            FailAndRollback("5C no alineó 4D con el turno: " + error);
            return;
        }

        if (!session.TryGetAssignmentView(
                targetEmployeeId,
                out BistroBuilderEmployeeSessionAssignmentView assignment) ||
            assignment == null ||
            session.BindingCount != 1)
        {
            FailAndRollback(
                "El turno no produjo exactamente un binding EmployeeId ↔ WaiterId.");
            return;
        }

        targetWaiterId = assignment.waiterId;
        openCompletedTasks = assignment.completedTasks;
        if (!serviceState.TryOpenService())
        {
            FailAndRollback("No pudo abrir el servicio planificado.");
            return;
        }

        openSessionJson = JsonUtility.ToJson(session.CreateSessionSnapshot());
        phase = Phase.SavingOpen;
        SetReport(
            "Turno aplicado a Waiter real. Guardando checkpoint Open...",
            MessageType.Info);
        if (!save.TrySaveSlot(
                checkpointSlot,
                "BB 5F SCHEDULE OPEN CHECKPOINT",
                out string rejection))
        {
            FailAndRollback("No pudo iniciar checkpoint Open: " + rejection);
        }
    }

    private void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            if (phase != Phase.Idle &&
                phase != Phase.Completed &&
                phase != Phase.Failed)
                CompleteFailure("Play Mode terminó durante 5F.");
            return;
        }

        if (phase == Phase.WaitingOpenMutation)
        {
            if (!Resolve(out string error))
            {
                FailAndRollback(error);
                return;
            }

            session.TryGetAssignmentView(
                targetEmployeeId,
                out BistroBuilderEmployeeSessionAssignmentView assignment);
            if (BistroBuilderStaff4GNaturalMutationProbe.HasObservableMutation(
                    openSessionJson,
                    openCompletedTasks,
                    session.CreateSessionSnapshot(),
                    assignment,
                    out string evidence))
            {
                phase = Phase.LoadingOpen;
                SetReport(
                    "Mutación real confirmada: " + evidence +
                    " Cargando checkpoint Open...",
                    MessageType.Info);
                if (!save.TryLoadSlot(checkpointSlot, out string rejection))
                    FailAndRollback("No pudo iniciar Load Open: " + rejection);
                return;
            }

            if (Time.realtimeSinceStartup >= deadline)
                FailAndRollback(
                    "Timeout esperando mutación operativa real antes de Load Open.");
            return;
        }

        if (phase == Phase.WaitingClose)
        {
            if (!Resolve(out string error))
            {
                FailAndRollback(error);
                return;
            }

            if (waiterCoordinator.ActiveTaskCount == 0 && AreBoundWaitersIdle())
            {
                if (!serviceState.TryCompleteClosing())
                {
                    FailAndRollback("No pudo completar Closing → Closed.");
                    return;
                }
                SaveClosedCheckpoint();
                return;
            }

            if (Time.realtimeSinceStartup >= deadline)
                FailAndRollback("Timeout esperando cierre operativo limpio.");
        }
    }

    private void ValidateOpenLoadAndClose()
    {
        if (!Resolve(out string error))
        {
            FailAndRollback(error);
            return;
        }

        if (serviceState.CurrentState != RestaurantServiceState.Open ||
            CurrentWaiterCount() != initialWaiterCount ||
            !JsonEquals(openScheduleJson, schedule.CreateSnapshot()) ||
            !JsonEquals(openSessionJson, session.CreateSessionSnapshot()) ||
            !bridge.IsCurrentSessionAligned(out error) ||
            !session.TryGetAssignmentView(
                targetEmployeeId,
                out BistroBuilderEmployeeSessionAssignmentView assignment) ||
            assignment == null ||
            assignment.waiterId != targetWaiterId)
        {
            FailAndRollback(
                "Load Open no restauró horario/binding/servicio exactamente. " + error);
            return;
        }

        if (!serviceState.TryBeginClosing())
        {
            FailAndRollback("No pudo iniciar Closing tras Load Open.");
            return;
        }

        deadline = Time.realtimeSinceStartup + CloseTimeout;
        phase = Phase.WaitingClose;
        SetReport(
            "Load Open correcto. Esperando cierre operativo limpio...",
            MessageType.Info);
    }

    private void SaveClosedCheckpoint()
    {
        if (!serviceState.IsClosed || session.HasActiveSession)
        {
            FailAndRollback(
                "El cierre no dejó service Closed y sesión 4D inactiva.");
            return;
        }

        closedScheduleJson = JsonUtility.ToJson(schedule.CreateSnapshot());
        phase = Phase.SavingClosed;
        SetReport("Guardando checkpoint Closed del horario...", MessageType.Info);
        if (!save.TrySaveSlot(
                checkpointSlot,
                "BB 5F SCHEDULE CLOSED CHECKPOINT",
                out string rejection))
        {
            FailAndRollback("No pudo guardar checkpoint Closed: " + rejection);
        }
    }

    private void MutateClosedScheduleAndLoad()
    {
        if (!Resolve(out string error))
        {
            FailAndRollback(error);
            return;
        }

        if (!schedule.TrySetScheduled(
                targetEmployeeId,
                targetDay,
                targetMealService,
                false,
                out error))
        {
            FailAndRollback("No pudo crear el estado B del horario: " + error);
            return;
        }

        string mutated = JsonUtility.ToJson(schedule.CreateSnapshot());
        if (string.Equals(mutated, closedScheduleJson, StringComparison.Ordinal))
        {
            FailAndRollback("staff.schedule B no diverge del checkpoint A.");
            return;
        }

        phase = Phase.LoadingClosed;
        SetReport(
            "A ≠ B confirmado. Cargando checkpoint Closed A...",
            MessageType.Info);
        if (!save.TryLoadSlot(checkpointSlot, out string rejection))
            FailAndRollback("No pudo iniciar Load Closed: " + rejection);
    }

    private void ValidateClosedLoad()
    {
        if (!Resolve(out string error))
        {
            FailAndRollback(error);
            return;
        }

        if (!serviceState.IsClosed ||
            session.HasActiveSession ||
            !JsonEquals(closedScheduleJson, schedule.CreateSnapshot()) ||
            !schedule.IsScheduled(targetEmployeeId, targetDay, targetMealService))
        {
            FailAndRollback("Load Closed no restauró exactamente el plan A.");
            return;
        }

        BeginRollback(false);
    }

    private bool TryChooseTargetEmployee(out string employeeId, out string error)
    {
        employeeId = string.Empty;
        employees.Clear();
        staff.CopyEmployees(employees, false);
        employees.Sort((left, right) => string.Compare(
            left?.employeeId,
            right?.employeeId,
            StringComparison.Ordinal));

        foreach (BistroBuilderEmployeeRecord employee in employees)
        {
            if (employee == null ||
                employee.employmentStatus != BistroBuilderEmploymentStatus.Active ||
                employee.availability != BistroBuilderEmployeeAvailability.Available ||
                !staff.TryGetRoleDefinition(
                    employee.roleId,
                    out BistroBuilderStaffRoleDefinition role) ||
                role == null ||
                !string.Equals(
                    role.operationalAdapterId,
                    BistroBuilderStaffOperationalAdapterIds.WaiterAgent,
                    StringComparison.Ordinal))
                continue;

            employeeId = employee.employeeId;
            error = string.Empty;
            return true;
        }

        error = "No existe un camarero Employee activo y disponible.";
        return false;
    }

    private void CaptureInitialState()
    {
        initialStaffJson = JsonUtility.ToJson(staff.CreateSnapshot());
        initialScheduleJson = JsonUtility.ToJson(schedule.CreateSnapshot());
        initialSessionJson = JsonUtility.ToJson(session.CreateSessionSnapshot());
        initialServiceState = serviceState.CurrentState;
        initialWaiterCount = CurrentWaiterCount();
    }

    private bool ValidateRollbackRestored(out string error)
    {
        if (serviceState.CurrentState != initialServiceState ||
            CurrentWaiterCount() != initialWaiterCount ||
            !JsonEquals(initialStaffJson, staff.CreateSnapshot()) ||
            !JsonEquals(initialScheduleJson, schedule.CreateSnapshot()) ||
            !JsonEquals(initialSessionJson, session.CreateSessionSnapshot()))
        {
            error =
                "Rollback no restituyó Staff, Schedule, Session, servicio y Waiter count.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool AreBoundWaitersIdle()
    {
        BistroBuilderStaffSessionSnapshot snapshot = session.CreateSessionSnapshot();
        if (snapshot == null || snapshot.bindings == null)
            return false;

        Waiter[] waiters = UnityEngine.Object.FindObjectsByType<Waiter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var byId = new Dictionary<int, Waiter>();
        foreach (Waiter waiter in waiters)
            if (waiter != null) byId[waiter.WaiterId] = waiter;

        foreach (BistroBuilderStaffSessionBindingRecord binding in snapshot.bindings)
        {
            if (binding == null ||
                !byId.TryGetValue(binding.waiterId, out Waiter waiter) ||
                waiter == null ||
                waiter.CurrentState != WaiterState.Idle)
                return false;
        }
        return true;
    }

    private bool Resolve(out string error)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            error = "No hay escena activa cargada.";
            return false;
        }

        save = Unique<BistroBuilderSaveGameService>(scene);
        staff = Unique<BistroBuilderStaffService>(scene);
        schedule = Unique<BistroBuilderStaffScheduleService>(scene);
        bridge = Unique<BistroBuilderStaffScheduleSessionBridge>(scene);
        session = Unique<BistroBuilderStaffSessionService>(scene);
        facade = Unique<BistroBuilderStaffSchedulePlayerFacade>(scene);
        screen = Unique<BistroBuilderStaffSchedulePlayerScreen>(scene);
        serviceState = Unique<RestaurantServiceStateService>(scene);
        orderIntegration = Unique<BistroBuilderCanonicalOrderIntegrationService>(scene);
        waiterCoordinator = Unique<WaiterTaskCoordinator>(scene);

        if (save == null || staff == null || schedule == null || bridge == null ||
            session == null || facade == null || screen == null ||
            serviceState == null || orderIntegration == null || waiterCoordinator == null)
        {
            error =
                "5F necesita una única autoridad Save/Staff/Schedule/Bridge/Session/" +
                "UI/Service/Orders/WaiterTaskCoordinator.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static T Unique<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        T found = null;
        int count = 0;
        foreach (T value in all)
        {
            if (value != null && value.gameObject.scene == scene)
            {
                found = value;
                count++;
            }
        }
        return count == 1 ? found : null;
    }

    private bool FindTwoFreeSlots(out int first, out int second)
    {
        first = -1;
        second = -1;
        for (int slot = 970; slot <= 979; slot++)
        {
            if (save.SlotExists(slot)) continue;
            if (first < 0) first = slot;
            else
            {
                second = slot;
                return true;
            }
        }
        return false;
    }

    private static bool JsonEquals(string expected, object current)
    {
        return current != null && string.Equals(
            expected,
            JsonUtility.ToJson(current),
            StringComparison.Ordinal);
    }

    private int CurrentWaiterCount()
    {
        return UnityEngine.Object.FindObjectsByType<Waiter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length;
    }

    private void FailAndRollback(string message)
    {
        pendingFailure = message;
        BeginRollback(true);
    }

    private void BeginRollback(bool failure)
    {
        phase = failure
            ? Phase.LoadingRollbackFailure
            : Phase.LoadingRollbackSuccess;
        SetReport(
            failure
                ? "Fallo detectado; restaurando rollback: " + pendingFailure
                : "Queen flow validado; restaurando rollback integral...",
            failure ? MessageType.Warning : MessageType.Info);

        if (!save.TryLoadSlot(rollbackSlot, out string rejection))
            CompleteFailure(
                PrefixFailure(failure) +
                "No pudo iniciarse rollback: " + rejection);
    }

    private void DeleteCheckpoint(bool failure)
    {
        phase = failure
            ? Phase.DeletingCheckpointFailure
            : Phase.DeletingCheckpointSuccess;
        if (!save.SlotExists(checkpointSlot))
        {
            DeleteRollback(failure);
            return;
        }
        if (!save.TryDeleteSlot(checkpointSlot, out string rejection))
        {
            pendingFailure = PrefixFailure(failure) +
                "No pudo borrar checkpoint: " + rejection;
            DeleteRollback(true);
        }
    }

    private void DeleteRollback(bool failure)
    {
        phase = failure
            ? Phase.DeletingRollbackFailure
            : Phase.DeletingRollbackSuccess;
        if (!save.SlotExists(rollbackSlot))
        {
            if (failure) CompleteFailure(pendingFailure);
            else CompleteSuccess();
            return;
        }
        if (!save.TryDeleteSlot(rollbackSlot, out string rejection))
            CompleteFailure(
                PrefixFailure(failure) +
                "No pudo borrar rollback: " + rejection);
    }

    private string PrefixFailure(bool failure)
    {
        return failure && !string.IsNullOrWhiteSpace(pendingFailure)
            ? pendingFailure + " "
            : string.Empty;
    }

    private void Subscribe()
    {
        Unsubscribe();
        save.OperationCompleted += HandleOperation;
        EditorApplication.update += Tick;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (save != null) save.OperationCompleted -= HandleOperation;
        EditorApplication.update -= Tick;
        subscribed = false;
    }

    private void ResetRun()
    {
        Unsubscribe();
        phase = Phase.Idle;
        rollbackSlot = -1;
        checkpointSlot = -1;
        pendingFailure = string.Empty;
        targetEmployeeId = string.Empty;
        targetWaiterId = 0;
        openCompletedTasks = 0;
        reportType = MessageType.Info;
    }

    private void SetReport(string message, MessageType type)
    {
        report = message;
        reportType = type;
        Repaint();
    }

    private void FailImmediate(string message)
    {
        Unsubscribe();
        phase = Phase.Failed;
        SetReport("5F — FAIL\n" + message, MessageType.Error);
        Debug.LogError(report);
    }

    private void CompleteFailure(string message)
    {
        Unsubscribe();
        phase = Phase.Failed;
        SetReport("5F — FAIL\n" + message, MessageType.Error);
        Debug.LogError(report);
    }

    private void CompleteSuccess()
    {
        Unsubscribe();
        phase = Phase.Completed;
        SetReport(
            "5F — QUEEN FLOW COMPLETADO\n" +
            "Turno EmployeeId planificado\n" +
            "Cobertura/coste coherentes\n" +
            "Binding 5C → 4D → WaiterId " + targetWaiterId + "\n" +
            "Save/Load Open tras mutación operativa real\n" +
            "staff.schedule A → B → Load → A en Closed\n" +
            "Rollback integral y limpieza de slots\n\n" +
            "Pendiente compilación, UI, Console y ejecución real antes de cerrar Bloque 5.",
            MessageType.Info);
        Debug.Log(report);
    }
}
