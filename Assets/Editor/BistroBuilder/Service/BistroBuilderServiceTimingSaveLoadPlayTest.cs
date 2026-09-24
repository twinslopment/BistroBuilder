using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Regresión real de Save/Load para las acciones contextuales de cuenta.
/// Reproduce el caso que reconstruye CustomerGroup con el mismo GroupId y
/// verifica que reputation.runtime se reengancha a la nueva instancia sin
/// perder la visita ni duplicar la tarea DeliverBill.
/// </summary>
[InitializeOnLoad]
public static class BistroBuilderServiceTimingSaveLoadPlayTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.ServiceTiming.SaveLoad.Stage";
    private const string SuccessKey = "BB.ServiceTiming.SaveLoad.Success";
    private const string FailureKey = "BB.ServiceTiming.SaveLoad.Failure";
    private const string ReportPath = "Logs/ServiceTimingSaveLoadPlayTest.txt";

    private static BistroBuilderSaveGameService saveGame;
    private static RestaurantServiceStateService serviceState;
    private static BistroBuilderTableContextActionService actions;
    private static BistroBuilderCustomerExperienceTrackingService experience;
    private static TableAssignmentSystem assignments;
    private static WaiterTaskCoordinator coordinator;
    private static CustomerGroupSpawner spawner;

    private static RestaurantTable table;
    private static CustomerGroup group;
    private static CustomerGroup priorLoadGroupReference;
    private static int tableId;
    private static int groupId;
    private static int slot = -1;
    private static string runtimeFailure;

    static BistroBuilderServiceTimingSaveLoadPlayTest()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
    }

    public static void RunFromCommandLine()
    {
        Begin(true);
    }

    [MenuItem("Bistro Builder/Servicio/Timing contextual/SaveLoad real")]
    private static void RunFromMenu()
    {
        Begin(false);
    }

    private static void Begin(bool commandLine)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "El Save/Load de Service Timing ya está ejecutándose."
            );

        Directory.CreateDirectory(
            Path.GetDirectoryName(Path.GetFullPath(ReportPath))
        );
        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.EraseString(FailureKey);
        SessionState.SetString(
            StageKey,
            commandLine ? "enter_cli" : "enter_menu"
        );

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(stage))
            return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            runtimeFailure = null;
            Application.logMessageReceived -= HandleRuntimeLog;
            Application.logMessageReceived += HandleRuntimeLog;

            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(
                StageKey,
                cli ? "prepare_cli" : "prepare_menu"
            );
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            Application.logMessageReceived -= HandleRuntimeLog;

            bool success = SessionState.GetBool(SuccessKey, false);
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            SessionState.EraseString(StageKey);
            if (cli)
                EditorApplication.Exit(success ? 0 : 1);
        }
    }

    private static void HandleRuntimeLog(
        string message,
        string stackTrace,
        LogType type)
    {
        if (type != LogType.Exception &&
            type != LogType.Assert &&
            type != LogType.Error)
        {
            return;
        }

        if (!string.IsNullOrEmpty(stackTrace) &&
            stackTrace.Contains("UnityEditor.Search"))
        {
            return;
        }

        if (string.IsNullOrEmpty(runtimeFailure))
        {
            runtimeFailure =
                type + ": " + message +
                (string.IsNullOrWhiteSpace(stackTrace)
                    ? string.Empty
                    : "\n" + stackTrace);
        }
    }

    private static void HandleUpdate()
    {
        if (!EditorApplication.isPlaying)
            return;

        string stage = SessionState.GetString(StageKey, string.Empty);
        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);

        try
        {
            Check(
                string.IsNullOrEmpty(runtimeFailure),
                "Console runtime no limpia: " + runtimeFailure
            );

            if (stage.StartsWith("prepare_", StringComparison.Ordinal) &&
                Time.frameCount >= 6)
            {
                Prepare(cli);
                return;
            }

            if (stage.StartsWith("apply_", StringComparison.Ordinal) &&
                Time.frameCount >= 8)
            {
                ApplyActionsAndSave(cli);
            }
        }
        catch (Exception exception)
        {
            FailAndCleanup(
                "Excepción en Save/Load Service Timing: " + exception,
                cli
            );
        }
    }

    private static void Prepare(bool commandLine)
    {
        SessionState.SetString(
            StageKey,
            commandLine ? "apply_cli" : "apply_menu"
        );

        UnityEngine.Object
            .FindFirstObjectByType<BistroBuilderNewGameOpeningPlayerScreen>()
            ?.Hide();

        saveGame = Find<BistroBuilderSaveGameService>();
        serviceState = Find<RestaurantServiceStateService>();
        actions = Find<BistroBuilderTableContextActionService>();
        experience = Find<BistroBuilderCustomerExperienceTrackingService>();
        assignments = Find<TableAssignmentSystem>();
        coordinator = Find<WaiterTaskCoordinator>();
        spawner = Find<CustomerGroupSpawner>();

        Check(
            saveGame != null &&
            serviceState != null &&
            actions != null &&
            experience != null &&
            assignments != null &&
            coordinator != null &&
            spawner != null,
            "Faltan autoridades runtime."
        );

        saveGame.RefreshExtensions();

        Check(
            saveGame.HasProvider(
                BistroBuilderActiveServiceSaveSectionProvider.StableSectionId
            ),
            "Falta service.runtime en SaveGame."
        );
        Check(
            saveGame.HasProvider(
                BistroBuilderReputationRuntimeSaveSectionProvider.StableSectionId
            ),
            "Falta reputation.runtime en SaveGame."
        );
        Check(
            actions.ValidateConfiguration(out string actionsError),
            "TableContextActionService inválido: " + actionsError
        );
        Check(
            experience.ValidateConfiguration(out string experienceError),
            "Experience Tracking inválido: " + experienceError
        );

        if (!TryFindFreeSlot(out slot))
            throw new InvalidOperationException(
                "No existe slot diagnóstico libre 990-999."
            );

        spawner.enabled = true;
        Check(
            serviceState.TryOpenService(),
            "No se pudo abrir el servicio real para el checkpoint."
        );
        Check(
            spawner.HasInitializedSpawnSchedule,
            "La apertura real no inicializó el calendario de llegadas."
        );

        // Conserva el calendario persistible y evita que lleguen grupos
        // ajenos mientras se construye el fixture.
        spawner.enabled = false;

        foreach (Waiter waiter in
                 UnityEngine.Object.FindObjectsByType<Waiter>(
                     FindObjectsSortMode.None
                 ))
        {
            coordinator.UnregisterWaiter(waiter);
        }

        foreach (RestaurantTable candidate in
                 UnityEngine.Object.FindObjectsByType<RestaurantTable>(
                     FindObjectsSortMode.InstanceID
                 ))
        {
            if (candidate != null &&
                candidate.AssignedCustomerGroup == null)
            {
                table = candidate;
                break;
            }
        }

        Check(table != null, "No hay una mesa libre para la prueba.");

        groupId = ResolveDiagnosticGroupId();
        SetSpawnerNextGroupId(groupId + 1);

        GameObject groupObject =
            new GameObject("BB_ServiceTiming_SaveLoad_Group");
        group = groupObject.AddComponent<CustomerGroup>();

        Check(
            group.Initialize(
                groupId,
                Math.Max(1, Math.Min(2, table.Capacity))
            ),
            "No se pudo inicializar el grupo diagnóstico."
        );
        Check(
            assignments.RegisterCustomerGroup(group),
            "No se pudo registrar el grupo diagnóstico."
        );
        Check(
            group.AssignTable(table),
            "No se pudo asignar la mesa al grupo diagnóstico."
        );

        tableId = table.TableId;
        priorLoadGroupReference = group;
        group.SetState(CustomerGroupState.WaitingForBill);
        table.SetState(TableState.WaitingForBill);
    }

    private static void ApplyActionsAndSave(bool commandLine)
    {
        BistroBuilderReputationVisitRuntimeRecord visit =
            GetInternalVisit(groupId);
        visit.billWaitSeconds = 300f;

        Check(
            actions.TryGetBillSnapshot(table, out var before) &&
            before.TimingState == BistroBuilderServiceTimingState.Incident,
            "El fixture no alcanzó Incidencia antes del guardado."
        );

        Check(
            actions.TryAccelerateBill(table, out string accelerateError),
            "Agilizar cuenta falló: " + accelerateError
        );
        Check(
            actions.TryExplainBillDelay(table, out string explainError),
            "Explicar demora falló: " + explainError
        );
        Check(
            actions.TryApologize(table, out string apologyError),
            "Disculpa falló: " + apologyError
        );

        Check(
            actions.TryGetBillSnapshot(table, out var appliedBill) &&
            appliedBill.IsAlreadyAccelerated &&
            appliedBill.IsDelayExplained &&
            appliedBill.TaskPriority == WaiterTaskPriority.Urgent &&
            !appliedBill.CanAccelerate &&
            !appliedBill.CanExplainDelay,
            "El estado aplicado antes de Save no es el esperado."
        );
        Check(
            actions.TryGetApologySnapshot(table, out var appliedApology) &&
            appliedApology.BillIncidentAlreadyApologized &&
            !appliedApology.CanApologize,
            "Disculpa no quedó aplicada antes de Save."
        );

        saveGame.OperationCompleted -= HandleOperationCompleted;
        saveGame.OperationCompleted += HandleOperationCompleted;

        SessionState.SetString(
            StageKey,
            commandLine ? "save_cli" : "save_menu"
        );

        Check(
            saveGame.TrySaveSlot(
                slot,
                "BB SERVICE TIMING SAVELOAD TEST",
                out string rejection
            ),
            "Save rechazado: " + rejection
        );
    }

    private static void HandleOperationCompleted(
        BistroBuilderSaveOperationResult result)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        bool commandLine =
            stage.EndsWith("cli", StringComparison.Ordinal);

        if (!string.IsNullOrEmpty(runtimeFailure))
        {
            FailAndCleanup(
                "Console runtime no limpia: " + runtimeFailure,
                commandLine
            );
            return;
        }

        if (result == null || !result.Succeeded)
        {
            FailAndCleanup(
                "Operación Save/Load falló: " +
                (result != null ? result.Message : "resultado nulo"),
                commandLine
            );
            return;
        }

        try
        {
            if (stage.StartsWith("save_", StringComparison.Ordinal))
            {
                MutateAfterSave();

                SessionState.SetString(
                    StageKey,
                    commandLine ? "load_cli" : "load_menu"
                );

                Check(
                    saveGame.TryLoadSlot(slot, out string rejection),
                    "Load rechazado: " + rejection
                );
                return;
            }

            if (stage.StartsWith("load_", StringComparison.Ordinal))
            {
                VerifyLoadedState();
                MutateForRepeatedLoad();

                SessionState.SetString(
                    StageKey,
                    commandLine ? "reload_cli" : "reload_menu"
                );

                Check(
                    saveGame.TryLoadSlot(slot, out string rejection),
                    "Segundo Load rechazado: " + rejection
                );
                return;
            }

            if (stage.StartsWith("reload_", StringComparison.Ordinal))
            {
                VerifyLoadedState();

                SessionState.SetString(
                    StageKey,
                    commandLine ? "delete_cli" : "delete_menu"
                );

                Check(
                    saveGame.TryDeleteSlot(slot, out string rejection),
                    "No se pudo limpiar el slot: " + rejection
                );
                return;
            }

            if (stage.StartsWith("delete_", StringComparison.Ordinal))
            {
                Finish(
                    true,
                    "PASS — Save -> mutación -> Load x2 restaura Agilizar cuenta, " +
                    "Explicar demora y Disculpa; reengancha reputation.runtime " +
                    "en cada reconstrucción de CustomerGroup y conserva exactamente " +
                    "una tarea DeliverBill priorizada sin duplicados.",
                    commandLine
                );
                return;
            }

            if (stage.StartsWith("cleanup_", StringComparison.Ordinal))
            {
                string failure = SessionState.GetString(
                    FailureKey,
                    "Save/Load Service Timing falló."
                );
                Finish(false, failure, commandLine);
            }
        }
        catch (Exception exception)
        {
            FailAndCleanup(
                "Verificación Save/Load falló: " + exception,
                commandLine
            );
        }
    }

    private static void MutateAfterSave()
    {
        Check(
            coordinator.TryChangePendingTableTaskPriority(
                WaiterTaskType.DeliverBill,
                table,
                WaiterTaskPriority.High,
                out _
            ),
            "No se pudo degradar la prioridad antes de Load."
        );

        BistroBuilderReputationVisitRuntimeRecord mutatedVisit =
            GetInternalVisit(groupId);
        mutatedVisit.billWaitSeconds = 0f;
        mutatedVisit.billDelayExplanationMitigationBasisPoints = 0;
        mutatedVisit.billIncidentApologyMitigationBasisPoints = 0;

        group.SetState(CustomerGroupState.Eating);
        table.SetState(TableState.Eating);

        Check(
            mutatedVisit.billWaitSeconds == 0f &&
            mutatedVisit.billDelayExplanationMitigationBasisPoints == 0 &&
            mutatedVisit.billIncidentApologyMitigationBasisPoints == 0,
            "La mutación previa a Load no alteró reputation.runtime."
        );
    }

    private static void MutateForRepeatedLoad()
    {
        priorLoadGroupReference = group;

        Check(
            coordinator.TryChangePendingTableTaskPriority(
                WaiterTaskType.DeliverBill,
                table,
                WaiterTaskPriority.High,
                out _
            ),
            "No se pudo degradar la prioridad antes del segundo Load."
        );

        BistroBuilderReputationVisitRuntimeRecord mutatedVisit =
            GetInternalVisit(groupId);
        mutatedVisit.billWaitSeconds = 1f;
        mutatedVisit.billDelayExplanationMitigationBasisPoints = 0;
        mutatedVisit.billIncidentApologyMitigationBasisPoints = 0;

        group.SetState(CustomerGroupState.Eating);
        table.SetState(TableState.Eating);
    }

    private static void VerifyLoadedState()
    {
        table = FindTable(tableId);
        group = FindGroup(groupId);

        Check(table != null, "Load no restauró la mesa.");
        Check(group != null, "Load no restauró el grupo.");
        Check(
            !ReferenceEquals(priorLoadGroupReference, group),
            "Load no reconstruyó CustomerGroup; la regresión no fue ejercitada."
        );

        int sameGroupCount = 0;
        foreach (CustomerGroup candidate in
                 UnityEngine.Object.FindObjectsByType<CustomerGroup>(
                     FindObjectsSortMode.None
                 ))
        {
            if (candidate != null && candidate.GroupId == groupId)
                sameGroupCount++;
        }

        Check(
            sameGroupCount == 1,
            "Load duplicó el grupo diagnóstico: " +
            sameGroupCount + " instancias."
        );

        Check(
            serviceState.CurrentState == RestaurantServiceState.Open,
            "Load no restauró el servicio Open."
        );
        Check(
            table.CurrentState == TableState.WaitingForBill,
            "Load no restauró la mesa en WaitingForBill."
        );
        Check(
            group.CurrentState == CustomerGroupState.WaitingForBill,
            "Load no restauró el grupo en WaitingForBill."
        );
        Check(
            table.AssignedCustomerGroup != null &&
            table.AssignedCustomerGroup.GroupId == groupId &&
            group.AssignedTable != null &&
            group.AssignedTable.TableId == tableId,
            "Load no restauró la asignación grupo-mesa."
        );

        Check(
            experience.TryGetRuntimeVisit(
                groupId,
                out BistroBuilderReputationVisitRuntimeRecord visit
            ) &&
            visit != null,
            "Load no restauró reputation.runtime."
        );

        int runtimeVisitCount = 0;
        BistroBuilderReputationRuntimeSnapshot runtimeSnapshot =
            experience.CreateRuntimeSnapshot();
        for (int index = 0; index < runtimeSnapshot.visits.Count; index++)
        {
            if (runtimeSnapshot.visits[index] != null &&
                runtimeSnapshot.visits[index].groupId == groupId)
            {
                runtimeVisitCount++;
            }
        }
        Check(
            runtimeVisitCount == 1,
            "Load debe conservar exactamente una visita reputation.runtime; hay " +
            runtimeVisitCount + "."
        );
        Check(
            visit.billWaitSeconds >= 299f,
            "Load perdió el tiempo de espera de cuenta: " +
            visit.billWaitSeconds
        );
        Check(
            visit.billDelayExplanationMitigationBasisPoints == 1500,
            "Load perdió Explicar demora."
        );
        Check(
            visit.billIncidentApologyMitigationBasisPoints == 2500,
            "Load perdió Disculpa."
        );

        Check(
            actions.TryGetBillSnapshot(table, out var bill),
            "No se pudo leer la cuenta restaurada."
        );
        Check(
            bill.IsAlreadyAccelerated &&
            bill.TaskPriority == WaiterTaskPriority.Urgent &&
            bill.IsDelayExplained &&
            !bill.CanAccelerate &&
            !bill.CanExplainDelay,
            "Las acciones de cuenta no se restauraron exactamente."
        );

        Check(
            actions.TryGetApologySnapshot(table, out var apology) &&
            apology.BillIncidentAlreadyApologized &&
            !apology.CanApologize,
            "Disculpa no se restauró o volvió a quedar disponible."
        );

        int deliverBillTaskCount = 0;
        foreach (WaiterTask task in coordinator.ActiveTasks)
        {
            if (task != null &&
                task.Type == WaiterTaskType.DeliverBill &&
                task.Table != null &&
                task.Table.TableId == tableId)
            {
                deliverBillTaskCount++;
            }
        }

        Check(
            deliverBillTaskCount == 1,
            "Load debe dejar exactamente una tarea DeliverBill; hay " +
            deliverBillTaskCount + "."
        );
    }

    private static BistroBuilderReputationVisitRuntimeRecord
        GetInternalVisit(int id)
    {
        FieldInfo field =
            typeof(BistroBuilderCustomerExperienceTrackingService)
                .GetField(
                    "visitsByGroup",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );

        Check(field != null, "No se encontró visitsByGroup.");

        IDictionary visits = field.GetValue(experience) as IDictionary;
        Check(
            visits != null && visits.Contains(id),
            "Experience Tracking no registró la visita."
        );

        var visit =
            visits[id] as BistroBuilderReputationVisitRuntimeRecord;
        Check(visit != null, "La visita interna es nula.");
        return visit;
    }

    private static int ResolveDiagnosticGroupId()
    {
        int maximum = 0;
        foreach (CustomerGroup candidate in
                 UnityEngine.Object.FindObjectsByType<CustomerGroup>(
                     FindObjectsSortMode.None
                 ))
        {
            if (candidate != null)
                maximum = Math.Max(maximum, candidate.GroupId);
        }

        return Math.Max(900, maximum + 100);
    }

    private static void SetSpawnerNextGroupId(int nextId)
    {
        FieldInfo field = typeof(CustomerGroupSpawner).GetField(
            "nextGroupId",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Check(field != null, "No se encontró nextGroupId.");
        field.SetValue(spawner, nextId);
    }

    private static RestaurantTable FindTable(int id)
    {
        foreach (RestaurantTable candidate in
                 UnityEngine.Object.FindObjectsByType<RestaurantTable>(
                     FindObjectsSortMode.None
                 ))
        {
            if (candidate != null && candidate.TableId == id)
                return candidate;
        }
        return null;
    }

    private static CustomerGroup FindGroup(int id)
    {
        foreach (CustomerGroup candidate in
                 UnityEngine.Object.FindObjectsByType<CustomerGroup>(
                     FindObjectsSortMode.None
                 ))
        {
            if (candidate != null && candidate.GroupId == id)
                return candidate;
        }
        return null;
    }

    private static bool TryFindFreeSlot(out int found)
    {
        found = -1;
        for (int candidate = 990; candidate <= 998; candidate++)
        {
            if (saveGame.SlotExists(candidate))
                continue;

            found = candidate;
            return true;
        }
        return false;
    }

    private static void FailAndCleanup(
        string message,
        bool commandLine)
    {
        SessionState.SetString(FailureKey, message);

        if (saveGame != null &&
            slot >= 0 &&
            saveGame.SlotExists(slot) &&
            !saveGame.IsBusy)
        {
            SessionState.SetString(
                StageKey,
                commandLine ? "cleanup_cli" : "cleanup_menu"
            );

            if (saveGame.TryDeleteSlot(slot, out _))
                return;
        }

        Finish(false, message, commandLine);
    }

    private static void Finish(
        bool success,
        string message,
        bool commandLine)
    {
        if (success && !string.IsNullOrEmpty(runtimeFailure))
        {
            success = false;
            message = "Console runtime no limpia: " + runtimeFailure;
        }

        Application.logMessageReceived -= HandleRuntimeLog;

        if (saveGame != null)
            saveGame.OperationCompleted -= HandleOperationCompleted;

        string report =
            "=== BISTRO BUILDER — SERVICE TIMING SAVE/LOAD REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") +
            message + "\n";

        File.WriteAllText(
            Path.GetFullPath(ReportPath),
            report
        );

        if (success)
            Debug.Log(report);
        else
            Debug.LogError(report);

        SessionState.SetBool(SuccessKey, success);
        SessionState.EraseString(FailureKey);
        SessionState.SetString(
            StageKey,
            commandLine ? "exit_cli" : "exit_menu"
        );

        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static T Find<T>() where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindFirstObjectByType<T>(
            FindObjectsInactive.Include
        );
    }
}
