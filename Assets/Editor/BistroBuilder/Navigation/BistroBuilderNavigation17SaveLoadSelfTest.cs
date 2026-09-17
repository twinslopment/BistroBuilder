using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderNavigation17SaveLoadSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Navigation17.SaveLoad.StageV2";
    private const string SuccessKey = "BB.Navigation17.SaveLoad.SuccessV2";
    private const string SlotKey = "BB.Navigation17.SaveLoad.SlotV2";
    private const string ReportPath = "Navigation17SaveLoadReport.txt";
    private const double TimeoutSeconds = 180d;
    private const double PlayReadyDelaySeconds = 0.35d;

    private static double stageStartedAt;
    private static double playReadyAt;

    static BistroBuilderNavigation17SaveLoadSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Bistro Builder/17 Navegacion/SaveLoad real")]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El Save/Load Navigation 17 ya esta ejecutandose.");

        File.Delete(Path.GetFullPath(ReportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetInt(SlotKey, 0);
        SessionState.SetString(StageKey, cli ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage)) return;

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(StageKey, cli ? "prepare_cli" : "prepare_menu");
            playReadyAt = EditorApplication.timeSinceStartup + PlayReadyDelaySeconds;
            stageStartedAt = EditorApplication.timeSinceStartup;
            if (cli) EditorApplication.QueuePlayerLoopUpdate();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            CleanupSession();
            if (cli) EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static void OnUpdate()
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage)) return;

        if (!EditorApplication.isPlaying)
        {
            if (!stage.StartsWith("exit_", StringComparison.Ordinal)) return;
            bool cliExit = stage.EndsWith("cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            CleanupSession();
            if (cliExit) EditorApplication.Exit(ok ? 0 : 1);
            return;
        }

        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        if (cli) EditorApplication.QueuePlayerLoopUpdate();

        if (stage.StartsWith("enter_", StringComparison.Ordinal))
        {
            stage = cli ? "prepare_cli" : "prepare_menu";
            SessionState.SetString(StageKey, stage);
            playReadyAt = EditorApplication.timeSinceStartup + PlayReadyDelaySeconds;
            stageStartedAt = EditorApplication.timeSinceStartup;
        }

        if (EditorApplication.timeSinceStartup - stageStartedAt > TimeoutSeconds)
        {
            BistroBuilderSaveGameService timedOutSave = FindSaveService();
            string diagnostics = timedOutSave != null
                ? " stage=" + stage +
                  ", busy=" + timedOutSave.IsBusy +
                  ", op=" + timedOutSave.ActiveOperation +
                  ", phase=" + timedOutSave.CurrentPhase +
                  ", progress=" + timedOutSave.CurrentProgress.ToString("F2") +
                  ", status=" + timedOutSave.CurrentStatusMessage
                : " stage=" + stage + ", SaveGameService ausente";
            Finish(false,
                "Timeout del Save/Load especifico de Navigation 17;" + diagnostics + ".",
                cli);
            return;
        }

        if (stage.StartsWith("prepare_", StringComparison.Ordinal))
        {
            if (playReadyAt <= 0d)
                playReadyAt = EditorApplication.timeSinceStartup + PlayReadyDelaySeconds;
            if (EditorApplication.timeSinceStartup < playReadyAt) return;
            BeginSave(cli);
            return;
        }

        if (stage.StartsWith("saving_", StringComparison.Ordinal))
        {
            PollSave(cli);
            return;
        }

        if (stage.StartsWith("loading_", StringComparison.Ordinal))
        {
            PollLoad(cli);
            return;
        }

        if (stage.StartsWith("deleting_", StringComparison.Ordinal))
            PollDelete(cli);
    }

    private static void BeginSave(bool cli)
    {
        BistroBuilderSaveGameService save = FindSaveService();
        BistroBuilderNavigationService navigation = FindNavigation();
        if (save == null || navigation == null)
        {
            Finish(false, "Faltan SaveGameService o NavigationService en runtime.", cli);
            return;
        }

        save.RefreshExtensions();
        navigation.RebuildNavigationTopology();
        ValidateNavigationPersistencePreflight(navigation);

        int slot = FindFreeSlot(save);
        if (slot <= 0)
        {
            Finish(false, "Los slots diagnosticos 970-979 estan ocupados.", cli);
            return;
        }

        SessionState.SetInt(SlotKey, slot);
        if (!save.TrySaveSlot(slot, "BB Navigation 17 diagnostic", out string rejection))
        {
            Finish(false, "Save rechazado: " + rejection, cli);
            return;
        }

        SetStage(cli ? "saving_cli" : "saving_menu");
    }

    private static void PollSave(bool cli)
    {
        BistroBuilderSaveGameService save = FindSaveService();
        int slot = SessionState.GetInt(SlotKey, 0);
        if (save == null || save.IsBusy) return;

        BistroBuilderSaveOperationResult result = save.LastResult;
        if (result == null || result.OperationKind != BistroBuilderSaveOperationKind.Save || result.SlotIndex != slot)
            return;
        if (!result.Succeeded)
        {
            Finish(false, "Save fallo: " + result.Message, cli);
            return;
        }

        if (!save.TryLoadSlot(slot, out string rejection))
        {
            Finish(false, "Load rechazado: " + rejection, cli);
            return;
        }
        SetStage(cli ? "loading_cli" : "loading_menu");
    }

    private static void PollLoad(bool cli)
    {
        BistroBuilderSaveGameService save = FindSaveService();
        int slot = SessionState.GetInt(SlotKey, 0);
        if (save == null || save.IsBusy) return;

        BistroBuilderSaveOperationResult result = save.LastResult;
        if (result == null || result.OperationKind != BistroBuilderSaveOperationKind.Load || result.SlotIndex != slot)
            return;
        if (!result.Succeeded)
        {
            Finish(false, "Load fallo: " + result.Message, cli);
            return;
        }

        BistroBuilderNavigationService navigation = FindNavigation();
        if (navigation == null)
        {
            Finish(false, "NavigationService no existe despues de cargar.", cli);
            return;
        }

        try
        {
            navigation.RebuildNavigationTopology();
            ValidateNavigationRuntime(navigation);
            ValidateRealRoutes(navigation);
        }
        catch (Exception exception)
        {
            Finish(false, "Navigation no se reconstruyo correctamente tras Load: " + exception.Message, cli);
            return;
        }

        if (!save.TryDeleteSlot(slot, out string rejection))
        {
            Finish(false, "La prueba paso Save/Load pero no pudo eliminar el slot: " + rejection, cli);
            return;
        }
        SetStage(cli ? "deleting_cli" : "deleting_menu");
    }

    private static void PollDelete(bool cli)
    {
        BistroBuilderSaveGameService save = FindSaveService();
        int slot = SessionState.GetInt(SlotKey, 0);
        if (save == null || save.IsBusy) return;

        BistroBuilderSaveOperationResult result = save.LastResult;
        if (result == null || result.OperationKind != BistroBuilderSaveOperationKind.Delete || result.SlotIndex != slot)
            return;
        if (!result.Succeeded)
        {
            Finish(false, "Save/Load fue correcto pero Delete fallo: " + result.Message, cli);
            return;
        }

        Finish(true,
            "PASS - Save/Load real conserva y reconstruye Navigation: topologia, Route Graph, rutas cliente/camarero, sillas y ausencia de deadlocks fantasma.",
            cli);
    }

    private static void ValidateNavigationPersistencePreflight(BistroBuilderNavigationService navigation)
    {
        if (navigation.RouteGraphNodeCount <= 0 || navigation.RouteGraphEdgeCount <= 0)
            throw new InvalidOperationException("Route Graph no esta construido.");
        if (navigation.ActiveDeadlockCount != 0)
            throw new InvalidOperationException("Hay deadlocks fantasma antes de Save.");

        RestaurantSeat[] seats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (seats.Length == 0)
            throw new InvalidOperationException("No se encontraron sillas operativas.");
        for (int i = 0; i < seats.Length; i++)
            if (seats[i] != null && seats[i].GetComponent<BistroBuilderSeatCirculationEnvelope>() == null)
                throw new InvalidOperationException("Una silla perdio su envelope de circulacion.");
    }

    private static void ValidateNavigationRuntime(BistroBuilderNavigationService navigation)
    {
        BistroBuilderCirculationHealthReport health = navigation.EvaluateCirculationHealth();
        if (health == null || !health.IsOperational || health.checkedConnections <= 0 ||
            health.reachableConnections != health.checkedConnections)
            throw new InvalidOperationException("La salud de circulacion no es operativa.");
        if (navigation.RouteGraphNodeCount <= 0 || navigation.RouteGraphEdgeCount <= 0)
            throw new InvalidOperationException("Route Graph no esta construido.");
        if (navigation.ActiveDeadlockCount != 0)
            throw new InvalidOperationException("Hay deadlocks fantasma tras reconstruccion.");

        RestaurantSeat[] seats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (seats.Length == 0)
            throw new InvalidOperationException("No se encontraron sillas operativas.");
        for (int i = 0; i < seats.Length; i++)
            if (seats[i] != null && seats[i].GetComponent<BistroBuilderSeatCirculationEnvelope>() == null)
                throw new InvalidOperationException("Una silla perdio su envelope de circulacion.");
    }

    private static void ValidateRealRoutes(BistroBuilderNavigationService navigation)
    {
        GameObject entrance = GameObject.Find("RestaurantEntrancePoint");
        KitchenSystem kitchen = UnityEngine.Object.FindFirstObjectByType<KitchenSystem>();
        RestaurantTable[] tables = UnityEngine.Object.FindObjectsByType<RestaurantTable>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        if (entrance == null || kitchen == null || kitchen.PickupPoint == null || tables.Length == 0)
            throw new InvalidOperationException("Faltan puntos reales para verificar rutas.");

        RestaurantTable target = null;
        for (int i = 0; i < tables.Length; i++)
        {
            if (tables[i] != null && tables[i].CustomerApproachPoint != null && tables[i].WaiterServicePoint != null)
            {
                target = tables[i];
                break;
            }
        }
        if (target == null)
            throw new InvalidOperationException("No hay mesa con puntos de aproximacion completos.");

        var customerRoute = new List<Vector3>(32);
        if (!navigation.TryBuildRoute("saveload17:customer", BistroBuilderNavigationAgentMask.Customer,
                entrance.transform.position, target.CustomerApproachPoint.position,
                customerRoute, out float customerMeters, out _) || customerMeters <= 0f)
            throw new InvalidOperationException("Ruta real de cliente no se reconstruyo.");

        var waiterRoute = new List<Vector3>(32);
        if (!navigation.TryBuildRoute("saveload17:waiter", BistroBuilderNavigationAgentMask.Waiter,
                kitchen.PickupPoint.position, target.WaiterServicePoint.position,
                waiterRoute, out float waiterMeters, out _) || waiterMeters <= 0f)
            throw new InvalidOperationException("Ruta real de camarero no se reconstruyo.");
    }

    private static BistroBuilderSaveGameService FindSaveService() =>
        UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();

    private static BistroBuilderNavigationService FindNavigation() =>
        UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>();

    private static int FindFreeSlot(BistroBuilderSaveGameService save)
    {
        for (int slot = 979; slot >= 970; slot--)
            if (!save.SlotExists(slot)) return slot;
        return 0;
    }

    private static void SetStage(string stage)
    {
        SessionState.SetString(StageKey, stage);
        stageStartedAt = EditorApplication.timeSinceStartup;
    }

    private static void Finish(bool success, string message, bool cli)
    {
        string report = "=== BISTRO BUILDER - BLOQUE 17 / SAVE-LOAD REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    private static void CleanupSession()
    {
        SessionState.EraseString(StageKey);
        SessionState.EraseInt(SlotKey);
        playReadyAt = 0d;
        stageStartedAt = 0d;
    }
}
