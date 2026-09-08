using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderNavigation17PlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Navigation17.Play.Stage";
    private const string SuccessKey = "BB.Navigation17.Play.Success";
    private const string ReportPath = "Navigation17PlayModeReport.txt";
    private const double PlayReadyDelaySeconds = 0.25d;
    private static double playReadyAt;

    static BistroBuilderNavigation17PlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Bistro Builder/17 Navegacion/PlayMode real")]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 17 ya esta ejecutandose.");
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
        {
            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(StageKey, cli ? "run_cli" : "run_menu");
            playReadyAt = EditorApplication.timeSinceStartup + PlayReadyDelaySeconds;
        }
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
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage)) return;

        if (!EditorApplication.isPlaying)
        {
            if (!stage.StartsWith("exit_", StringComparison.Ordinal)) return;
            bool cliExit = stage.EndsWith("cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            SessionState.EraseString(StageKey);
            if (cliExit) EditorApplication.Exit(ok ? 0 : 1);
            return;
        }

        if (stage.StartsWith("enter_", StringComparison.Ordinal))
        {
            bool cliEnter = stage.EndsWith("cli", StringComparison.Ordinal);
            stage = cliEnter ? "run_cli" : "run_menu";
            SessionState.SetString(StageKey, stage);
            playReadyAt = EditorApplication.timeSinceStartup + PlayReadyDelaySeconds;
        }

        if (!stage.StartsWith("run_", StringComparison.Ordinal)) return;
        if (playReadyAt <= 0d)
            playReadyAt = EditorApplication.timeSinceStartup + PlayReadyDelaySeconds;
        if (EditorApplication.timeSinceStartup < playReadyAt) return;

        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        try
        {
            RunRuntimeProbe();
            Finish(true, "PASS - navegacion hibrida, accesos, congestion, derechos espaciales BBSIS, " +
                "obstaculos dinamicos y aproximacion operativa funcionan en runtime.", cli);
        }
        catch (Exception exception)
        {
            Finish(false, "17 PlayMode: " + exception.Message, cli);
        }
    }    private static void RunRuntimeProbe()
    {
        BistroBuilderNavigationService navigation =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>();
        BistroBuilderWaiterRoutingService waiterRouting =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderWaiterRoutingService>();
        GameObject entrance = GameObject.Find("RestaurantEntrancePoint");
        KitchenSystem kitchen = UnityEngine.Object.FindFirstObjectByType<KitchenSystem>();
        RestaurantTable[] tables = UnityEngine.Object.FindObjectsByType<RestaurantTable>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);

        if (navigation == null || waiterRouting == null || entrance == null ||
            kitchen == null || kitchen.PickupPoint == null || tables.Length == 0)
            throw new InvalidOperationException("Faltan autoridades o puntos runtime del Bloque 17.");

        navigation.RebuildNavigationTopology();
        BistroBuilderCirculationHealthReport health = navigation.EvaluateCirculationHealth();
        if (health == null || !health.IsOperational || health.checkedConnections < 1)
            throw new InvalidOperationException("La salud de circulacion runtime no es operativa.");

        bool usedOperationalDock = false;
        bool checkedCustomerAccess = false;
        for (int i = 0; i < tables.Length; i++)
        {
            RestaurantTable table = tables[i];
            if (table == null) continue;
            if (table.CustomerApproachPoint != null)
            {
                var route = new List<Vector3>(32);
                if (!navigation.TryBuildRoute("play17:c:" + i,
                        BistroBuilderNavigationAgentMask.Customer,
                        entrance.transform.position,
                        table.CustomerApproachPoint.position,
                        route, out _, out BistroBuilderNavigationRouteKind kind))
                    throw new InvalidOperationException("Un cliente no pudo alcanzar una mesa real.");
                usedOperationalDock |= kind == BistroBuilderNavigationRouteKind.OperationalDock;
                checkedCustomerAccess = true;
            }
            if (table.WaiterServicePoint != null)
            {
                var route = new List<Vector3>(32);
                if (!navigation.TryBuildRoute("play17:w:" + i,
                        BistroBuilderNavigationAgentMask.Waiter,
                        kitchen.PickupPoint.position,
                        table.WaiterServicePoint.position,
                        route, out _, out BistroBuilderNavigationRouteKind kind))
                    throw new InvalidOperationException("Un camarero no pudo alcanzar una mesa real.");
                usedOperationalDock |= kind == BistroBuilderNavigationRouteKind.OperationalDock;
            }
        }
        if (!checkedCustomerAccess)
            throw new InvalidOperationException("No se verificaron rutas reales de clientes.");
        if (!usedOperationalDock)
            throw new InvalidOperationException("La aproximacion operativa inteligente no entro en uso.");

        var waiterPoints = new List<Vector3>(32);
        RestaurantTable firstTable = tables[0];
        if (firstTable == null || firstTable.WaiterServicePoint == null ||
            !waiterRouting.TryBuildRoute(kitchen.PickupPoint.position,
                firstTable.WaiterServicePoint.position, waiterPoints, out float waiterMeters) ||
            waiterMeters <= 0f)
            throw new InvalidOperationException("La IA del Bloque 13 no delega en Navegacion 17.");

        Vector3 reservedA;
        Vector3 reservedB;
        if (!navigation.TryReserveDestination("play17:a", BistroBuilderNavigationAgentMask.Waiter,
                firstTable.WaiterServicePoint.position, 0.3f, 40, out reservedA) ||
            !navigation.TryReserveDestination("play17:b", BistroBuilderNavigationAgentMask.Waiter,
                firstTable.WaiterServicePoint.position, 0.3f, 40, out reservedB) ||
            (reservedA - reservedB).sqrMagnitude < 0.01f)
            throw new InvalidOperationException("Las reservas de destino no evitan conflictos.");
        navigation.ReleaseDestination("play17:a");
        navigation.ReleaseDestination("play17:b");
        GameObject obstacleObject = new GameObject("__BB17_RuntimeObstacle__");
        BistroBuilderDynamicCirculationEnvelope obstacle =
            obstacleObject.AddComponent<BistroBuilderDynamicCirculationEnvelope>();
        obstacle.ConfigureForEditor(Vector3.zero, new Vector2(0.9f, 0.9f),
            BistroBuilderNavigationAgentMask.All,
            BistroBuilderDynamicSpaceKind.TemporaryObstacle);
        obstacleObject.transform.position = firstTable.WaiterServicePoint.position;
        int previousTrafficEpoch = navigation.TrafficEpoch;
        obstacle.SetActiveWindow("play17:obstacle", true);
        navigation.NotifyDynamicSpaceChanged();
        if (navigation.TrafficEpoch <= previousTrafficEpoch)
            throw new InvalidOperationException("Un obstaculo dinamico no actualizo el estado transitorio de trafico.");
        if (navigation.CanAdvance("play17:outsider",
                BistroBuilderNavigationAgentMask.Waiter,
                obstacleObject.transform.position, 0.2f, 40))
            throw new InvalidOperationException("El espacio dinamico no bloqueo el paso.");
        obstacle.SetActiveWindow("play17:obstacle", false);
        navigation.NotifyDynamicSpaceChanged();
        UnityEngine.Object.Destroy(obstacleObject);

        GameObject doorObject = new GameObject("__BB17_RuntimeDoor__");
        doorObject.transform.position = firstTable.WaiterServicePoint.position + Vector3.right * 20f;
        BistroBuilderDoorCirculationEnvelope doorEnvelope =
            doorObject.AddComponent<BistroBuilderDoorCirculationEnvelope>();
        doorEnvelope.ConfigureForEditor(Vector3.zero, new Vector2(1.1f, 1.1f),
            BistroBuilderNavigationAgentMask.All, BistroBuilderDynamicSpaceKind.DoorSwing);
        BistroBuilderNavigableDoor door = doorObject.AddComponent<BistroBuilderNavigableDoor>();
        navigation.RegisterDynamicEnvelope(doorEnvelope);
        if (!door.TrySetOpen(true) || !door.IsMoving || !doorEnvelope.IsActive)
            throw new InvalidOperationException("La puerta no reservo su barrido antes de abrirse.");
        if (navigation.CanAdvance("play17:door-outsider", BistroBuilderNavigationAgentMask.Waiter,
                doorObject.transform.position, 0.2f, 40))
            throw new InvalidOperationException("El barrido de puerta no bloqueo el paso durante apertura.");
        navigation.UnregisterDynamicEnvelope(doorEnvelope);
        UnityEngine.Object.Destroy(doorObject);

        RestaurantSeat[] runtimeSeats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        RestaurantSeat motionSeat = null;
        for (int s = 0; s < runtimeSeats.Length; s++)
        {
            if (runtimeSeats[s] != null && runtimeSeats[s].UseProfile != null && runtimeSeats[s].OperationalState == RestaurantSeatOperationalState.Parked)
            {
                motionSeat = runtimeSeats[s];
                break;
            }
        }
        if (motionSeat == null)
            throw new InvalidOperationException("No hay silla real disponible para probar espacio dinamico.");
        BistroBuilderSeatCirculationEnvelope seatEnvelope =
            motionSeat.GetComponent<BistroBuilderSeatCirculationEnvelope>();
        if (seatEnvelope == null || !motionSeat.TryReserve("play17:seat") || !motionSeat.TryPullOut() ||
            !seatEnvelope.IsActive)
            throw new InvalidOperationException("La silla no reservo el espacio al desplazarse para sentar al cliente.");

        RestaurantArea[] areas = UnityEngine.Object.FindObjectsByType<RestaurantArea>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < areas.Length; i++)
        {
            RestaurantArea area = areas[i];
            if (area == null || (area.AreaId ?? string.Empty).IndexOf(
                    "kitchen", StringComparison.OrdinalIgnoreCase) < 0) continue;
            BistroBuilderNavigationAccessZone zone =
                area.GetComponent<BistroBuilderNavigationAccessZone>();
            if (zone == null || zone.Allows(BistroBuilderNavigationAgentMask.Customer) ||
                !zone.Allows(BistroBuilderNavigationAgentMask.Waiter))
                throw new InvalidOperationException("Los permisos de cocina no son coherentes.");
        }
    }
    private static void Finish(bool success, string message, bool cli)
    {
        string report = "=== BISTRO BUILDER - BLOQUE 17 / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message;
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}

