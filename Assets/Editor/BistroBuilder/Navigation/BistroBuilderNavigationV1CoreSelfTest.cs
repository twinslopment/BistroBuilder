using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Autotest determinista de los algoritmos propios de Navigation & Crowd Flow v1.
/// </summary>
public static class BistroBuilderNavigationV1CoreSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";

    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/17 Navegacion/V1 Ejecutar core autotests")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int ok = 0;
        int fail = 0;
        var report = new StringBuilder("NAVIGATION & CROWD FLOW V1 - CORE AUTOTEST\n");
        void Check(bool condition, string label)
        {
            if (condition) { ok++; report.AppendLine("OK - " + label); }
            else { fail++; report.AppendLine("FAIL - " + label); }
        }

        TestReciprocalTraffic(Check);
        TestControlledPassage(Check);
        TestBlockDependencyGraph(Check);
        TestTrafficHeat(Check);
        TestAdaptiveGuidance(Check);
        TestReciprocalConstraintKernel(Check);
        TestConflictHorizon(Check);
        TestFailureMemory(Check);
        TestRouteSwitchPolicy(Check);
        TestSocialMotionPolicy(Check);
        TestRouteCorridor(Check);
        TestRouteCache(Check);
        TestPathQueryScheduler(Check);
        TestRecoveryPlanner(Check);
        TestPhysicalQueues(Check);
        TestReplay(Check);
        TestSceneIntegration(Check);

        LastPassed = ok;
        LastFailed = fail;
        report.AppendLine("Resultado: " + ok + " OK / " + fail + " fallos.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (fail > 0) throw new InvalidOperationException(LastReport);
    }

    private static void TestReciprocalTraffic(Action<bool, string> check)
    {
        var traffic = new BistroBuilderNavigationTrafficCoordinator();
        traffic.UpsertPresence(
            "a", BistroBuilderNavigationAgentMask.Other,
            new Vector3(0f, 0f, -0.4f), Vector3.forward,
            0.3f, 0, 0f, 0f, 0f, 0f);
        traffic.UpsertPresence(
            "b", BistroBuilderNavigationAgentMask.Other,
            new Vector3(0f, 0f, 0.4f), Vector3.back,
            0.3f, 0, 0f, 0f, 0f, 0f);

        BistroBuilderNavigationLocalMoveDecision a = traffic.Solve(
            new BistroBuilderNavigationLocalMoveInput
            {
                ownerId = "a",
                agentMask = BistroBuilderNavigationAgentMask.Other,
                position = new Vector3(0f, 0f, -0.4f),
                preferredVelocity = Vector3.forward,
                radius = 0.3f
            }, 0f);
        BistroBuilderNavigationLocalMoveDecision b = traffic.Solve(
            new BistroBuilderNavigationLocalMoveInput
            {
                ownerId = "b",
                agentMask = BistroBuilderNavigationAgentMask.Other,
                position = new Vector3(0f, 0f, 0.4f),
                preferredVelocity = Vector3.back,
                radius = 0.3f
            }, 0f);

        check(!a.shouldYield && b.shouldYield,
            "Encounter determinista resuelve dos agentes de frente");
        check(a.passingSide == 1 && b.passingSide == 1,
            "Passing side permanece estable a la derecha");
        check(traffic.TryGetEncounter("a", "b", out var encounter) &&
              encounter.preferredOwnerId == "a",
            "Empate usa Stable Agent ID y no aleatoriedad");
    }

    private static void TestControlledPassage(Action<bool, string> check)
    {
        var coordinator = new BistroBuilderNavigationControlledPassageCoordinator();
        coordinator.Configure(new[]
        {
            new BistroBuilderNavigationGateDescriptor
            {
                gateId = "test.gate",
                subjectId = "test.subject",
                start = new Vector3(-0.35f, 0f, 0f),
                end = new Vector3(0.35f, 0f, 0f),
                minimumWidth = 0.7f,
                criticalRoute = true
            }
        });

        coordinator.UpdateAgentPosition("a", new Vector3(0f, 0f, -0.7f), 0f);
        coordinator.UpdateAgentPosition("b", new Vector3(0f, 0f, 0.7f), 0f);
        bool aGranted = coordinator.TryAuthorizeStep(
            "a", new Vector3(0f, 0f, -0.7f), new Vector3(0f, 0f, -0.2f),
            0.3f, 0, 0f, 0f, out _);
        bool bGranted = coordinator.TryAuthorizeStep(
            "b", new Vector3(0f, 0f, 0.7f), new Vector3(0f, 0f, 0.2f),
            0.3f, 0, 0f, 0.05f, out var bWait);

        check(aGranted && !bGranted &&
              bWait.waitingReason == BistroBuilderNavigationWaitingReason.ControlledPassage,
            "Controlled Passage impide cruce incompatible simultaneo");

        coordinator.UpdateAgentPosition("a", new Vector3(0f, 0f, 1.5f), 0.2f);
        bool bAfterDrain = coordinator.TryAuthorizeStep(
            "b", new Vector3(0f, 0f, 0.7f), new Vector3(0f, 0f, 0.2f),
            0.3f, 0, 0.2f, 0.3f, out _);
        check(bAfterDrain,
            "Controlled Passage drena y cambia de sentido sin starvation");
        check(coordinator.WaitCount > 0,
            "Controlled Passage registra espera observable");
    }

    private static void TestBlockDependencyGraph(Action<bool, string> check)
    {
        var graph = new BistroBuilderNavigationBlockDependencyGraph();
        graph.SetDependency("a", "b", 0f);
        graph.SetDependency("b", "c", 0f);
        graph.SetDependency("c", "a", 0f);
        var cycle = new List<string>();
        bool found = graph.TryFindCycle("b", cycle, out string signature);
        check(found && cycle.Count == 3 && signature == "a>b>c",
            "Block Dependency Graph detecta y canoniza ciclos");
        string candidate = graph.SelectRecoveryCandidate(cycle, _ => 0f);
        check(candidate == "c",
            "Deadlock recovery tiene desempate determinista");
    }

    private static void TestTrafficHeat(Action<bool, string> check)
    {
        var heat = new BistroBuilderNavigationTrafficHeatField();
        for (int i = 0; i < 5; i++)
            heat.UpsertAgent(
                "agent:" + i,
                new Vector3(0.2f + i * 0.03f, 0f, 0.2f),
                Vector3.zero,
                0.3f,
                0f);
        heat.Rebuild(0f);
        float penalty = heat.QueryPenalty(new Vector3(0.2f, 0f, 0.2f));
        check(penalty > 0f,
            "Traffic Heat convierte densidad y baja velocidad en coste");
        check(heat.ActiveCellCount > 0,
            "Traffic Heat mantiene celdas compactas de telemetria");
    }

    private static void TestAdaptiveGuidance(Action<bool, string> check)
    {
        var guidance = new BistroBuilderNavigationAdaptiveGuidanceField();
        guidance.Observe("a", Vector3.zero, Vector3.zero, Vector3.forward, true, 0f);
        guidance.ObserveDirection(Vector3.zero, Vector3.forward);
        guidance.ReportIncident(Vector3.zero, 1f, 0f);
        guidance.Rebuild(0f);
        float forward = guidance.QueryPenalty(Vector3.zero, Vector3.forward, 0f);
        float reverse = guidance.QueryPenalty(Vector3.zero, Vector3.back, 0f);
        check(forward > 0f, "Adaptive Guidance convierte espera e incidentes en coste predictivo");
        check(reverse > forward, "Adaptive Guidance penaliza contraflujo respecto a la direccion dominante");
    }

    private static void TestReciprocalConstraintKernel(Action<bool, string> check)
    {
        var solver = new BistroBuilderNavigationReciprocalConstraintSolver();
        bool added = solver.AddAgentConstraint(
            Vector3.zero, Vector3.zero, 0.3f,
            new Vector3(0f, 0f, 0.7f), Vector3.back, 0.3f,
            "b", 1.1f, 0.5f);
        Vector3 solved = solver.Solve(Vector3.forward, Vector3.zero, 1f, 20f, 0.05f);
        check(added && solver.LastConstraintCount > 0,
            "Reciprocal Constraint Solver genera restricciones ORCA-like acotadas");
        check(solved.sqrMagnitude <= 1.0001f && solved.z < 0.999f,
            "Reciprocal Constraint Solver proyecta velocidad segura con limite de velocidad");
    }

    private static void TestConflictHorizon(Action<bool, string> check)
    {
        var coordinator = new BistroBuilderNavigationConflictHorizonCoordinator();
        coordinator.Configure(new[]
        {
            new BistroBuilderNavigationGateDescriptor
            {
                gateId = "horizon.gate", subjectId = "subject",
                start = new Vector3(-0.4f, 0f, 0f), end = new Vector3(0.4f, 0f, 0f),
                minimumWidth = 0.8f, criticalRoute = true
            }
        });
        bool a = coordinator.TryCoordinate("a", new Vector3(0f,0f,-1f), new Vector3(0f,0f,-0.6f),
            0.28f, 1f, 0f, 0f, out _);
        bool b = coordinator.TryCoordinate("b", new Vector3(0f,0f,1f), new Vector3(0f,0f,0.6f),
            0.28f, 1f, 0f, 0.1f, out var bDecision);
        coordinator.TryCoordinate("c", new Vector3(0.1f,0f,1f), new Vector3(0.1f,0f,0.6f),
            0.28f, 1f, 0f, 0.15f, out _);
        check(a && !b && !string.IsNullOrEmpty(bDecision.blockerId),
            "Conflict Horizon anticipa trafico opuesto antes del cuello critico");
        check(coordinator.ArbitrationCount > 0 && coordinator.MicroPlanCount > 0,
            "Conflict Horizon activa arbitraje multiagente y Micro-Conflict Planner");
    }

    private static void TestFailureMemory(Action<bool, string> check)
    {
        var memory = new BistroBuilderNavigationFailureMemory();
        memory.RecordFailure("a", BistroBuilderNavigationRecoveryStage.EscapePocket, "sig", 0f);
        check(memory.IsSuppressed("a", BistroBuilderNavigationRecoveryStage.EscapePocket, "sig", 0.2f),
            "Failure Memory suprime recovery repetida bajo la misma firma");
        memory.RecordSuccess("a", BistroBuilderNavigationRecoveryStage.EscapePocket, "sig");
        check(!memory.IsSuppressed("a", BistroBuilderNavigationRecoveryStage.EscapePocket, "sig", 0.3f),
            "Failure Memory libera la firma tras una recovery exitosa");
    }

    private static void TestRouteSwitchPolicy(Action<bool, string> check)
    {
        var policy = new BistroBuilderNavigationRouteSwitchPolicy();
        bool first = policy.ShouldSwitch("a", "alt", 10f, 7f, 3f, out _);
        bool stable = policy.ShouldSwitch("a", "alt", 10f, 7f, 3.8f, out float advantage);
        bool flapping = policy.ShouldSwitch("a", "alt2", 10f, 6f, 4.0f, out _);
        check(!first && stable && advantage > 2.5f,
            "Wait-vs-detour exige ventaja ETA estable antes de cambiar ruta");
        check(!flapping, "Route switch cooldown evita route flapping inmediato");
    }

    private static void TestSocialMotionPolicy(Action<bool, string> check)
    {
        var builder = new BistroBuilderNavigationRouteCorridorBuilder();
        var route = new List<Vector3> { new Vector3(0f,0f,2f), new Vector3(0f,0f,4f) };
        var topology = new BistroBuilderNavigationTopologySnapshot
        { spatialRevision = 1, navigationRevision = 1, trafficEpoch = 1, flowQuality = 1f };
        builder.TryBuild(Vector3.zero, route, 0.25f, topology, (a,b) => Mathf.Abs(b.x) <= 0.75f,
            out BistroBuilderNavigationRouteCorridor corridor);
        var social = new BistroBuilderNavigationSocialMotionPolicy();
        bool overtake = social.CanOvertake(corridor, new Vector3(0f,0f,1f), 2f, 1f, false, out int side);
        Vector3 bias = social.ComputeLooseGroupBias(corridor, new Vector3(0f,0f,1f),
            new Vector3(0.65f,0f,2f), Vector3.forward, 1f);
        check(overtake && side != 0, "Overtaking solo se habilita con holgura y ventaja de velocidad");
        check(bias.sqrMagnitude > 0f, "Loose Group Cohesion genera solo un bias blando dentro de corredor amplio");
    }
    private static void TestRouteCorridor(Action<bool, string> check)
    {
        var builder = new BistroBuilderNavigationRouteCorridorBuilder();
        var route = new List<Vector3>
        {
            new Vector3(0f, 0f, 1.5f),
            new Vector3(0f, 0f, 3f)
        };
        var topology = new BistroBuilderNavigationTopologySnapshot
        {
            spatialRevision = 4,
            navigationRevision = 7,
            trafficEpoch = 2,
            flowQuality = 1f
        };
        bool built = builder.TryBuild(
            Vector3.zero, route, 0.28f, topology,
            (a, b) => Mathf.Abs(b.x) <= 0.61f,
            out BistroBuilderNavigationRouteCorridor corridor);
        check(built && corridor != null && corridor.IsUsable &&
              corridor.samples.Count >= 4 && corridor.AverageClearance > 0.4f,
            "Route Corridor certifica holgura lateral sobre una ruta estructural");

        bool projected = builder.TryProject(
            corridor, new Vector3(0.3f, 0f, 1.2f), out var projection);
        check(projected && projection.signedLateral > 0.2f &&
              builder.Contains(corridor, new Vector3(0.3f, 0f, 1.2f)),
            "Route Corridor proyecta desviaciones validas sin replan");

        bool bypass = builder.TryFindBypass(
            corridor, new Vector3(0f, 0f, 0.8f), 0.5f, 1,
            (a, b) => Mathf.Abs(b.x) <= 0.61f,
            out Vector3 bypassTarget);
        check(bypass && bypassTarget.z > 0.8f && Mathf.Abs(bypassTarget.x) > 0.1f,
            "Route Corridor propone bypass lateral determinista dentro de su holgura");
    }

    private static void TestRouteCache(Action<bool, string> check)
    {
        var cache = new BistroBuilderNavigationRouteCache();
        var topology = new BistroBuilderNavigationTopologySnapshot
        {
            spatialRevision = 10,
            navigationRevision = 20,
            trafficEpoch = 1,
            flowQuality = 1f
        };
        Vector3 origin = Vector3.zero;
        Vector3 destination = new Vector3(2f, 0f, 2f);
        var route = new BistroBuilderNavigationRoute
        {
            kind = BistroBuilderNavigationRouteKind.NavMesh,
            lengthMeters = 2.8f,
            isComplete = true
        };
        route.points.Add(destination);
        cache.Store(
            topology, BistroBuilderNavigationAgentMask.Customer,
            origin, destination, 0.3f, route);
        bool hit = cache.TryGet(
            topology, BistroBuilderNavigationAgentMask.Customer,
            origin, destination, 0.3f, out BistroBuilderNavigationRoute cached);
        check(hit && cached != null && cached.points.Count == 1,
            "Structural Route Cache reutiliza rutas por revision");

        bool unsafeLargerHit = cache.TryGet(
            topology, BistroBuilderNavigationAgentMask.Customer,
            origin, destination, 0.39f, out _);
        check(!unsafeLargerHit,
            "Route Cache no reutiliza una ruta validada con menor Mobility Envelope");

        cache.Store(
            topology, BistroBuilderNavigationAgentMask.Customer,
            origin, destination, 0.39f, route);
        bool safeSmallerHit = cache.TryGet(
            topology, BistroBuilderNavigationAgentMask.Customer,
            origin, destination, 0.31f, out _);
        check(safeSmallerHit,
            "Route Cache permite reutilizar una ruta validada con envelope mayor");

        Vector3 impossible = new Vector3(99f, 0f, 99f);
        cache.StoreUnavailable(
            topology, BistroBuilderNavigationAgentMask.Customer,
            origin, impossible, 0.3f);
        check(cache.IsKnownUnavailable(
                topology, BistroBuilderNavigationAgentMask.Customer,
                origin, impossible, 0.3f),
            "Negative Route Cache evita repetir rutas conocidas imposibles");

        Vector3 tightImpossible = new Vector3(88f, 0f, 88f);
        cache.StoreUnavailable(
            topology, BistroBuilderNavigationAgentMask.Customer,
            origin, tightImpossible, 0.39f);
        check(!cache.IsKnownUnavailable(
                topology, BistroBuilderNavigationAgentMask.Customer,
                origin, tightImpossible, 0.31f),
            "Negative Cache no descarta un envelope menor por fallo de uno mayor");
    }

    private static void TestPathQueryScheduler(Action<bool, string> check)
    {
        var scheduler = new BistroBuilderNavigationPathQueryScheduler();
        scheduler.Enqueue("new:a", BistroBuilderNavigationQueryPriority.NewTrip, 0);
        scheduler.Enqueue("recovery:b", BistroBuilderNavigationQueryPriority.Recovery, 0);
        scheduler.Enqueue("new:c", BistroBuilderNavigationQueryPriority.NewTrip, 50);
        scheduler.Enqueue("new:a", BistroBuilderNavigationQueryPriority.Recovery, 10);

        var firstBatch = new List<string>();
        scheduler.Dequeue(2, firstBatch);
        check(firstBatch.Count == 2 &&
              firstBatch[0] == "new:a" && firstBatch[1] == "recovery:b",
            "Path Query Scheduler respeta prioridad, coalescing y orden estable");
        check(scheduler.PendingCount == 1 && scheduler.CoalescedCount == 1,
            "Path Query Scheduler limita trabajo por tick sin duplicar peticiones");

        var secondBatch = new List<string>();
        scheduler.Dequeue(1, secondBatch);
        check(secondBatch.Count == 1 && secondBatch[0] == "new:c",
            "Path Query Scheduler conserva consultas aplazadas para el siguiente tick");
    }

    private static void TestRecoveryPlanner(Action<bool, string> check)
    {
        var planner = new BistroBuilderNavigationRecoveryPlanner();
        bool escapeA = planner.TryFindEscapePocket(
            "agent:a", Vector3.zero, Vector3.forward, _ => true, out Vector3 escape1);
        bool escapeB = planner.TryFindEscapePocket(
            "agent:a", Vector3.zero, Vector3.forward, _ => true, out Vector3 escape2);
        check(escapeA && escapeB && (escape1 - escape2).sqrMagnitude < 0.000001f,
            "Escape Pocket selecciona candidato determinista");

        bool retreat = planner.TryFindRetreat(
            "agent:a", Vector3.zero, Vector3.forward, _ => true, out Vector3 retreatTarget);
        check(retreat && retreatTarget.z < -0.4f,
            "Retreat busca espacio valido hacia una zona de liberacion posterior");
    }

    private static void TestPhysicalQueues(Action<bool, string> check)
    {
        var queues = new BistroBuilderNavigationPhysicalQueueCoordinator();
        bool configured = queues.ConfigureQueue("q", new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, -0.8f)
        });
        queues.Enqueue("q", "b", 20, 0f);
        queues.Enqueue("q", "a", 10, 0.1f);
        queues.Enqueue("q", "c", 30, 0.2f);
        bool aTarget = queues.TryGetTarget("a", out _, out int aSlot, out _, out bool aOverflow);
        bool bTarget = queues.TryGetTarget("b", out _, out int bSlot, out _, out bool bOverflow);
        bool cTarget = queues.TryGetTarget("c", out _, out _, out _, out bool cOverflow);
        check(configured && aTarget && bTarget && aSlot == 0 && bSlot == 1 && !aOverflow && !bOverflow,
            "Cola fisica materializa el orden logico sin decidirlo");
        check(!cTarget && cOverflow && queues.OverflowEventCount > 0,
            "Cola fisica detecta overflow sin apilar agentes");
        queues.Remove("a");
        queues.TryGetTarget("b", out _, out bSlot, out _, out _);
        check(bSlot == 0,
            "Cola fisica avanza automaticamente al liberar la cabecera");
    }

    private static void TestReplay(Action<bool, string> check)
    {
        var recorder = new BistroBuilderNavigationReplayRecorder();
        var topology = new BistroBuilderNavigationTopologySnapshot
        {
            spatialRevision = 2,
            navigationRevision = 3,
            trafficEpoch = 4,
            flowQuality = 0.9f
        };
        var trace = new BistroBuilderNavigationDecisionTrace
        {
            ownerId = "agent:a",
            state = BistroBuilderNavigationTravelState.Yielding,
            waitingReason = BistroBuilderNavigationWaitingReason.Yield,
            recoveryStage = BistroBuilderNavigationRecoveryStage.Yield,
            blockerId = "agent:b"
        };
        recorder.Record("yield", "agent:a", new Vector3(1f, 0f, 2f), trace, topology, "test");
        recorder.Record("resume", "agent:a", new Vector3(1.1f, 0f, 2f), trace, topology, "test2");
        BistroBuilderNavigationReplayBundle bundle = recorder.CaptureBundle();
        bool valid = BistroBuilderNavigationReplayRecorder.ValidateBundle(bundle, out _);
        var recorder2 = new BistroBuilderNavigationReplayRecorder();
        recorder2.Record("yield", "agent:a", new Vector3(1f, 0f, 2f), trace, topology, "test");
        recorder2.Record("resume", "agent:a", new Vector3(1.1f, 0f, 2f), trace, topology, "test2");
        check(valid && bundle.deterministicDigest == recorder2.CaptureBundle().deterministicDigest,
            "Replay produce una firma determinista para el mismo stream de decisiones");
        bundle.events[0].detail = "tampered";
        check(!BistroBuilderNavigationReplayRecorder.ValidateBundle(bundle, out _),
            "Replay detecta alteraciones del stream registrado");
    }
    private static void TestSceneIntegration(Action<bool, string> check)
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BistroBuilderNavigationService navigation =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>();
        check(navigation != null, "Navigation v1 esta instalado en la escena real");
        if (navigation == null) return;

        navigation.RebuildNavigationTopology();
        check(navigation.RouteGraphNodeCount > 0,
            "Route Graph se construye desde zonas y Spatial Gates reales");
        check(navigation.BlockDependencyCount == 0,
            "Block Dependency Graph arranca sin dependencias fantasma");
        check(navigation.ActiveDeadlockCount == 0,
            "Runtime arranca sin deadlocks fantasma");
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