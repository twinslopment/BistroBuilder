using System;
using System.Collections.Generic;
using System.IO;
using DiagnosticsStopwatch = System.Diagnostics.Stopwatch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BistroBuilderNavigation17StressSoakSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.Navigation17.Stress.Stage";
    private const string SuccessKey = "BB.Navigation17.Stress.Success";
    private const string AgentCountKey = "BB.Navigation17.Stress.AgentCount";
    private const string ReportPathKey = "BB.Navigation17.Stress.ReportPath";
    private const string ReportPath50 = "Navigation17StressSoakReport.txt";
    private const string ReportPath100 = "Navigation17StressSoak100Report.txt";
    private const int DefaultAgentCount = 50;
    private const int ExtremeAgentCount = 100;
    private const float RunSeconds = 22f;
    private const float Radius = 0.26f;
    private const float Speed = 3.2f;
    private const float Arrival = 0.12f;

    private static readonly List<AgentRuntime> Agents = new List<AgentRuntime>(ExtremeAgentCount);
    private static readonly List<double> NavigationFrameTimesMs = new List<double>(8192);
    private static int agentCount = DefaultAgentCount;
    private static string reportPath = ReportPath50;
    private static BistroBuilderNavigationService navigation;
    private static float startedAt;
    private static float readyAt;
    private static int completedLegs;
    private static int explicitFailures;
    private static string lastFailureDiagnostic = string.Empty;
    private static int maxPending;
    private static int maxEncounters;
    private static int maxDependencies;
    private static int maxDeadlocks;
    private static int replanCount;
    private static int startFrame;
    private static bool draining;
    private static float drainStartedAt;

    static BistroBuilderNavigation17StressSoakSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Bistro Builder/17 Navegacion/Stress-Soak 50 NPC")]
    private static void RunFromMenu() => Begin(false, DefaultAgentCount, ReportPath50);

    [MenuItem("Bistro Builder/17 Navegacion/Stress-Soak 100 NPC")]
    private static void Run100FromMenu() => Begin(false, ExtremeAgentCount, ReportPath100);

    public static void RunFromCommandLine() => Begin(true, DefaultAgentCount, ReportPath50);
    public static void Run100FromCommandLine() => Begin(true, ExtremeAgentCount, ReportPath100);

    private static void Begin(bool cli, int requestedAgentCount, string requestedReportPath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stress/Soak Navigation 17 ya esta en ejecucion.");
        agentCount = Mathf.Clamp(requestedAgentCount, 1, ExtremeAgentCount);
        reportPath = string.IsNullOrWhiteSpace(requestedReportPath) ? ReportPath50 : requestedReportPath;
        File.Delete(Path.GetFullPath(reportPath));
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetInt(AgentCountKey, agentCount);
        SessionState.SetString(ReportPathKey, reportPath);
        SessionState.SetString(StageKey, cli ? "enter_cli" : "enter_menu");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage)) return;
        agentCount = Mathf.Clamp(SessionState.GetInt(AgentCountKey, DefaultAgentCount), 1, ExtremeAgentCount);
        reportPath = SessionState.GetString(ReportPathKey, ReportPath50);
        if (string.IsNullOrWhiteSpace(reportPath)) reportPath = ReportPath50;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
            SessionState.SetString(StageKey, cli ? "prepare_cli" : "prepare_menu");
            readyAt = Time.realtimeSinceStartup + 0.5f;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool cli = stage.Contains("cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            Cleanup();
            if (cli) EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static void OnUpdate()
    {
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (string.IsNullOrEmpty(stage)) return;
        agentCount = Mathf.Clamp(SessionState.GetInt(AgentCountKey, DefaultAgentCount), 1, ExtremeAgentCount);
        reportPath = SessionState.GetString(ReportPathKey, ReportPath50);
        if (string.IsNullOrWhiteSpace(reportPath)) reportPath = ReportPath50;
        if (!EditorApplication.isPlaying)
        {
            if (!stage.StartsWith("exit_", StringComparison.Ordinal)) return;
            bool cliExit = stage.EndsWith("cli", StringComparison.Ordinal);
            bool ok = SessionState.GetBool(SuccessKey, false);
            Cleanup();
            if (cliExit) EditorApplication.Exit(ok ? 0 : 1);
            return;
        }

        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        if (stage.StartsWith("enter_", StringComparison.Ordinal))
        {
            stage = cli ? "prepare_cli" : "prepare_menu";
            SessionState.SetString(StageKey, stage);
            readyAt = Time.realtimeSinceStartup + 0.5f;
        }

        try
        {
            if (stage.StartsWith("prepare_", StringComparison.Ordinal))
            {
                if (Time.realtimeSinceStartup < readyAt) return;
                Prepare(cli);
                return;
            }
            if (!stage.StartsWith("run_", StringComparison.Ordinal)) return;
            Tick(cli);
        }
        catch (Exception exception)
        {
            Finish(false, "Stress/Soak exception: " + exception.Message, cli);
        }
    }

    private static void Prepare(bool cli)
    {
        navigation = UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>();
        GameObject entrance = GameObject.Find("RestaurantEntrancePoint");
        KitchenSystem kitchen = UnityEngine.Object.FindFirstObjectByType<KitchenSystem>();
        RestaurantTable[] tables = UnityEngine.Object.FindObjectsByType<RestaurantTable>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        if (navigation == null || entrance == null || kitchen == null || kitchen.PickupPoint == null)
            throw new InvalidOperationException("Faltan Navigation, entrada o pickup de cocina.");

        navigation.RebuildNavigationTopology();
        RestaurantTable table = null;
        for (int i = 0; i < tables.Length; i++)
        {
            if (tables[i] != null && tables[i].CustomerApproachPoint != null && tables[i].WaiterServicePoint != null)
            {
                table = tables[i];
                break;
            }
        }
        if (table == null) throw new InvalidOperationException("No hay mesa de stress con ambos approach points.");

        var customerPath = new List<Vector3>(32);
        var waiterPath = new List<Vector3>(32);
        if (!navigation.TryBuildRoute("stress:template:c", BistroBuilderNavigationAgentMask.Customer,
                entrance.transform.position, table.CustomerApproachPoint.position,
                customerPath, out _, out _) || customerPath.Count == 0)
            throw new InvalidOperationException("No pudo construir ruta plantilla de clientes.");
        if (!navigation.TryBuildRoute("stress:template:w", BistroBuilderNavigationAgentMask.Waiter,
                kitchen.PickupPoint.position, table.WaiterServicePoint.position,
                waiterPath, out _, out _) || waiterPath.Count == 0)
            throw new InvalidOperationException("No pudo construir ruta plantilla de camareros.");

        Agents.Clear();
        NavigationFrameTimesMs.Clear();
        completedLegs = explicitFailures = maxPending = maxEncounters = maxDependencies = maxDeadlocks = replanCount = 0;
        lastFailureDiagnostic = string.Empty;
        draining = false;
        drainStartedAt = 0f;
        navigation.NavigationTripFinished -= OnTripFinished;
        navigation.NavigationTripFinished += OnTripFinished;
        for (int i = 0; i < agentCount; i++)
        {
            bool waiter = (i & 1) == 1;
            List<Vector3> path = waiter ? waiterPath : customerPath;
            Vector3 startAnchor = waiter ? kitchen.PickupPoint.position : entrance.transform.position;
            bool reverse = ((i / 2) & 1) == 1;
            int laneSlot = (i / 4) % 6;
            float originFraction = reverse
                ? 0.80f - laneSlot * 0.012f
                : 0.20f + laneSlot * 0.012f;
            Vector3 endpointA = SamplePolylineFraction(startAnchor, path, 0.20f);
            Vector3 endpointB = SamplePolylineFraction(startAnchor, path, 0.80f);
            Vector3 origin = SamplePolylineFraction(startAnchor, path, originFraction);
            Vector3 destination = reverse ? endpointA : endpointB;

            var agent = new AgentRuntime
            {
                id = "stress:" + i.ToString("D3"),
                mask = waiter ? BistroBuilderNavigationAgentMask.Waiter : BistroBuilderNavigationAgentMask.Customer,
                position = origin,
                endpointA = endpointA,
                endpointB = endpointB,
                goingToB = !reverse,
                urgency = i % 7 == 0 ? 20 : 0
            };
            Agents.Add(agent);
            StartLeg(agent, destination);
        }
        startedAt = Time.realtimeSinceStartup;
        startFrame = Time.frameCount;
        SessionState.SetString(StageKey, cli ? "run_cli" : "run_menu");
    }

    private static void Tick(bool cli)
    {
        float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.005f, 0.04f);
        long navStarted = DiagnosticsStopwatch.GetTimestamp();
        for (int i = 0; i < Agents.Count; i++) TickAgent(Agents[i], dt);
        double navFrameMs = (DiagnosticsStopwatch.GetTimestamp() - navStarted) * 1000.0 / DiagnosticsStopwatch.Frequency;
        if (!draining && Time.realtimeSinceStartup - startedAt >= 2f)
            NavigationFrameTimesMs.Add(navFrameMs);

        maxPending = Math.Max(maxPending, navigation.PendingPathQueryCount);
        maxEncounters = Math.Max(maxEncounters, navigation.ActiveEncounterCount);
        maxDependencies = Math.Max(maxDependencies, navigation.BlockDependencyCount);
        maxDeadlocks = Math.Max(maxDeadlocks, navigation.ActiveDeadlockCount);

        if (explicitFailures > 0)
        {
            Finish(false, "Hubo " + explicitFailures + " fallos explicitos durante el soak. " + lastFailureDiagnostic + " " + BuildDiagnostics(), cli);
            return;
        }

        if (!draining && Time.realtimeSinceStartup - startedAt >= RunSeconds)
        {
            draining = true;
            drainStartedAt = Time.realtimeSinceStartup;
        }
        if (!draining) return;

        if (navigation.ActiveTripCount > 0 || navigation.PendingPathQueryCount > 0 || navigation.ActiveDeadlockCount > 0)
        {
            if (Time.realtimeSinceStartup - drainStartedAt < 12f) return;
            Finish(false, "El drenaje final no convergio. " + BuildDiagnostics(), cli);
            return;
        }

        if (completedLegs < agentCount)
        {
            Finish(false, "Throughput insuficiente: " + completedLegs + " legs completados para " + agentCount + " agentes. " + BuildDiagnostics(), cli);
            return;
        }

        BistroBuilderNavigationMetricsSnapshot metrics = navigation.CaptureNavigationMetrics();
        double navP95 = Percentile95(NavigationFrameTimesMs);
        double navMean = Mean(NavigationFrameTimesMs);
        string result =
            "PASS - " + agentCount + " agentes logicos en PlayMode durante " + RunSeconds.ToString("0") + " s. " +
            "Legs=" + completedLegs + ", replans=" + replanCount +
            ", maxPending=" + maxPending + ", maxEncounters=" + maxEncounters +
            ", maxDependencies=" + maxDependencies + ", maxDeadlocks=" + maxDeadlocks +
            ", yields=" + metrics.yieldCount + ", recoveries=" + metrics.recoveryCount +
            ", hardFailures=" + metrics.tripsFailed +
            ", navP95Ms=" + navP95.ToString("0.000") +
            ", navMeanMs=" + navMean.ToString("0.000") + ".";
        Finish(true, result, cli);
    }

    private static void TickAgent(AgentRuntime agent, float dt)
    {
        if (agent.retired) return;
        navigation.UpdateAgentPresence(agent.id, agent.mask, agent.position, Radius, agent.urgency);

        if (agent.route == null)
        {
            if (navigation.TryGetNavigationPlan(agent.id, out BistroBuilderNavigationPlan plan) && plan?.route?.points != null)
            {
                agent.route = new List<Vector3>(plan.route.points);
                agent.routeIndex = 0;
            }
            else
            {
                if (navigation.TryGetNavigationTrace(agent.id, out BistroBuilderNavigationDecisionTrace pending) &&
                    pending.state == BistroBuilderNavigationTravelState.Failed)
                    explicitFailures++;
                return;
            }
        }

        if (agent.routeIndex >= agent.route.Count)
        {
            CompleteAndReverse(agent);
            return;
        }

        Vector3 target = agent.route[agent.routeIndex];
        bool moved = navigation.TryResolveMovementStep(
            agent.id, agent.mask, agent.position, target, Speed, Radius,
            agent.urgency, dt, out Vector3 proposed, out BistroBuilderNavigationLocalMoveDecision decision);
        if (moved) agent.position = proposed;
        navigation.ReportNavigationPosition(
            agent.id, agent.position,
            moved ? BistroBuilderNavigationWaitingReason.None :
                (decision.waitingReason == BistroBuilderNavigationWaitingReason.None
                    ? BistroBuilderNavigationWaitingReason.Yield : decision.waitingReason),
            decision.yieldingTo);

        if (navigation.TryGetNavigationTrace(agent.id, out BistroBuilderNavigationDecisionTrace trace))
        {
            if (trace.state == BistroBuilderNavigationTravelState.Failed)
            {
                explicitFailures++;
                return;
            }
            if (trace.recoveryStage == BistroBuilderNavigationRecoveryStage.FullReplan ||
                trace.recoveryStage == BistroBuilderNavigationRecoveryStage.CorridorRepair)
            {
                BistroBuilderNavigationReplanLevel level =
                    trace.recoveryStage == BistroBuilderNavigationRecoveryStage.FullReplan
                        ? BistroBuilderNavigationReplanLevel.FullReplan
                        : BistroBuilderNavigationReplanLevel.CorridorRepair;
                Vector3 destination = agent.goingToB ? agent.endpointB : agent.endpointA;
                if (navigation.TryReplanNavigation(agent.id, agent.position, destination, level, out BistroBuilderNavigationRoute rebuilt) &&
                    rebuilt?.points != null)
                {
                    agent.route = new List<Vector3>(rebuilt.points);
                    agent.routeIndex = 0;
                    replanCount++;
                }
            }
        }

        if ((agent.position - target).sqrMagnitude <= Arrival * Arrival)
            agent.routeIndex++;
        if (agent.routeIndex >= agent.route.Count)
            CompleteAndReverse(agent);
    }

    private static void CompleteAndReverse(AgentRuntime agent)
    {
        navigation.CompleteNavigation(agent.id);
        completedLegs++;
        if (draining)
        {
            agent.retired = true;
            navigation.RemoveAgentPresence(agent.id);
            return;
        }
        agent.goingToB = !agent.goingToB;
        Vector3 destination = agent.goingToB ? agent.endpointB : agent.endpointA;
        StartLeg(agent, destination);
    }

    private static void StartLeg(AgentRuntime agent, Vector3 destination)
    {
        agent.route = null;
        agent.routeIndex = 0;
        var request = new BistroBuilderNavigationRequest
        {
            ownerId = agent.id,
            agentMask = agent.mask,
            origin = agent.position,
            destination = destination,
            nominalSpeed = Speed,
            mobilityRadius = Radius,
            externalUrgency = agent.urgency
        };
        if (!navigation.TryStartNavigation(request, out BistroBuilderNavigationPlan plan, out BistroBuilderNavigationResult failure))
        {
            explicitFailures++;
            return;
        }
        if (plan?.route?.points != null)
            agent.route = new List<Vector3>(plan.route.points);
    }

    private static void OnTripFinished(BistroBuilderNavigationResult result)
    {
        if (result != null && result.finalState == BistroBuilderNavigationTravelState.Failed)
        {
            explicitFailures++;
            lastFailureDiagnostic = "owner=" + result.ownerId + ", reason=" + result.failureReason + ".";
        }
    }

    private static Vector3 SamplePolylineFraction(Vector3 origin, List<Vector3> points, float fraction)
    {
        float length = 0f;
        Vector3 previous = origin;
        for (int i = 0; i < points.Count; i++)
        {
            length += Vector3.Distance(previous, points[i]);
            previous = points[i];
        }
        return SamplePolyline(origin, points, length * Mathf.Clamp01(fraction), false);
    }
    private static Vector3 SamplePolyline(Vector3 origin, List<Vector3> points, float distance, bool reverse)
    {
        if (reverse) return origin;
        Vector3 a = origin;
        float remaining = Mathf.Max(0f, distance);
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 b = points[i];
            float length = Vector3.Distance(a, b);
            if (remaining <= length && length > 0.0001f)
                return Vector3.Lerp(a, b, remaining / length);
            remaining -= length;
            a = b;
        }
        return a;
    }

    private static List<Vector3> ReverseWithOrigin(Vector3 origin, List<Vector3> points)
    {
        var result = new List<Vector3>(points.Count + 1);
        for (int i = points.Count - 2; i >= 0; i--) result.Add(points[i]);
        result.Add(origin);
        return result;
    }

    private static double Percentile95(List<double> samples)
    {
        if (samples == null || samples.Count == 0) return 0.0;
        var sorted = new List<double>(samples);
        sorted.Sort();
        int index = Mathf.Clamp(Mathf.CeilToInt(sorted.Count * 0.95f) - 1, 0, sorted.Count - 1);
        return sorted[index];
    }

    private static double Mean(List<double> samples)
    {
        if (samples == null || samples.Count == 0) return 0.0;
        double total = 0.0;
        for (int i = 0; i < samples.Count; i++) total += samples[i];
        return total / samples.Count;
    }

    private static string BuildDiagnostics()
    {
        int requesting = 0, following = 0, waiting = 0, recovering = 0, failed = 0;
        string sample = string.Empty;
        for (int i = 0; i < Agents.Count; i++)
        {
            if (!navigation.TryGetNavigationTrace(Agents[i].id, out BistroBuilderNavigationDecisionTrace trace) || trace == null)
                continue;
            switch (trace.state)
            {
                case BistroBuilderNavigationTravelState.RequestingRoute: requesting++; break;
                case BistroBuilderNavigationTravelState.FollowingRoute: following++; break;
                case BistroBuilderNavigationTravelState.Recovering:
                case BistroBuilderNavigationTravelState.Replanning: recovering++; break;
                case BistroBuilderNavigationTravelState.Failed: failed++; break;
                default: waiting++; break;
            }
            if (string.IsNullOrEmpty(sample))
                sample = Agents[i].id + " " + trace.state + "/" + trace.waitingReason + "/" + trace.recoveryStage + "/progress=" + trace.routeProgressMeters.ToString("0.00") + "/pos=" + Agents[i].position.ToString("F2") + "/blocker=" + trace.blockerId + "/decision=" + trace.lastDecision;
        }
        return "Frames=" + (Time.frameCount - startFrame) +
            ", active=" + navigation.ActiveTripCount +
            ", pending=" + navigation.PendingPathQueryCount +
            ", requesting=" + requesting + ", following=" + following +
            ", waiting=" + waiting + ", recovering=" + recovering + ", failed=" + failed +
            ", dependencies=" + navigation.BlockDependencyCount +
            ", deadlocks=" + navigation.ActiveDeadlockCount +
            ", sample=" + sample + ".";
    }
    private static void Finish(bool success, string message, bool cli)
    {
        string report = "=== BISTRO BUILDER - BLOQUE 17 / STRESS-SOAK ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message + "\n";
        File.WriteAllText(Path.GetFullPath(reportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    private static void Cleanup()
    {
        if (navigation != null)
        {
            navigation.NavigationTripFinished -= OnTripFinished;
            for (int i = 0; i < Agents.Count; i++)
            {
                navigation.CancelNavigation(Agents[i].id);
                navigation.RemoveAgentPresence(Agents[i].id);
            }
        }
        Agents.Clear();
        NavigationFrameTimesMs.Clear();
        navigation = null;
        SessionState.EraseString(StageKey);
        SessionState.EraseInt(AgentCountKey);
        SessionState.EraseString(ReportPathKey);
    }

    private sealed class AgentRuntime
    {
        public string id;
        public BistroBuilderNavigationAgentMask mask;
        public Vector3 position;
        public Vector3 endpointA;
        public Vector3 endpointB;
        public bool goingToB;
        public int urgency;
        public List<Vector3> route;
        public int routeIndex;
        public bool retired;
    }
}