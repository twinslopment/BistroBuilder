using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 4G — Queen Test reversible de Personal.
///
/// La prueba usa solo autoridades reales: SaveGame, Staff, Recruitment,
/// Development, StaffSession, Presentation, RestaurantServiceStateService y
/// WaiterTaskCoordinator. No crea tareas, clientes, XP ni métricas de prueba.
/// Espera trabajo real del servicio antes de validar persistencia activa.
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
    private const float NaturalMutationTimeoutSeconds = 60f;
    private const float CloseTimeoutSeconds = 180f;

    private Vector2 scroll;
    private string report =
        "Entra en Play Mode y ejecuta antes el preflight 4G.";
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
    private int targetWaiterId;
    private int activeCheckpointCompletedTasks;

    private string activeStaffJson = string.Empty;
    private string activeMarketJson = string.Empty;
    private string activeSessionJson = string.Empty;
    private string closedStaffJson = string.Empty;
    private string closedMarketJson = string.Empty;
    private string closedSessionJson = string.Empty;

    private readonly List<BistroBuilderEmployeeRecord> employees =
        new List<BistroBuilderEmployeeRecord>();
    private readonly List<BistroBuilderStaffCandidateRecord> candidates =
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
            "Guarda rollback integral, contrata y forma a un candidato real, " +
            "abre un servicio real con ese empleado como único camarero " +
            "disponible, espera trabajo real, prueba Save/Load activo, cierra, " +
            "verifica XP/rendimiento, prueba Save/Load cerrado y restaura todo.",
            MessageType.Info);

        bool canRun = EditorApplication.isPlaying &&
                      (phase == Phase.Idle ||
                       phase == Phase.Completed ||
                       phase == Phase.Failed);
        using (new EditorGUI.DisabledScope(!canRun))
        {
            if (GUILayout.Button("EJECUTAR QUEEN TEST 4G", GUILayout.Height(38f)))
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
        if (!Resolve(out string error) || !ValidatePreconditions(out error))
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
        SetReport(
            "Guardando rollback integral en slot " + rollbackSlot + "...",
            MessageType.Info);

        if (!save.TrySaveSlot(
                rollbackSlot,
                "BB 4G STAFF QUEEN ROLLBACK",
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
            !recruitment.ValidateConfiguration(out error) ||
            !development.ValidateConfiguration(out error) ||
            !session.ValidateConfiguration(out error) ||
            !facade.ValidateConfiguration(out error) ||
            !screen.ValidateConfiguration(out error))
        {
            return false;
        }

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
                "4G debe comenzar en Closed y sin staff.session.runtime activo.";
            return false;
        }

        if (!recruitment.EnsureMarketReady(out error) ||
            recruitment.CandidateCount < 1)
        {
            return false;
        }

        if (CurrentWaiterCount() < 1)
        {
            error = "No existen agentes Waiter reales para 4D.";
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
                FailImmediate("Rollback no guardado: " + result.Message);
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
                FailAndRollback("Checkpoint activo falló: " + result.Message);
                return;
            }
            deadline = Time.realtimeSinceStartup + NaturalMutationTimeoutSeconds;
            phase = Phase.WaitingNaturalMutation;
            SetReport(
                "Checkpoint activo guardado; esperando una mutación observable " +
                "del runtime real antes del Load...",
                MessageType.Info);
            return;
        }

        if (phase == Phase.LoadingActiveCheckpoint &&
            result.SlotIndex == checkpointSlot)
        {
            if (!result.Succeeded)
            {
                FailAndRollback("Load activo falló: " + result.Message);
                return;
            }
            ValidateActiveLoadAndBeginClosing();
            return;
        }

        if (phase == Phase.SavingClosedCheckpoint &&
            result.SlotIndex == checkpointSlot)
        {
            if (!result.Succeeded)
            {
                FailAndRollback("Checkpoint Closed falló: " + result.Message);
                return;
            }
            phase = Phase.LoadingClosedCheckpoint;
            SetReport("Cargando checkpoint Closed...", MessageType.Info);
            if (!save.TryLoadSlot(checkpointSlot, out string rejection))
            {
                FailAndRollback("No pudo iniciarse Load Closed: " + rejection);
            }
            return;
        }

        if (phase == Phase.LoadingClosedCheckpoint &&
            result.SlotIndex == checkpointSlot)
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

            string resolveError;
            if (!Resolve(out resolveError))
            {
                CompleteFailure(
                    PrefixFailure(failure) +
                    "Rollback cargado pero no se resolvieron dependencias: " +
                    resolveError);
                return;
            }

            string restoreError;
            if (!ValidateRollbackRestored(out restoreError))
            {
                CompleteFailure(
                    PrefixFailure(failure) + restoreError);
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

    private void ExecutePreServiceMutations()
    {
        if (!Resolve(out string error) ||
            !recruitment.EnsureMarketReady(out error))
        {
            FailAndRollback(error);
            return;
        }

        // Comprueba la vista real 4F sin usarla como autoridad.
        screen.Show();
        if (!screen.IsVisible)
        {
            FailAndRollback("La pantalla 4F no se abrió en Play Mode.");
            return;
        }
        screen.Hide();

        candidates.Clear();
        recruitment.CopyCandidates(candidates);
        if (candidates.Count == 0 || candidates[0] == null)
        {
            FailAndRollback("El mercado no contiene candidato utilizable.");
            return;
        }

        targetCandidateId = candidates[0].candidateId;
        int waiterCountBeforeHire = CurrentWaiterCount();
        int employeeCountBeforeHire = staff.EmployeeCount;

        BistroBuilderEmployeeRecord hired;
        if (!facade.TryHireCandidate(
                targetCandidateId,
                out hired,
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
                "4B no mantuvo CandidateId retirado, EmployeeId nuevo y Waiter count estable.");
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
            FailAndRollback("Disponibilidad reversible falló: " + error);
            return;
        }

        if (!TrainTarget(out error) ||
            !MakeTargetOnlyAvailableWaiter(out error))
        {
            FailAndRollback(error);
            return;
        }

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

        BistroBuilderEmployeeSessionAssignmentView assignment;
        if (!session.TryGetAssignmentView(
                targetEmployeeId,
                out assignment) || assignment == null)
        {
            FailAndRollback("El empleado contratado no quedó ligado a Waiter real.");
            return;
        }
        targetWaiterId = assignment.waiterId;

        if (!serviceState.TryOpenService())
        {
            FailAndRollback("No pudo abrir servicio desde Preparing.");
            return;
        }

        BistroBuilderEmployeeRecord beforeService;
        if (!staff.TryGetEmployee(
                targetEmployeeId,
                out beforeService) || beforeService == null)
        {
            FailAndRollback("No pudo releerse empleado antes del servicio.");
            return;
        }

        targetExperienceBeforeService = beforeService.experiencePoints;
        targetCompletedServicesBefore = beforeService.performance.completedServices;
        deadline = Time.realtimeSinceStartup + RealWorkTimeoutSeconds;
        phase = Phase.WaitingRealWork;
        SetReport(
            "Servicio Open. Esperando una tarea REAL completada por el empleado objetivo...",
            MessageType.Info);
    }

    private bool TrainTarget(out string error)
    {
        error = string.Empty;
        BistroBuilderStaffDevelopmentProfile profile = development.DevelopmentProfile;
        if (profile == null || profile.Trainings == null)
        {
            error = "4C no expone formaciones V1.";
            return false;
        }

        for (int index = 0; index < profile.Trainings.Count; index++)
        {
            BistroBuilderStaffTrainingDefinition training = profile.Trainings[index];
            if (training == null || training.financialCostCents != 0L)
            {
                continue;
            }

            BistroBuilderEmployeeTrainingResult result;
            string trainingError;
            if (facade.TryTrainEmployee(
                    targetEmployeeId,
                    training.trainingId,
                    out _,
                    out result,
                    out trainingError) &&
                result != null &&
                !result.wasReplayed &&
                result.skillGained > 0)
            {
                targetTrainingId = training.trainingId;
                return true;
            }
            error = trainingError;
        }

        if (string.IsNullOrWhiteSpace(error))
        {
            error = "No existe formación gratuita elegible para el nuevo empleado.";
        }
        return false;
    }

    private bool MakeTargetOnlyAvailableWaiter(out string error)
    {
        employees.Clear();
        staff.CopyEmployees(employees, false);
        for (int index = 0; index < employees.Count; index++)
        {
            BistroBuilderEmployeeRecord employee = employees[index];
            if (employee == null ||
                string.Equals(
                    employee.employeeId,
                    targetEmployeeId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            BistroBuilderStaffRoleDefinition role;
            if (!staff.TryGetRoleDefinition(employee.roleId, out role) ||
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
                CompleteFailure("Play Mode terminó durante 4G.");
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

            BistroBuilderEmployeeSessionAssignmentView view;
            if (session.TryGetAssignmentView(targetEmployeeId, out view) &&
                view != null && view.completedTasks > 0)
            {
                activeCheckpointCompletedTasks = view.completedTasks;
                CaptureActiveState();
                phase = Phase.SavingActiveCheckpoint;
                SetReport(
                    "Trabajo real observado. Guardando checkpoint Open...",
                    MessageType.Info);
                if (!save.TrySaveSlot(
                        checkpointSlot,
                        "BB 4G STAFF ACTIVE CHECKPOINT",
                        out string rejection))
                {
                    FailAndRollback(
                        "No pudo iniciar checkpoint Open: " + rejection);
                }
                return;
            }

            if (Time.realtimeSinceStartup >= deadline)
            {
                FailAndRollback("Timeout esperando tarea real completada.");
            }
            return;
        }

        if (phase == Phase.WaitingNaturalMutation)
        {
            if (!Resolve(out string error))
            {
                FailAndRollback(error);
                return;
            }

            session.TryGetAssignmentView(
                targetEmployeeId,
                out BistroBuilderEmployeeSessionAssignmentView mutationAssignment);

            if (BistroBuilderStaff4GNaturalMutationProbe.HasObservableMutation(
                    activeSessionJson,
                    activeCheckpointCompletedTasks,
                    session.CreateSessionSnapshot(),
                    mutationAssignment,
                    out string evidence))
            {
                phase = Phase.LoadingActiveCheckpoint;
                SetReport(
                    "Mutación observable confirmada: " + evidence +
                    " Cargando checkpoint Open...",
                    MessageType.Info);
                if (!save.TryLoadSlot(checkpointSlot, out string rejection))
                {
                    FailAndRollback("No pudo iniciar Load Open: " + rejection);
                }
                return;
            }

            if (Time.realtimeSinceStartup >= deadline)
            {
                FailAndRollback("Timeout esperando mutación observable antes del Load Open.");
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

            if (waiterCoordinator.ActiveTaskCount == 0 && AreBoundWaitersIdle())
            {
                if (!serviceState.TryCompleteClosing())
                {
                    FailAndRollback("No pudo completar Closing -> Closed.");
                    return;
                }
                ValidateClosedSessionAndSave();
                return;
            }

            if (Time.realtimeSinceStartup >= deadline)
            {
                FailAndRollback("Timeout esperando cierre operativo limpio.");
            }
        }
    }

    private void ValidateActiveLoadAndBeginClosing()
    {
        if (!Resolve(out string error))
        {
            FailAndRollback(error);
            return;
        }

        BistroBuilderEmployeeSessionAssignmentView assignment;
        bool assignmentOk = session.TryGetAssignmentView(
            targetEmployeeId,
            out assignment);

        if (serviceState.CurrentState != RestaurantServiceState.Open ||
            CurrentWaiterCount() != initialWaiterCount ||
            !JsonEquals(activeStaffJson, staff.CreateSnapshot()) ||
            !JsonEquals(activeMarketJson, recruitment.CreateMarketSnapshot()) ||
            !JsonEquals(activeSessionJson, session.CreateSessionSnapshot()) ||
            !assignmentOk || assignment == null ||
            assignment.waiterId != targetWaiterId ||
            assignment.completedTasks != activeCheckpointCompletedTasks)
        {
            FailAndRollback(
                "Load Open no restauró exactamente staff/mercado/binding/servicio.");
            return;
        }

        if (!serviceState.TryBeginClosing())
        {
            FailAndRollback("No pudo iniciar Closing tras Load Open.");
            return;
        }

        deadline = Time.realtimeSinceStartup + CloseTimeoutSeconds;
        phase = Phase.WaitingCloseReady;
        SetReport(
            "Load Open correcto. Esperando tareas y camareros libres para completar Closing...",
            MessageType.Info);
    }

    private void ValidateClosedSessionAndSave()
    {
        if (session.HasActiveSession)
        {
            FailAndRollback("4D mantiene sesión activa después de Closed.");
            return;
        }

        BistroBuilderEmployeeRecord employee;
        if (!staff.TryGetEmployee(targetEmployeeId, out employee) ||
            employee == null ||
            employee.experiencePoints <= targetExperienceBeforeService ||
            employee.performance.completedServices !=
                targetCompletedServicesBefore + 1 ||
            employee.performance.completedTasks < activeCheckpointCompletedTasks)
        {
            FailAndRollback(
                "Closed no consolidó XP/rendimiento real exactamente una vez.");
            return;
        }

        string beforeReplay = JsonUtility.ToJson(staff.CreateSnapshot());
        string replayError;
        if (!session.TryFinalizeClosedSession(out replayError) ||
            !string.Equals(
                beforeReplay,
                JsonUtility.ToJson(staff.CreateSnapshot()),
                StringComparison.Ordinal))
        {
            FailAndRollback(
                "Segunda finalización no fue idempotente: " + replayError);
            return;
        }

        CaptureClosedState();
        phase = Phase.SavingClosedCheckpoint;
        SetReport("Guardando checkpoint Closed...", MessageType.Info);
        if (!save.TrySaveSlot(
                checkpointSlot,
                "BB 4G STAFF CLOSED CHECKPOINT",
                out string rejection))
        {
            FailAndRollback("No pudo iniciar checkpoint Closed: " + rejection);
        }
    }

    private void ValidateClosedLoad()
    {
        if (!Resolve(out string error))
        {
            FailAndRollback(error);
            return;
        }

        if (serviceState.CurrentState != RestaurantServiceState.Closed ||
            session.HasActiveSession ||
            !JsonEquals(closedStaffJson, staff.CreateSnapshot()) ||
            !JsonEquals(closedMarketJson, recruitment.CreateMarketSnapshot()) ||
            !JsonEquals(closedSessionJson, session.CreateSessionSnapshot()))
        {
            FailAndRollback("Load Closed no restauró Personal exactamente.");
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

    private void CaptureActiveState()
    {
        activeStaffJson = JsonUtility.ToJson(staff.CreateSnapshot());
        activeMarketJson = JsonUtility.ToJson(recruitment.CreateMarketSnapshot());
        activeSessionJson = JsonUtility.ToJson(session.CreateSessionSnapshot());
    }

    private void CaptureClosedState()
    {
        closedStaffJson = JsonUtility.ToJson(staff.CreateSnapshot());
        closedMarketJson = JsonUtility.ToJson(recruitment.CreateMarketSnapshot());
        closedSessionJson = JsonUtility.ToJson(session.CreateSessionSnapshot());
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
                "Rollback no restituyó exactamente staff, mercado, sesión, servicio y Waiter count.";
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
            Waiter waiter;
            if (!byId.TryGetValue(waiterId, out waiter) ||
                waiter == null || waiter.CurrentState != WaiterState.Idle)
            {
                return false;
            }
        }
        return true;
    }

    private static bool JsonEquals(string expected, object current)
    {
        return current != null &&
               string.Equals(
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
            if (first < 0) first = slot;
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
        {
            CompleteFailure(
                PrefixFailure(failure) +
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
            pendingFailure = PrefixFailure(failure) +
                "No pudo iniciar borrado de checkpoint: " + rejection;
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
                PrefixFailure(failure) +
                "No pudo iniciar borrado de rollback: " + rejection);
        }
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
        pendingFailure = string.Empty;
        rollbackSlot = -1;
        checkpointSlot = -1;
        targetCandidateId = string.Empty;
        targetEmployeeId = string.Empty;
        targetTrainingId = string.Empty;
        targetWaiterId = 0;
        activeCheckpointCompletedTasks = 0;
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
        SetReport("4G — FAIL\n" + message, MessageType.Error);
        Debug.LogError(report);
    }

    private void CompleteFailure(string message)
    {
        Unsubscribe();
        phase = Phase.Failed;
        SetReport("4G — FAIL\n" + message, MessageType.Error);
        Debug.LogError(report);
    }

    private void CompleteSuccess()
    {
        Unsubscribe();
        phase = Phase.Completed;
        SetReport(
            "4G — QUEEN FLOW COMPLETADO\n" +
            "Contratación + EmployeeId estable\n" +
            "Disponibilidad reversible\n" +
            "Formación V1: " + targetTrainingId + "\n" +
            "Binding real con WaiterId " + targetWaiterId + "\n" +
            "Trabajo real: " + activeCheckpointCompletedTasks + " tarea(s)\n" +
            "Save/Load Open con mutación observable previa\n" +
            "Closed + XP/rendimiento idempotente\n" +
            "Save/Load Closed\n" +
            "Rollback integral + limpieza de slots\n\n" +
            "No cerrar Bloque 4 hasta revisar también compilación, logs y UI real.",
            MessageType.Info);
        Debug.Log(report);
    }
}
