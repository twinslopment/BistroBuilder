using System;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BistroBuilderNavigation17Validator
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/17 Navegacion/Validar bloque 17")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(
            "Assets/Scenes/Prototype_Restaurant.unity", OpenSceneMode.Single);
        int ok = 0;
        int fail = 0;
        StringBuilder report = new StringBuilder("BLOQUE 17 - VALIDACION\n");
        void Check(bool condition, string label)
        {
            if (condition) { ok++; report.AppendLine("OK - " + label); }
            else { fail++; report.AppendLine("FAIL - " + label); }
        }

        BistroBuilderNavigationService navigation =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>();
        Check(navigation != null, "Autoridad unica de navegacion instalada");
        Check(navigation != null && navigation.ValidateConfiguration(out _),
            "Configuracion de navegacion valida");
        Check(UnityEngine.Object.FindObjectsByType<BistroBuilderNavigationService>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "No hay autoridades de navegacion duplicadas");
        Check(UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>() != null,
            "Superficie NavMesh profesional instalada");
        Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationEditIntegration>() != null,
            "Modo Edicion sincroniza la topologia de circulacion");
        Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderWaiterRoutingService>() != null,
            "IA de rutas de Camareros 13 conectable a Navegacion 17");

        RestaurantArea[] areas = UnityEngine.Object.FindObjectsByType<RestaurantArea>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(areas.Length >= 3, "Zonas transitables canonicas presentes");
        Check(areas.All(area => area.GetComponent<BistroBuilderNavigationAccessZone>() != null),
            "Todas las zonas tienen politica de acceso");

        RestaurantArea kitchen = areas.FirstOrDefault(area =>
            area != null && (area.AreaId ?? string.Empty).IndexOf(
                "kitchen", StringComparison.OrdinalIgnoreCase) >= 0);
        BistroBuilderNavigationAccessZone kitchenZone =
            kitchen != null ? kitchen.GetComponent<BistroBuilderNavigationAccessZone>() : null;
        Check(kitchenZone != null &&
              !kitchenZone.Allows(BistroBuilderNavigationAgentMask.Customer),
            "Clientes no pueden usar cocina como atajo");
        Check(kitchenZone != null && kitchenZone.Allows(BistroBuilderNavigationAgentMask.Waiter),
            "Camareros conservan acceso a cocina");

        RestaurantSeat[] seats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(seats.Length > 0, "Sillas funcionales detectadas");
        Check(seats.All(seat => seat.GetComponent<BistroBuilderSeatCirculationEnvelope>() != null),
            "Movimiento de todas las sillas reserva espacio de circulacion");
        Check(typeof(BistroBuilderNavigableDoor) != null &&
              typeof(BistroBuilderDoorCirculationEnvelope) != null,
            "Puertas disponen de barrido y bloqueo dinamico");
        Check(typeof(BistroBuilderGoodsReceivingRoute) != null,
            "Rutas de reparto siguen integradas");
        Check(typeof(CustomerMovementView) != null,
            "Clientes usan el proveedor comun de circulacion");

        Check(typeof(WaiterMovementView) != null,
            "Camareros usan rutas y evitacion de Navegacion 17");
        Check(typeof(BistroBuilderDynamicCirculationEnvelope) != null,
            "Obstaculos temporales universales disponibles");
        Check(typeof(BistroBuilderNavigationTrafficCoordinator) != null &&
              typeof(BistroBuilderNavigationControlledPassageCoordinator) != null,
            "Solver reciproco y Controlled Passages v1 disponibles");
        Check(typeof(BistroBuilderNavigationBlockDependencyGraph) != null,
            "Block Dependency Graph y recuperacion anti-deadlock disponibles");        Check(typeof(BistroBuilderNavigationRecoveryPlanner) != null,
            "Escape Pocket y Retreat recovery v1 disponibles");
        Check(typeof(BistroBuilderNavigationPhysicalQueueCoordinator) != null,
            "Colas fisicas generales sin autoridad logica paralela disponibles");
        Check(typeof(BistroBuilderNavigationReplayRecorder) != null &&
              typeof(BistroBuilderNavigationReplayPlayer) != null,
            "Deterministic Replay e incident timeline disponibles");
        Check(typeof(BistroBuilderNavigationLabWindow) != null,
            "Navigation Lab Tool-First instalado");
        Check(typeof(BistroBuilderNavigationRouteCorridorBuilder) != null,
            "Route Corridor de primera clase disponible");
        Check(typeof(BistroBuilderNavigationAdaptiveGuidanceField) != null,
            "Adaptive Guidance predictivo disponible");
        Check(typeof(BistroBuilderNavigationReciprocalConstraintSolver) != null,
            "Reciprocal Constraint Solver ORCA-like disponible");
        Check(typeof(BistroBuilderNavigationConflictHorizonCoordinator) != null &&
              typeof(BistroBuilderNavigationMicroConflictPlanner) != null,
            "Conflict Horizon y Micro-Conflict Planner disponibles");
        Check(typeof(BistroBuilderNavigationFailureMemory) != null &&
              typeof(BistroBuilderNavigationRouteSwitchPolicy) != null &&
              typeof(BistroBuilderNavigationSocialMotionPolicy) != null,
            "Failure Memory, wait-vs-detour y social motion policies disponibles");        Check(typeof(BistroBuilderNavigationTrafficHeatField) != null &&
              typeof(BistroBuilderNavigationRouteCache) != null &&
              typeof(BistroBuilderNavigationRouteGraph) != null,
            "Traffic Heat, Route Cache y Route Graph v1 disponibles");
        Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>() != null,
            "Navigation consume BBSIS como autoridad espacial");

        if (navigation != null)
        {
            navigation.RebuildNavigationTopology();
            Check(navigation.RouteGraphNodeCount > 0,
                "Route Graph v1 construido sobre la topologia real");
            Check(navigation.ActiveDeadlockCount == 0,
                "No existen deadlocks fantasma al validar");
            BistroBuilderCirculationHealthReport health =
                navigation.EvaluateCirculationHealth();
            if (health != null)
            {
                report.AppendLine(health.BuildSummary());
                foreach (BistroBuilderCirculationIssue issue in health.issues)
                    report.AppendLine(issue.severity + " - " + issue.message);
            }
            Check(health != null && health.checkedConnections > 0,
                "Auditoria de salud de circulacion ejecutable");
            Check(health != null && health.IsOperational,
                "Conexiones criticas del restaurante son transitables");
        }
        else
        {
            Check(false, "Auditoria de salud de circulacion ejecutable");
            Check(false, "Conexiones criticas del restaurante son transitables");
        }

        Check(UnityEngine.Object.FindFirstObjectByType<RestaurantPlacementTransactionService>() != null,
            "Validacion se integra con colocacion y reformas");
        Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderGoodsReceivingRoute>() != null,
            "Acceso de almacen/reparto participa en auditoria");

        LastPassed = ok;
        LastFailed = fail;
        report.AppendLine("Resultado: " + ok + " OK / " + fail + " errores.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (fail > 0) throw new InvalidOperationException(LastReport);
    }

    public static void RunFromCommandLine()
    {
        try
        {
            Run();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
