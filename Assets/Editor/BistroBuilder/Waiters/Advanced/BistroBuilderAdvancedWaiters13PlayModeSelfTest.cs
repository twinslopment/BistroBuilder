using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderAdvancedWaiters13PlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Waiters13.Play.Stage";
    private const string SuccessKey = "BB.Waiters13.Play.Success";
    private const string ReportPath = "AdvancedWaiters13PlayModeReport.txt";

    private static readonly List<GameObject> temporaryObjects = new List<GameObject>();
    private static BistroBuilderAdvancedWaiterService advanced;
    private static BistroBuilderWaiterRoutingService routing;
    private static Waiter movingWaiter;
    private static WaiterMovementView movement;
    private static Transform movementDestination;
    private static bool destinationReached;
    private static double phaseStarted;

    static BistroBuilderAdvancedWaiters13PlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Tools/Bistro Builder/Waiters/13 - PlayMode real", false, 13003)]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode 13 ya esta ejecutandose.");

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
        if (!EditorApplication.isPlaying || Time.frameCount < 5) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage) || stage.StartsWith("exit_", StringComparison.Ordinal)) return;
        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);

        try
        {
            if (stage.StartsWith("run_", StringComparison.Ordinal))
            {
                SetupAndVerifyPlanning();
                BeginMovementProbe();
                phaseStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StageKey, cli ? "moving_cli" : "moving_menu");
                return;
            }

            if (stage.StartsWith("moving_", StringComparison.Ordinal))
            {
                if (!destinationReached)
                {
                    if (EditorApplication.timeSinceStartup - phaseStarted > 5d)
                        throw new TimeoutException("El camarero no completo la ruta funcional.");
                    return;
                }

                if (movingWaiter == null || movementDestination == null ||
                    Vector3.Distance(movingWaiter.transform.position, movementDestination.position) > 0.08f)
                    throw new InvalidOperationException("La ruta termino fuera del destino operativo.");

                if (movement.CurrentRouteKind != BistroBuilderWaiterRouteKind.NavMeshOptimal &&
                    movement.CurrentRouteKind != BistroBuilderWaiterRouteKind.DirectFallback &&
                    movement.CurrentRouteKind != BistroBuilderWaiterRouteKind.ExternalProfessional)
                    throw new InvalidOperationException("El movimiento no uso un proveedor de ruta valido.");

                Finish(true,
                    "PASS - planificacion multitarea, prioridades, responsabilidades, apoyo, " +
                    "saturacion, reserva de destinos y movimiento por mejores rutas son coherentes.", cli);
            }
        }
        catch (Exception exception)
        {
            Finish(false, "13 PlayMode: " + Unwrap(exception).Message, cli);
        }
    }

    private static void SetupAndVerifyPlanning()
    {
        advanced = Find<BistroBuilderAdvancedWaiterService>();
        routing = Find<BistroBuilderWaiterRoutingService>();
        BistroBuilderAdvancedWaiterPlayerScreen screen = Find<BistroBuilderAdvancedWaiterPlayerScreen>();
        WaiterTaskCoordinator coordinator = Find<WaiterTaskCoordinator>();
        BistroBuilderCustomerExperienceTrackingService experience =
            Find<BistroBuilderCustomerExperienceTrackingService>();

        if (advanced == null || routing == null || screen == null ||
            coordinator == null || experience == null)
            throw new InvalidOperationException("Faltan autoridades runtime del Bloque 13.");

        if (!advanced.ValidateConfiguration(out string advancedError))
            throw new InvalidOperationException(advancedError);
        if (!screen.ValidateConfiguration(out string screenError))
            throw new InvalidOperationException(screenError);

        Waiter waiterA = CreateWaiter("BB13_WaiterA", 1301, Vector3.zero, "dining");
        Waiter waiterB = CreateWaiter("BB13_WaiterB", 1302, new Vector3(8f, 0f, 0f), "bar");
        RestaurantTable tableA = CreateTable("BB13_TableA", 1301, new Vector3(2f, 0f, 0f));
        RestaurantTable tableB = CreateTable("BB13_TableB", 1302, new Vector3(5f, 0f, 2f));
        RestaurantTable tableC = CreateTable("BB13_TableC", 1303, new Vector3(9f, 0f, 0f));

        var tasks = new List<WaiterTask>
        {
            new WaiterTask(13001, WaiterTaskType.TakeOrder, WaiterTaskPriority.Normal, tableA, null, 1),
            new WaiterTask(13002, WaiterTaskType.DeliverBill, WaiterTaskPriority.High, tableB, null, 2),
            new WaiterTask(13003, WaiterTaskType.CleanTable, WaiterTaskPriority.Urgent, tableC, null, 3)
        };
        var waiters = new List<Waiter> { waiterA, waiterB };
        advanced.RebuildPlans(tasks, waiters, ResolveTaskPosition);

        if (!advanced.TryBuildSnapshot(waiterA, out BistroBuilderAdvancedWaiterSnapshot snapshotA) ||
            !advanced.TryBuildSnapshot(waiterB, out BistroBuilderAdvancedWaiterSnapshot snapshotB))
            throw new InvalidOperationException("No pudieron construirse snapshots de planificacion.");

        if (snapshotA.plannedTaskCount + snapshotB.plannedTaskCount < 3)
            throw new InvalidOperationException("La IA no mantuvo multiples tareas planificadas.");

        if (snapshotA.saturation == BistroBuilderWaiterSaturationLevel.Free &&
            snapshotB.saturation == BistroBuilderWaiterSaturationLevel.Free)
            throw new InvalidOperationException("La saturacion individual no refleja la cartera de trabajo.");

        bool hasSecondarySupport = false;
        bool hasRouteEstimate = false;
        InspectPlans(snapshotA, ref hasSecondarySupport, ref hasRouteEstimate);
        InspectPlans(snapshotB, ref hasSecondarySupport, ref hasRouteEstimate);
        if (!hasSecondarySupport)
            throw new InvalidOperationException("La planificacion no uso responsabilidad secundaria de apoyo.");
        if (!hasRouteEstimate)
            throw new InvalidOperationException("La planificacion no incorporo coste de ruta.");

        WaiterTask sameDestination = new WaiterTask(
            13004, WaiterTaskType.CleanTable, WaiterTaskPriority.Normal, tableA, null, 4);
        advanced.NotifyTaskAssigned(waiterA, tasks[0]);
        Waiter reservedWinner = advanced.FindBestAvailableWaiter(
            sameDestination, waiters, ResolveTaskPosition(sameDestination), null, out _);
        if (!ReferenceEquals(reservedWinner, waiterA))
            throw new InvalidOperationException("La reserva de destino permitio competir por el mismo punto.");
        advanced.NotifyTaskEnded(tasks[0]);

        if (!routing.TryBuildRoute(
                Vector3.zero, new Vector3(4f, 0f, 3f),
                new List<Vector3>(), out float routeMeters) || routeMeters <= 0f)
            throw new InvalidOperationException("El motor IA no pudo estimar una ruta valida.");
    }

    private static void InspectPlans(
        BistroBuilderAdvancedWaiterSnapshot snapshot,
        ref bool hasSecondarySupport,
        ref bool hasRouteEstimate)
    {
        if (snapshot == null) return;
        for (int i = 0; i < snapshot.plans.Count; i++)
        {
            BistroBuilderAdvancedWaiterTaskPlan plan = snapshot.plans[i];
            if (plan == null) continue;
            if (plan.responsibility == BistroBuilderWaiterResponsibilityKind.Secondary ||
                plan.responsibility == BistroBuilderWaiterResponsibilityKind.Support)
                hasSecondarySupport = true;
            if (plan.estimatedRouteMeters > 0.01f)
                hasRouteEstimate = true;
        }
    }

    private static void BeginMovementProbe()
    {
        movingWaiter = CreateWaiter(
            "BB13_MovingWaiter", 1399, new Vector3(-1f, 0f, -1f), "dining");
        RestaurantTable table = CreateTable(
            "BB13_MovementTable", 1399, new Vector3(1f, 0f, 1f));
        movementDestination = table.WaiterServicePoint;

        movement = movingWaiter.gameObject.AddComponent<WaiterMovementView>();
        SetPrivate(movement, "routingService", routing);
        SetPrivate(movement, "movementSpeed", 12f);
        movement.DestinationReached += HandleDestinationReached;

        table.SetState(TableState.WaitingForWaiter);
        if (!movingWaiter.AssignTable(table))
            throw new InvalidOperationException("El camarero de prueba no acepto la tarea de movimiento.");
    }

    private static void HandleDestinationReached(WaiterMovementView view)
    {
        if (ReferenceEquals(view, movement)) destinationReached = true;
    }

    private static Waiter CreateWaiter(
        string name,
        int waiterId,
        Vector3 position,
        string primaryZone)
    {
        GameObject go = new GameObject(name);
        temporaryObjects.Add(go);
        go.transform.position = position;
        Waiter waiter = go.AddComponent<Waiter>();
        SetPrivate(waiter, "waiterId", waiterId);
        BistroBuilderAdvancedWaiterProfile profile =
            go.AddComponent<BistroBuilderAdvancedWaiterProfile>();
        string secondaryZone = primaryZone == "dining" ? "bar" : "dining";
        if (!profile.TryRestorePersistentSettings(
                primaryZone,
                new List<string> { secondaryZone },
                2,
                1f,
                out string profileError))
            throw new InvalidOperationException(profileError);
        return waiter;
    }

    private static RestaurantTable CreateTable(
        string name,
        int tableId,
        Vector3 position)
    {
        GameObject go = new GameObject(name);
        temporaryObjects.Add(go);
        go.transform.position = position;
        RestaurantTable table = go.AddComponent<RestaurantTable>();
        table.AssignTableId(tableId);
        GameObject servicePoint = new GameObject(name + "_ServicePoint");
        servicePoint.transform.SetParent(go.transform, false);
        servicePoint.transform.localPosition = Vector3.zero;
        SetPrivate(table, "waiterServicePoint", servicePoint.transform);
        return table;
    }

    private static Vector3 ResolveTaskPosition(WaiterTask task)
    {
        if (task == null) return Vector3.zero;
        Transform point = BistroBuilderServiceModeUtility.GetWaiterServicePoint(
            task.Table,
            task.BarSpot
        );
        if (point != null) return point.position;
        if (task.Table != null) return task.Table.transform.position;
        return task.BarSpot != null ? task.BarSpot.transform.position : Vector3.zero;
    }

    private static void Finish(bool success, string message, bool cli)
    {
        string report = "=== BISTRO BUILDER - BLOQUE 13 / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message;
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);

        CleanupFixture();
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }

    private static void CleanupFixture()
    {
        if (movement != null)
            movement.DestinationReached -= HandleDestinationReached;

        for (int i = temporaryObjects.Count - 1; i >= 0; i--)
        {
            GameObject go = temporaryObjects[i];
            if (go == null) continue;
            Waiter waiter = go.GetComponent<Waiter>();
            if (waiter != null && advanced != null)
                advanced.UnregisterWaiter(waiter);
            UnityEngine.Object.Destroy(go);
        }
        temporaryObjects.Clear();
        advanced?.ResetForRuntimeLoad();
        movingWaiter = null;
        movement = null;
        movementDestination = null;
        destinationReached = false;
    }

    private static T Find<T>() where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException invocation &&
               invocation.InnerException != null)
            exception = invocation.InnerException;
        return exception;
    }
}
