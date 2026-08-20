using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 4G — Queen Test reversible de Personal.
///
/// Usa exclusivamente las autoridades reales ya instaladas: SaveGame universal,
/// fachada 4F, estado de servicio, StaffSessionService y WaiterTaskCoordinator.
/// No fabrica tareas, clientes, XP ni métricas. Para validar rendimiento espera
/// trabajo REAL del servicio y solo entonces continúa con Save/Load activo.
/// </summary>
public sealed class BistroBuilderStaff4GQueenTestWindow : EditorWindow
{
    private enum Phase
    {
        Idle,
        SavingRollback,
        WaitingRealWork,
        SavingActiveCheckpoint,
        WaitingNaturalMutation,
        LoadingActiveCheckpoint,
        WaitingCloseReady,
        SavingClosedCheckpoint,
        LoadingClosedCheckpoint,
        LoadingRollbackSuccess,
        LoadingRollbackFailure,
        DeletingCheckpointSuccess,
        DeletingCheckpointFailure,
        DeletingRollbackSuccess,
        DeletingRollbackFailure,
        Completed,
        Failed
    }

    private const float RealWorkTimeoutSeconds = 180f;
    private const float NaturalMutationWaitSeconds = 3f;
    private const float CloseTimeoutSeconds = 180f;

    private Vector2 scroll;
    private string report =
        "Entra en Play Mode, ejecuta primero el preflight 4G y después este Queen Test.";
    private MessageType reportType = MessageType.Info;
    private Phase phase;

    private BistroBuilderSaveGameService save;
    private BistroBuilderStaffService staff;
    private BistroBuilderStaffRecruitmentService recruitment;
    private BistroBuilderStaffDevelopmentService development;
    private BistroBuilderStaffSessionService session;
    private BistroBuilderStaffPlayerFacade facade;
    private BistroBuilderStaffPlayerScreen screen;
    private RestaurantServiceStateService serviceState;
    private WaiterTaskCoordinator waiterCoordinator;

    private int rollbackSlot = -1;
    private int checkpointSlot = -1;
    private float deadline;
    private string pendingFailure = string.Empty;
    private bool subscribed;

    private string initialStaffJson = string.Empty;
    private string initialMarketJson = string.Empty;
    private string initialSessionJson = string.Empty;
    private RestaurantServiceState initialServiceState;
    private int initialWaiterCount;

    private string targetCandidateId = string.Empty;
    private string targetEmployeeId = string.Empty;
    private string targetTrainingId = string.Empty;
    private long targetExperienceBeforeService;
    private int targetCompletedServicesBefore;

    private string activeCheckpointStaffJson = string.Empty;
    private string activeCheckpointMarketJson = string.Empty;
    private string activeCheckpointSessionJson = string.Empty;
    private int activeCheckpointCompletedTasks;
    private int activeCheckpointWaiterId;

    private string closedCheckpointStaffJson = string.Empty;
    private string closedCheckpointMarketJson = string.Empty;
    private string closedCheckpointSessionJson = string.Empty;

    private readonly List<BistroBuilderEmployeeRecord> employeeBuffer =
        new List<BistroBuilderEmployeeRecord>();
    private readonly List<BistroBuilderStaffCandidateRecord> candidateBuffer =
        new List<BistroBuilderStaffCandidateRecord>();

    [MenuItem(
        "Tools/Bistro Builder/Personal/4G - QUEEN TEST reversible",
        false,
        3261)]
    private static void Open()
    {
        GetWindow<BistroBuilderStaff4GQueenTestWindow>("Queen Test 4G");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "4G — QUEEN TEST REVERSIBLE DE PERSONAL",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "La prueba guarda primero rollback integral, contrata y forma a un " +
            "candidato real, fuerza una cobertura de un solo empleado mediante " +
            "disponibilidad canónica, abre servicio REAL, espera tareas reales, " +
            "valida Save/Load activo, cierra, verifica XP/rendimiento e " +
            "idempotencia, valida Save/Load cerrado y restaura el rollback.",
            MessageType.Info);

        bool canRun = EditorApplication.isPlaying &&
                      (phase == Phase.Idle ||
                       phase == Phase.Completed ||
                       phase == Phase.Failed);
        using (new EditorGUI.DisabledScope(!canRun))
        {
            if (GUILayout.Button(
                    "EJECUTAR QUEEN TEST 4G",
                    GUILayout.Height(38f)))
            {
                Begin();
            }
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(report, reportType);
        EditorGUILayout.EndScrollView();
    }

    private void Begin()
    {
        ResetRun();

        if (!Resolve(out string error) ||
            !ValidatePreconditions(out error))
        {
            FailImmediate(error);
            return;
        }

        if (!FindTwoFreeSlots(out rollbackSlot, out checkpointSlot))
        {
            FailImmediate("Se necesitan dos slots libres entre 980 y 989.");
            return;
        }

        CaptureInitialState();
        Subscribe();
        phase = Phase.SavingRollback;
        report =
            "Guardando rollback integral en slot " + rollbackSlot + "...";
        Repaint();

        if (!save.TrySaveSlot(
                rollbackSlot,
                "BB 4G STAFF QUEEN ROLLBACK",
                out string rejection))
        {
            FailImmediate("No se pudo iniciar rollback: " + rejection);
        }
    }

    private bool ValidatePreconditions(out string error)
    {
        if (!save.ValidateConfiguration(out error) ||
            !staff.ValidateConfiguration(out error) ||
            !recruitment.ValidateConfiguration(out error) ||
            !development.ValidateConfiguration(out error) ||
            !session.ValidateConfiguration(out error) ||
            !facade.ValidateConfiguration(out error) ||
            !screen.ValidateConfiguration(out error))
        {
            return false;
        }

        save.RefreshExtensions();
        if (!save.HasProvider(BistroBuilderStaffStateSaveSectionProvider.StableSectionId) ||
            !save.HasProvider(BistroBuilderStaffRecruitmentSaveSectionProvider.StableSectionId) ||
            !save.HasProvider(BistroBuilderStaffSessionSaveSectionProvider.StableSectionId))
        {
            error = "SaveGame universal no registra las tres secciones 4E.";
            return false;
        }

        if (!serviceState.IsClosed || session.HasActiveSession)
        {
            error =
                "El Queen Test debe comenzar con servicio Closed y sin sesión 4D activa.";
            return false;
        }

        if (!recruitment.EnsureMarketReady(out error) ||
            recruitment.CandidateCount < 1)
        {
            return false;
        }

        if (CurrentWaiterCount() < 1)
        {
            error = "No existen agentes Waiter reales para ejecutar el Queen Test.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void HandleSaveOperation(BistroBuilderSaveOperationResult result)
    {
        if (result == null)
        {
            return;
        }

        if (phase == Phase.SavingRollback && result.SlotIndex == rollbackSlot)
        {
            if (!result.Succeeded)
            {
                FailImmediate("No pudo guardarse rollback: " + result.Message);
                return;
            }
            ExecutePreServiceMutations();
            return;
        }

        if (phase == Phase.SavingActiveCheckpoint &&
            result.SlotIndex == checkpointSlot)
        {
            if (!result.Succeeded)
            {
                FailAndRollback(
                    "No pudo guardarse checkpoint activo: " + result.Message);
                return;
            }

            deadline = Time.realtimeSinceStartup + NaturalMutationWaitSeconds;
            phase = Phase.WaitingNaturalMutation;
            report =
                "Checkpoint activo guardado. Dejando que el servicio evolucione " +
                "de forma natural antes de cargarlo...";
            Repaint();
            return;
        }

        if (phase == Phase.LoadingActiveCheckpoint &&
            result.SlotIndex == checkpointSlot)
        {
            if (!result.Succeeded)
            {
                FailAndRollback(
                    "No pudo cargarse checkpoint activo: " + result.Message);
                return;
            }
            ValidateActiveCheckpointAfterLoad();
            return;
        }

        if (phase == Phase.SavingClosedCheckpoint &&
            result.SlotIndex == checkpointSlot)
        {
            if (!result.Succeeded)
            {
                FailAndRollback(
                    "No pudo guardarse checkpoint cerrado: " + result.Message);
                return;
            }

            phase = Phase.LoadingClosedCheckpoint;
            report = "Cargando checkpoint cerrado para validar persistencia final...";
            Repaint();
            if (!save.TryLoadSlot(checkpointSlot, out string rejection))
            {
                FailAndRollback(
                    "No pudo iniciarse carga cerrada: " + rejection);
            }
            return;
        }

        if (phase == Phase.LoadingClosedCheckpoint &&
            result.SlotIndex == checkpointSlot)
        {
            if (!result.Succeeded)
            {
                FailAndRollback(
                    "No pudo cargarse checkpoint cerrado: " + result.Message);
                return;
            }
            ValidateClosedCheckpointAfterLoad();
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
                    (failure ? pendingFailure + " " : string.Empty) +
                    "Además falló el rollback: " + result.Message);
                return;
            }

            if (!Resolve(out string resolveError) ||
                !ValidateRollbackRestored(out string restoreError))
            {
                CompleteFailure(
                    (failure ? pendingFailure + " " : string.Empty) +
                    "Rollback cargado pero no verificable: " +
                    (string.IsNullOrWhiteSpace(resolveError)
                        ? restoreError
                        : resolveError));
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
                pendingFailure =
                    (failure ? pendingFailure + " " : string.Empty) +
                    "No se pudo eliminar checkpoint diagnóstico: " + result.Message;
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
                    (failure ? pendingFailure + " " : string.Empty) +
                    "No se pudo eliminar rollback diagnóstico: " + result.Message);
                return;
            }

            if (failure)
            {
                CompleteFailure(pendingFailure);
            }
            else
            {
                CompleteSuccess();
            }
        }
    }

    private void ExecutePreServiceMutations()
    {
        if (!Resolve(out string error) ||
            !recruitment.EnsureMarketReady(out error))
        {
            FailAndRollback(error);
            return;
        }

        screen.Show();
        if (!screen.IsVisible)
        {
            FailAndRollback("La pantalla 4F no pudo abrirse en Play Mode.");
            return;
        }
        screen.Hide();

        candidateBuffer.Clear();
        recruitment.CopyCandidates(candidateBuffer);
        if (candidateBuffer.Count == 0 || candidateBuffer[0] == null)
        {
            FailAndRollback("El mercado no contiene candidato utilizable.");
            return;
        }

        targetCandidateId = candidateBuffer[0].candidateId;
        int waiterCountBeforeHire = CurrentWaiterCount();
        int employeeCountBeforeHire = staff.EmployeeCount;

        if (!facade.TryHireCandidate(
                targetCandidateId,
                out BistroBuilderEmployeeRecord hired,
                out error) ||
            hired == null ||
            !BistroBuilderEmployeeIdUtility.IsValid(hired.employeeId))
        {
            FailAndRollback("Contratación 4B falló: " + error);
            return;
        }

        targetEmployeeId = hired.employeeId;
        if (staff.EmployeeCount != employeeCountBeforeHire + 1 ||
            CurrentWaiterCount() != waiterCountBeforeHire ||
            recruitment.TryGetCandidate(targetCandidateId, out _))
        {
            FailAndRollback(
                "La contratación no respetó EmployeeId nuevo / CandidateId retirado / Waiter estable.");
            return;
        }

        if (!facade.TrySetAvailability(
                targetEmployeeId,
                BistroBuilderEmployeeAvailability.Unavailable,
                out _,
                out error) ||
            !facade.TrySetAvailability(
                targetEmployeeId,
                BistroBuilderEmployeeAvailability.Available,
                out _,
                out error))
        {
            FailAndRollback("Cambio reversible de disponibilidad falló: " + error);
            return;
        }

        if (!TryTrainTarget(out error))
        {
            FailAndRollback(error);
            return;
        }

        if (!MakeTargetOnlyAvailableWaiterEmployee(out error))
        {
            FailAndRollback(error);
            return;
        }

        if (!serviceState.TryBeginPreparation())
        {
            FailAndRollback("No pudo iniciarse Preparing desde Closed.");
            return;
        }
        if (!session.TryEnsureSessionStarted(out error))
        {
            FailAndRollback("4D no pudo iniciar sesión: " + error);
            return;
        }
        if (!session.TryGetAssignmentView(
                targetEmployeeId,
                out BistroBuilderEmployeeSessionAssignmentView assignment) ||
            assignment == null)
        {
            FailAndRollback(
                "El empleado contratado no quedó ligado a un Waiter real.");
            return;
        }

        activeCheckpointWaiterId = assignment.waiterId;
        if (!serviceState.TryOpenService())
        {
            FailAndRollback("No pudo abrirse servicio real desde Preparing.");
            return;
        }

        if (!staff.TryGetEmployee(
                targetEmployeeId,
                out BistroBuilderEmployeeRecord beforeService) ||
            beforeService == null)
        {
            FailAndRollback("No pudo releerse el empleado antes del servicio.");
            return;
        }

        targetExperienceBeforeService = beforeService.experiencePoints;
        targetCompletedServicesBefore = beforeService.performance.completedServices;
        deadline = Time.realtimeSinceStartup + RealWorkTimeoutSeconds;
        phase = Phase.WaitingRealWork;
        report =
            "Servicio abierto con el empleado contratado como único camarero " +
            "disponible. Esperando una tarea REAL completada (sin inyección diagnóstica)...";
        Repaint();
    }

    private bool TryTrainTarget(out string error)
    {
        error = string.Empty;
        BistroBuilderStaffDevelopmentProfile profile =
            development.DevelopmentProfile;
        if (profile == null || profile.Trainings == null)
        {
            error = "4C no expone perfil/formaciones V1.";
            return false;
        }

        for (int index = 0; index < profile.Trainings.Count; index++)
        {
            BistroBuilderStaffTrainingDefinition training = profile.Trainings[index];
            if (training == null || training.financialCostCents != 0L)
            {
                continue;
            }

            if (facade.TryTrainEmployee(
                    targetEmployeeId,
                    training.trainingId,
                    out _,
                    out BistroBuilderEmployeeTrainingResult result,
                    out string trainingError) &&
                result != null && !result.wasReplayed && result.skillGained > 0)
            {
                targetTrainingId = training.trainingId;
                return true;
            }

            error = trainingError;
        }

        if (string.IsNullOrWhiteSpace(error))
        {
            error = "No existe formación V1 gratuita elegible para el nuevo empleado.";
        }
        return false;
    }

    private bool MakeTargetOnlyAvailableWaiterEmployee(out string error)
    {
        employeeBuffer.Clear();
        staff.CopyEmployees(employeeBuffer, false);
        for (int index = 0; index < employeeBuffer.Count; index++)
        {
            BistroBuilderEmployeeRecord employee = employeeBuffer[index];
            if (employee == null ||
                string.Equals(
                    employee.employeeId,
                    targetEmployeeId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!staff.TryGetRoleDefinition(
                    employee.roleId,
                    out BistroBuilderStaffRoleDefinition role) ||
                role == null ||
                !string.Equals(
                    role.operationalAdapterId,
                    BistroBuilderStaffOperationalAdapterIds.WaiterAgent,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (employee.availability == BistroBuilderEmployeeAvailability.Available &&
                !facade.TrySetAvailability(
                    employee.employeeId,
                    BistroBuilderEmployeeAvailability.Unavailable,
                    out _,
                    out error))
            {
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            if (phase != Phase.Idle &&
                phase != Phase.Completed &&
                phase != Phase.Failed)
            {
                CompleteFailure("Play Mode terminó durante el Queen Test 4G.");
            }
            return;
        }

        if (phase == Phase.WaitingRealWork)
        {
            if (!Resolve(out string error))
            {
                FailAndRollback(error);
                return;
            }

            if (session.TryGetAssignmentView(
                    targetEmployeeId,
                    out BistroBuilderEmployeeSessionAssignmentView view) &&
                view != null && view.completedTasks > 0)
            {
                activeCheckpointCompletedTasks = view.completedTasks;
                CaptureActiveCheckpointState();
                phase = Phase.SavingActiveCheckpoint;
                report =
                    "Trabajo real observado: " + view.completedTasks +
                    " tarea(s). Guardando checkpoint con servicio Open...";
                Repaint();
                if (!save.TrySaveSlot(
                        checkpointSlot,
                        "BB 4G STAFF ACTIVE CHECKPOINT",
                        out string rejection))
                {
                    FailAndRollback(
                        "No pudo iniciarse checkpoint activo: " + rejection);
                }
                return;
            }

            if (Time.realtimeSinceStartup >= deadline)
            {
                FailAndRollback(
                    "Timeout esperando una tarea real completada por el empleado objetivo.");
            }
            return;
        }

        if (phase == Phase.WaitingNaturalMutation)
        {
            if (Time.realtimeSinceStartup < deadline)
            {
                return;
            }

            phase = Phase.LoadingActiveCheckpoint;
            report =
                "Cargando checkpoint activo. La evolución posterior se descartará mediante SaveGame real...";
            Repaint();
            if (!save.TryLoadSlot(checkpointSlot, out string rejection))
            {
                FailAndRollback(
                    "No pudo iniciarse carga del checkpoint activo: " + rejection);
            }
            return;
        }

        if (phase == Phase.WaitingCloseReady)
        {
            if (!Resolve(out string error))
            {
                FailAndRollback(error);
                return;
            }

            if (waiterCoordinator.ActiveTaskCount == 0 &&
                AreBoundWaitersIdle())
            {
                if (!serviceState.TryCompleteClosing())
                {
                    FailAndRollback("No pudo completarse Closing -> Closed.");
                    return;
                }
                ValidateClosedSessionThenSave();
                return;
            }

            if (Time.realtimeSinceStartup >= deadline)
            {
                FailAndRollback(
                    "Timeout esperando que tareas y camareros quedaran libres para cerrar.");
            }
        }
    }

    private void ValidateActiveCheckpointAfterLoad()
    {
        if (!Resolve(out string error))
        {
            FailAndRollback(error);
            return;
        }

        if (serviceState.CurrentState != RestaurantServiceState.Open ||
            CurrentWaiterCount() != initialWaiterCount ||
            !JsonEquals(activeCheckpointStaffJson, staff.CreateSnapshot()) ||
            !JsonEquals(activeCheckpointMarketJson, recruitment.CreateMarketSnapshot()) ||
            !JsonEquals(activeCheckpointSessionJson, session.CreateSessionSnapshot()) ||
            !session.TryGetAssignmentView(
                targetEmployeeId,
                out BistroBuilderEmployeeSessionAssignmentView assignment) ||
            assignment == null ||
            assignment.waiterId != activeCheckpointWaiterId ||
            assignment.completedTasks != activeCheckpointCompletedTasks)
        {
            FailAndRollback(
                "Save/Load activo no restauró exactamente staff/mercado/binding/estado de servicio.");
            return;
        }

        if (!serviceState.TryBeginClosing())
        {
            FailAndRollback("No pudo iniciarse cierre después del Load activo.");
            return;
        }

        deadline = Time.realtimeSinceStartup + CloseTimeoutSeconds;
        phase = Phase.WaitingCloseReady;
        report =
            "Checkpoint activo restaurado correctamente. Servicio en Closing; " +
            "esperando que el runtime real termine sus tareas antes de cerrar...";
        Repaint();
    }

    private void ValidateClosedSessionThenSave()
    {
        if (session.HasActiveSession)
        {
            FailAndRollback("4D conserva sesión activa después de Closed.");
            return;
        }

        if (!staff.TryGetEmployee(
                targetEmployeeId,
                out BistroBuilderEmployeeRecord employee) ||
            employee == null ||
            employee.experiencePoints <= targetExperienceBeforeService ||
            employee.performance.completedServices !=
                targetCompletedServicesBefore + 1 ||
            employee.performance.completedTasks < activeCheckpointCompletedTasks)
        {
            FailAndRollback(
                "El cierre no consolidó XP/rendimiento real exactamente una vez.");
            return;
        }

        string beforeReplay = JsonUtility.ToJson(staff.CreateSnapshot());
        if (!session.TryFinalizeClosedSession(out string error) ||
            !string.Equals(
                beforeReplay,
                JsonUtility.ToJson(staff.CreateSnapshot()),
                StringComparison.Ordinal))
        {
            FailAndRollback(
                "La segunda finalización no fue idempotente: " + error);
            return;
        }

        CaptureClosedCheckpointState();
        phase = Phase.SavingClosedCheckpoint;
        report =
            "Cierre + XP + rendimiento validados. Guardando checkpoint Closed...";
        Repaint();
        if (!save.TrySaveSlot(
                checkpointSlot,
                "BB 4G STAFF CLOSED CHECKPOINT",
                out string rejection))
        {
            FailAndRollback(
                "No pudo iniciarse checkpoint cerrado: " + rejection);
        }
    }

    private void ValidateClosedCheckpointAfterLoad()
    {
        if (!Resolve(out string error) ||
            serviceState.CurrentState != RestaurantServiceState.Closed ||
            session.HasActiveSession ||
            !JsonEquals(closedCheckpointStaffJson, staff.CreateSnapshot()) ||
            !JsonEquals(closedCheckpointMarketJson, recruitment.CreateMarketSnapshot()) ||
            !JsonEquals(closedCheckpointSessionJson, session.CreateSessionSnapshot()))
        {
            FailAndRollback(
                string.IsNullOrWhiteSpace(error)
                    ? "Save/Load cerrado no restauró Personal exactamente."
                    : error);
            return;
        }

        BeginRollback(false);
    }

    private void CaptureInitialState()
    {
        initialStaffJson = JsonUtility.ToJson(staff.CreateSnapshot());
        initialMarketJson = JsonUtility.ToJson(recruitment.CreateMarketSnapshot());
        initialSessionJson = JsonUtility.ToJson(session.CreateSessionSnapshot());
        initialServiceState = serviceState.CurrentState;
        initialWaiterCount = CurrentWaiterCount();
    }

    private void CaptureActiveCheckpointState()
    {
        activeCheckpointStaffJson = JsonUtility.ToJson(staff.CreateSnapshot());
        activeCheckpointMarketJson = JsonUtility.ToJson(recruitment.CreateMarketSnapshot());
        activeCheckpointSessionJson = JsonUtility.ToJson(session.CreateSessionSnapshot());
    }

    private void CaptureClosedCheckpointState()
    {
        closedCheckpointStaffJson = JsonUtility.ToJson(staff.CreateSnapshot());
        closedCheckpointMarketJson = JsonUtility.ToJson(recruitment.CreateMarketSnapshot());
        closedCheckpointSessionJson = JsonUtility.ToJson(session.CreateSessionSnapshot());
    }

    private bool ValidateRollbackRestored(out string error)
    {
        if (serviceState.CurrentState != initialServiceState ||
            CurrentWaiterCount() != initialWaiterCount ||
            !JsonEquals(initialStaffJson, staff.CreateSnapshot()) ||
            !JsonEquals(initialMarketJson, recruitment.CreateMarketSnapshot()) ||
            !JsonEquals(initialSessionJson, session.CreateSessionSnapshot()))
        {
            error =
                "El rollback no restituyó exactamente staff, mercado, sesión, servicio y Waiter count.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool AreBoundWaitersIdle()
    {
        BistroBuilderStaffSessionSnapshot snapshot = session.CreateSessionSnapshot();
        if (snapshot == null || snapshot.bindings == null)
        {
            return false;
        }

        Waiter[] waiters = UnityEngine.Object.FindObjectsByType<Waiter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var byId = new Dictionary<int, Waiter>();
        for (int index = 0; index < waiters.Length; index++)
        {
            if (waiters[index] != null)
            {
                byId[waiters[index].WaiterId] = waiters[index];
            }
        }

        for (int index = 0; index < snapshot.bindings.Count; index++)
        {
            int waiterId = snapshot.bindings[index].waiterId;
            if (!byId.TryGetValue(waiterId, out Waiter waiter) ||
                waiter == null || waiter.CurrentState != WaiterState.Idle)
            {
                return false;
            }
        }
        return true;
    }

    private static bool JsonEquals(string expectedJson, object current)
    {
        return current != null &&
               string.Equals(
                   expectedJson,
                   JsonUtility.ToJson(current),
                   StringComparison.Ordinal);
    }

    private int CurrentWaiterCount()
    {
        return UnityEngine.Object.FindObjectsByType<Waiter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length;
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
        recruitment = Unique<BistroBuilderStaffRecruitmentService>(scene);
        development = Unique<BistroBuilderStaffDevelopmentService>(scene);
        session = Unique<BistroBuilderStaffSessionService>(scene);
        facade = Unique<BistroBuilderStaffPlayerFacade>(scene);
        screen = Unique<BistroBuilderStaffPlayerScreen>(scene);
        serviceState = Unique<RestaurantServiceStateService>(scene);
        waiterCoordinator = Unique<WaiterTaskCoordinator>(scene);

        if (save == null || staff == null || recruitment == null ||
            development == null || session == null || facade == null ||
            screen == null || serviceState == null || waiterCoordinator == null)
        {
            error =
                "4G necesita una única autoridad Save/Staff/Recruitment/Development/" +
                "Session/Facade/Screen/ServiceState/WaiterTaskCoordinator.";
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
        for (int index = 0; index < all.Length; index++)
        {
            if (all[index] != null && all[index].gameObject.scene == scene)
            {
                found = all[index];
                count++;
            }
        }
        return count == 1 ? found : null;
    }

    private bool FindTwoFreeSlots(out int first, out int second)
    {
        first = -1;
        second = -1;
        for (int slot = 980; slot <= 989; slot++)
        {
            if (save.SlotExists(slot))
            {
                continue;
            }
            if (first < 0)
            {
                first = slot;
            }
            else
            {
                second = slot;
                return true;
            }
        }
        return false;
    }

    private void FailAndRollback(string message)
    {
        if (phase == Phase.LoadingRollbackFailure ||
            phase == Phase.DeletingCheckpointFailure ||
            phase == Phase.DeletingRollbackFailure)
        {
            CompleteFailure(message);
            return;
        }

        pendingFailure = message;
        BeginRollback(true);
    }

    private void BeginRollback(bool failure)
    {
        phase = failure
            ? Phase.LoadingRollbackFailure
            : Phase.LoadingRollbackSuccess;
        report = failure
            ? "Fallo detectado. Restaurando rollback integral: " + pendingFailure
            : "Queen flow validado. Restaurando rollback integral inicial...";
        reportType = failure ? MessageType.Warning : MessageType.Info;
        Repaint();

        if (!save.TryLoadSlot(rollbackSlot, out string rejection))
        {
            CompleteFailure(
                (failure ? pendingFailure + " " : string.Empty) +
                "No pudo iniciarse rollback: " + rejection);
        }
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
            pendingFailure =
                (failure ? pendingFailure + " " : string.Empty) +
                "No pudo iniciarse borrado de checkpoint: " + rejection;
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
        {
            CompleteFailure(
                (failure ? pendingFailure + " " : string.Empty) +
                "No pudo iniciarse borrado de rollback: " + rejection);
        }
    }

    private void Subscribe()
    {
        Unsubscribe();
        save.OperationCompleted += HandleSaveOperation;
        EditorApplication.update += Tick;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }
        if (save != null)
        {
            save.OperationCompleted -= HandleSaveOperation;
        }
        EditorApplication.update -= Tick;
        subscribed = false;
    }

    private void ResetRun()
    {
        Unsubscribe();
        phase = Phase.Idle;
        reportType = MessageType.Info;
        pendingFailure = string.Empty;
        rollbackSlot = -1;
        checkpointSlot = -1;
        targetCandidateId = string.Empty;
        targetEmployeeId = string.Empty;
        targetTrainingId = string.Empty;
        initialStaffJson = string.Empty;
        initialMarketJson = string.Empty;
        initialSessionJson = string.Empty;
        activeCheckpointStaffJson = string.Empty;
        activeCheckpointMarketJson = string.Empty;
        activeCheckpointSessionJson = string.Empty;
        closedCheckpointStaffJson = string.Empty;
        closedCheckpointMarketJson = string.Empty;
        closedCheckpointSessionJson = string.Empty;
    }

    private void FailImmediate(string message)
    {
        Unsubscribe();
        phase = Phase.Failed;
        report = "4G — FAIL\n" + message;
        reportType = MessageType.Error;
        Debug.LogError(report);
        Repaint();
    }

    private void CompleteFailure(string message)
    {
        Unsubscribe();
        phase = Phase.Failed;
        report = "4G — FAIL\n" + message;
        reportType = MessageType.Error;
        Debug.LogError(report);
        Repaint();
    }

    private void CompleteSuccess()
    {
        Unsubscribe();
        phase = Phase.Completed;
        report =
            "4G — QUEEN FLOW COMPLETADO\n" +
            "• contratación real + EmployeeId estable\n" +
            "• disponibilidad reversible\n" +
            "• formación V1 gratuita: " + targetTrainingId + "\n" +
            "• binding real EmployeeId ↔ WaiterId " + activeCheckpointWaiterId + "\n" +
            "• trabajo real observado: " + activeCheckpointCompletedTasks + " tarea(s)\n" +
            "• Save/Load con servicio activo\n" +
            "• cierre + XP/rendimiento idempotente\n" +
            "• Save/Load con servicio cerrado\n" +
            "• rollback integral verificado\n" +
            "• slots diagnósticos eliminados\n\n" +
            "Este resultado todavía debe revisarse junto con compilación, logs " +
            "y prueba visual antes de cerrar el Bloque 4.";
        reportType = MessageType.Info;
        Debug.Log(report);
        Repaint();
    }
}
