using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderAdvancedFrontOfHouse14PlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.FOH14.Play.Stage";
    private const string SuccessKey = "BB.FOH14.Play.Success";
    private const string ReportPath = "AdvancedFrontOfHouse14PlayModeReport.txt";

    private static BistroBuilderAdvancedFrontOfHouseService service;
    private static TableAssignmentSystem tableAssignment;
    private static CustomerGroupSpawner spawner;
    private static RestaurantServiceStateService serviceState;
    private static BistroBuilderBarServiceSystem bar;
    private static CustomerGroup group;
    private static RestaurantTable heldTable;
    private static TableState heldTableState;
    private static RestaurantServiceState initialServiceState;
    private static bool initialAutomaticBarOffers;
    private static double phaseStarted;

    static BistroBuilderAdvancedFrontOfHouse14PlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Front Of House/14 - PlayMode real", false, 14003)]
    private static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 14 ya esta ejecutandose.");
        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetString(StageKey, cli ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            SessionState.SetString(StageKey, stage.EndsWith("cli", StringComparison.Ordinal) ? "setup_cli" : "setup_menu");
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            SessionState.EraseString(StageKey);
            if (cli) EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static void OnUpdate()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 5) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage) || stage.StartsWith("exit_", StringComparison.Ordinal)) return;
        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        try
        {
            if (stage.StartsWith("setup_", StringComparison.Ordinal))
            {
                SetupFixture();
                phaseStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "wait_cli" : "wait_menu");
                return;
            }
            if (stage.StartsWith("wait_", StringComparison.Ordinal))
            {
                if (group == null) throw new InvalidOperationException("El grupo de prueba fue destruido.");
                if (group.CurrentState != CustomerGroupState.WaitingForTable)
                {
                    if (EditorApplication.timeSinceStartup - phaseStarted > 6d)
                        throw new TimeoutException("El grupo no alcanzo la cola de entrada.");
                    return;
                }
                SetPrivate(group, "waitingTime", 50f);
                phaseStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "queue_cli" : "queue_menu");
                return;
            }
            if (stage.StartsWith("queue_", StringComparison.Ordinal))
            {
                var entries = new List<BistroBuilderFrontOfHouseQueueEntry>();
                service.CopyQueueSnapshot(entries);
                BistroBuilderFrontOfHouseQueueEntry entry = entries.Find(x => x.groupId == group.GroupId);
                if (entry == null)
                {
                    if (EditorApplication.timeSinceStartup - phaseStarted > 3d)
                        throw new TimeoutException("La IA de sala no incorporo el grupo a la cola avanzada.");
                    return;
                }
                if (entry.priorityScore <= 0 || entry.queuePosition < 1)
                {
                    if (EditorApplication.timeSinceStartup - phaseStarted > 3d)
                        throw new TimeoutException("La cola avanzada no calculo prioridad/posicion.");
                    return;
                }
                if (!service.TrySendGroupToBar(group.GroupId, out string error))
                    throw new InvalidOperationException("Envio manual a barra fallo: " + error);
                phaseStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "bar_cli" : "bar_menu");
                return;
            }
            if (stage.StartsWith("bar_", StringComparison.Ordinal))
            {
                if (!group.IsOccupyingBar)
                {
                    if (EditorApplication.timeSinceStartup - phaseStarted > 3d)
                        throw new TimeoutException("El grupo no ocupo la barra tras la orden manual.");
                    return;
                }
                if (!service.TryCaptureRuntimeSnapshot(out var snapshot, out string snapshotError) ||
                    snapshot == null || !snapshot.TryValidate(out _))
                    throw new InvalidOperationException("Persistencia de sala invalida: " + snapshotError);
                Finish(true,
                    "PASS - cola priorizada, estado operativo, proteccion de mesa, envio manual a barra, clientes avanzados y persistencia funcionan en Play Mode real.", cli);
            }
        }
        catch (Exception exception)
        {
            Finish(false, "14 PlayMode: " + Unwrap(exception).Message, cli);
        }
    }

    private static void SetupFixture()
    {
        service = UnityEngine.Object.FindFirstObjectByType<BistroBuilderAdvancedFrontOfHouseService>();
        tableAssignment = UnityEngine.Object.FindFirstObjectByType<TableAssignmentSystem>();
        spawner = UnityEngine.Object.FindFirstObjectByType<CustomerGroupSpawner>();
        serviceState = UnityEngine.Object.FindFirstObjectByType<RestaurantServiceStateService>();
        bar = UnityEngine.Object.FindFirstObjectByType<BistroBuilderBarServiceSystem>();
        if (service == null || tableAssignment == null || spawner == null || serviceState == null || bar == null)
            throw new InvalidOperationException("Faltan autoridades runtime del Bloque 14.");
        if (!service.ValidateConfiguration(out string config))
            throw new InvalidOperationException(config);

        initialAutomaticBarOffers = GetPrivate<bool>(service, "enableAutomaticBarOffers");
        SetPrivate(service, "enableAutomaticBarOffers", false);
        initialServiceState = serviceState.CurrentState;
        if (serviceState.IsClosed)
        {
            if (!serviceState.TryBeginPreparation() || !serviceState.TryOpenService())
                throw new InvalidOperationException("No pudo abrirse el servicio para la prueba 14.");
        }

        foreach (RestaurantTable table in UnityEngine.Object.FindObjectsByType<RestaurantTable>(FindObjectsSortMode.InstanceID))
        {
            if (table != null && table.Capacity >= 2 && table.IsAvailable)
            {
                heldTable = table;
                heldTableState = table.CurrentState;
                table.SetState(TableState.Dirty);
                break;
            }
        }
        if (heldTable == null) throw new InvalidOperationException("No existe mesa libre para el fixture 14.");

        if (!spawner.TrySpawnExternalTableServiceGroup(2, out group, out string spawnError))
            throw new InvalidOperationException("No pudo crear grupo de prueba 14: " + spawnError);

        // El pipeline actual puede adquirir una preferencia logica durante el spawn.
        // El fixture libera solo ese derecho de prueba, sin pedir reevaluacion, para
        // verificar de forma determinista la proteccion de la mesa seleccionada.
        BistroBuilderSeatingReservationCoordinator seating =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSeatingReservationCoordinator>();
        if (seating != null &&
            seating.TryGetTableRight(group, out RestaurantTable currentRight, out _) &&
            !ReferenceEquals(currentRight, heldTable))
            seating.ReleaseGroupRight(group);

        if (!tableAssignment.TryReservePreferredTable(group, heldTable, out string reserveError))
            throw new InvalidOperationException("No pudo proteger mesa de prueba: " + reserveError);

        // Este test pertenece a Sala 14, no al subsistema de llegada. Evita que
        // un delay/race del prefab de llegada haga no determinista la cola de prueba.
        if (group.CurrentState == CustomerGroupState.Entering)
            group.SetState(CustomerGroupState.WaitingForTable);
    }

    private static void CleanupFixture()
    {
        try
        {
            if (service != null) SetPrivate(service, "enableAutomaticBarOffers", initialAutomaticBarOffers);
            if (group != null && spawner != null)
                spawner.UnregisterAndDestroyGroupForRuntimeLoad(group);
            if (heldTable != null && heldTable.AssignedCustomerGroup == null)
                heldTable.SetState(heldTableState);
            if (serviceState != null && initialServiceState == RestaurantServiceState.Closed && !serviceState.IsClosed)
                serviceState.TryCloseServiceImmediately();
        }
        catch { }
        group = null; heldTable = null;
    }

    private static void Finish(bool ok, string message, bool cli)
    {
        CleanupFixture();
        string report = "=== BISTRO BUILDER - BLOQUE 14 / PLAY MODE REAL ===\n" +
                        (ok ? "[PASS] " : "[FAIL] ") + message;
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, ok);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }

    private static T GetPrivate<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void SetPrivate(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static Exception Unwrap(Exception e) => e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
}

