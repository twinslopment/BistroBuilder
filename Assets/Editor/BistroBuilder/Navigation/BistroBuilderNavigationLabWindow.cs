using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Laboratorio Tool-First de Navigation & Crowd Flow v1.
/// Observa estado y lanza validaciones sin convertirse en autoridad de gameplay o BBSIS.
/// </summary>
public sealed class BistroBuilderNavigationLabWindow : EditorWindow
{
    private readonly List<BistroBuilderNavigationDecisionTrace> traces =
        new List<BistroBuilderNavigationDecisionTrace>(128);
    private readonly List<BistroBuilderNavigationTrafficCellSnapshot> heat =
        new List<BistroBuilderNavigationTrafficCellSnapshot>(128);    private readonly List<BistroBuilderNavigationPhysicalQueueSnapshot> queues =
        new List<BistroBuilderNavigationPhysicalQueueSnapshot>(16);
    private readonly List<BistroBuilderNavigationReplayEvent> replayEvents =
        new List<BistroBuilderNavigationReplayEvent>(512);
    private readonly BistroBuilderNavigationReplayPlayer replayPlayer =
        new BistroBuilderNavigationReplayPlayer();

    private BistroBuilderNavigationService navigation;
    private BistroBuilderNavigationMetricsSnapshot metrics;
    private Vector2 scroll;
    private string selectedOwnerId = string.Empty;
    private bool showTrips = true;
    private bool showTrafficHeat;
    private bool showQueues = true;
    private bool showReplay = true;
    private bool autoRefresh = true;
    private string replayStatus = string.Empty;

    [MenuItem("Bistro Builder/17 Navegacion/Navigation Lab")]
    public static void Open()
    {
        GetWindow<BistroBuilderNavigationLabWindow>("BB Navigation Lab");
    }

    private void OnEnable()
    {
        minSize = new Vector2(560f, 420f);
        RefreshSnapshot();
    }

    private void OnInspectorUpdate()
    {
        if (!autoRefresh) return;
        RefreshSnapshot();
        Repaint();
    }

    private void RefreshSnapshot()
    {
        navigation = UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>();
        traces.Clear();
        heat.Clear();
        queues.Clear();
        replayEvents.Clear();
        metrics = null;
        if (navigation == null) return;
        metrics = navigation.CaptureNavigationMetrics();
        navigation.WriteActiveNavigationTraces(traces);
        navigation.WriteTrafficHeatSnapshots(heat);
        navigation.WritePhysicalQueueSnapshots(queues);
        navigation.WriteNavigationReplayEvents(replayEvents);
        heat.Sort((a, b) => b.penalty.CompareTo(a.penalty));
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("BB Navigation & Crowd Flow v1", EditorStyles.boldLabel);
        autoRefresh = EditorGUILayout.ToggleLeft("Auto refresh", autoRefresh);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refrescar")) RefreshSnapshot();
            using (new EditorGUI.DisabledScope(navigation == null))
                if (GUILayout.Button("Reconstruir topologia")) navigation.RebuildNavigationTopology();
            if (GUILayout.Button("Validator")) BistroBuilderNavigation17Validator.Run();
            if (GUILayout.Button("Core tests")) BistroBuilderNavigationV1CoreSelfTest.Run();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Stress 50"))
                EditorApplication.ExecuteMenuItem("Bistro Builder/17 Navegacion/Stress-Soak 50 NPC");
            if (GUILayout.Button("Stress 100"))
                EditorApplication.ExecuteMenuItem("Bistro Builder/17 Navegacion/Stress-Soak 100 NPC");
            if (GUILayout.Button("Closure gate"))
                EditorApplication.ExecuteMenuItem("Bistro Builder/17 Navegacion/Closure gate");
        }

        if (navigation == null)
        {
            EditorGUILayout.HelpBox("No hay BistroBuilderNavigationService en la escena cargada.", MessageType.Warning);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawRuntimeSummary();
        DrawMetrics();
        DrawTrips();
        DrawPhysicalQueues();
        DrawReplay();
        DrawTrafficHeat();
        EditorGUILayout.EndScrollView();
    }

    private void DrawRuntimeSummary()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
        BistroBuilderNavigationTopologySnapshot topology = navigation.CaptureTopologySnapshot();
        EditorGUILayout.LabelField("Revisiones", $"Spatial {topology.spatialRevision} / Nav {topology.navigationRevision} / Traffic {topology.trafficEpoch}");
        EditorGUILayout.LabelField("Route Graph", $"{navigation.RouteGraphNodeCount} nodos / {navigation.RouteGraphEdgeCount} aristas");
        EditorGUILayout.LabelField("Viajes", $"{navigation.ActiveTripCount} activos / {navigation.PendingPathQueryCount} path queries pendientes");
        EditorGUILayout.LabelField("Crowd", $"{navigation.ActiveEncounterCount} encounters / {navigation.ActiveDeadlockCount} deadlocks");
        EditorGUILayout.LabelField("Dependencias", navigation.BlockDependencyCount.ToString());
        EditorGUILayout.LabelField("Controlled Passages", navigation.ControlledPassageCount.ToString());
        EditorGUILayout.LabelField("Physical Queues", $"{navigation.PhysicalQueueCount} colas / {navigation.QueuedPhysicalAgentCount} agentes");
        EditorGUILayout.LabelField("Replay", navigation.ReplayEventCount + " eventos");
        EditorGUILayout.LabelField("Traffic Heat", navigation.TrafficHeatCellCount + " celdas");
        EditorGUILayout.LabelField("Route Cache", $"{navigation.RouteCachePositiveCount} positivas / {navigation.RouteCacheNegativeCount} negativas");
        EditorGUILayout.LabelField("Flow Quality BBSIS", topology.flowQuality.ToString("0.000"));
    }

    private void DrawMetrics()
    {
        if (metrics == null) return;
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Metricas acumuladas", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Trips", $"{metrics.tripsCompleted}/{metrics.tripsStarted} completados; {metrics.tripsFailed} fallidos");
        EditorGUILayout.LabelField("Completion ratio", metrics.TripCompletionRatio.ToString("0.000"));
        EditorGUILayout.LabelField("Delay ratio", metrics.NavigationDelayRatio.ToString("0.000"));
        EditorGUILayout.LabelField("Yield / Recovery", $"{metrics.yieldCount} / {metrics.recoveryCount}");
        EditorGUILayout.LabelField("Deadlocks / Backoff", $"{metrics.deadlockCount} / {metrics.localBackoffCount}");
        EditorGUILayout.LabelField("Escape / Retreat", $"{metrics.escapePocketCount} / {metrics.retreatCount}");
        EditorGUILayout.LabelField("Queue overflow / Replay", $"{metrics.physicalQueueOverflowCount} / {metrics.replayEventCount}");
        EditorGUILayout.LabelField("Replans", $"R1 {metrics.corridorRepairCount} / R2 {metrics.routeSuffixRepairCount} / R3 {metrics.fullReplanCount}");
        EditorGUILayout.LabelField("Path Scheduler", $"{metrics.pathQueryCompletedCount} completadas / peak {metrics.pathQueryPendingPeak}");
    }

    private void DrawTrips()
    {
        EditorGUILayout.Space(8f);
        showTrips = EditorGUILayout.Foldout(showTrips, "Viajes activos (Why waiting / recovery)", true);
        if (!showTrips) return;
        if (traces.Count == 0)
        {
            EditorGUILayout.LabelField("Sin viajes activos.");
            return;
        }

        for (int i = 0; i < traces.Count; i++)
        {
            BistroBuilderNavigationDecisionTrace trace = traces[i];
            if (trace == null) continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                bool selected = string.Equals(selectedOwnerId, trace.ownerId, StringComparison.Ordinal);
                if (GUILayout.Toggle(selected, trace.ownerId, "Button", GUILayout.Width(180f)))
                    selectedOwnerId = trace.ownerId;
                EditorGUILayout.LabelField(trace.state.ToString(), GUILayout.Width(100f));
                EditorGUILayout.LabelField(trace.waitingReason.ToString(), GUILayout.Width(130f));
                EditorGUILayout.LabelField($"{trace.routeProgressMeters:0.0}/{trace.routeLengthMeters:0.0} m");
            }
        }

        BistroBuilderNavigationDecisionTrace selectedTrace =
            traces.Find(t => t != null && string.Equals(t.ownerId, selectedOwnerId, StringComparison.Ordinal));
        if (selectedTrace != null)
            DrawSelectedTrace(selectedTrace);
    }

    private static void DrawSelectedTrace(BistroBuilderNavigationDecisionTrace trace)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Trace seleccionada", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Owner / Request", trace.ownerId + " / " + trace.requestId);
        EditorGUILayout.LabelField("Estado", trace.state.ToString());
        EditorGUILayout.LabelField("Why waiting", trace.waitingReason.ToString());
        EditorGUILayout.LabelField("Blocker", string.IsNullOrEmpty(trace.blockerId) ? "-" : trace.blockerId);
        EditorGUILayout.LabelField("Yielding to", string.IsNullOrEmpty(trace.yieldingTo) ? "-" : trace.yieldingTo);
        EditorGUILayout.LabelField("Recovery", trace.recoveryStage.ToString());
        EditorGUILayout.LabelField("Sin progreso", trace.secondsWithoutProgress.ToString("0.00") + " s");
        EditorGUILayout.LabelField("Replans / Recoveries", trace.replanCount + " / " + trace.recoveryCount);
        EditorGUILayout.LabelField("Ultima decision", string.IsNullOrEmpty(trace.lastDecision) ? "-" : trace.lastDecision);
        EditorGUILayout.EndVertical();
    }

    private void DrawPhysicalQueues()
    {
        EditorGUILayout.Space(8f);
        showQueues = EditorGUILayout.Foldout(showQueues, "Colas fisicas", true);
        if (!showQueues) return;
        if (queues.Count == 0)
        {
            EditorGUILayout.LabelField("Sin colas fisicas configuradas/activas.");
            return;
        }

        for (int q = 0; q < queues.Count; q++)
        {
            BistroBuilderNavigationPhysicalQueueSnapshot queue = queues[q];
            if (queue == null) continue;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                queue.queueId,
                $"{queue.queuedCount}/{queue.capacity}; overflow {queue.overflowCount}");
            for (int i = 0; i < queue.entries.Count; i++)
            {
                BistroBuilderNavigationPhysicalQueueEntrySnapshot entry = queue.entries[i];
                if (entry == null) continue;
                EditorGUILayout.LabelField(
                    $"  {entry.ownerId}: " +
                    (entry.overflow ? "OVERFLOW" : "slot " + entry.slotIndex));
            }
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawReplay()
    {
        EditorGUILayout.Space(8f);
        showReplay = EditorGUILayout.Foldout(showReplay, "Deterministic Replay / Incidentes", true);
        if (!showReplay) return;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capturar runtime")) CaptureReplayFromRuntime();
            if (GUILayout.Button("Guardar JSON")) SaveReplay();
            if (GUILayout.Button("Cargar JSON")) LoadReplay();
            if (GUILayout.Button("Limpiar runtime"))
            {
                navigation.ClearNavigationReplay("Navigation Incident");
                replayPlayer.Reset();
                replayStatus = "Replay runtime limpiado.";
                RefreshSnapshot();
            }
        }

        if (!string.IsNullOrEmpty(replayStatus))
            EditorGUILayout.HelpBox(replayStatus, MessageType.Info);

        EditorGUILayout.LabelField(
            "Runtime events",
            replayEvents.Count.ToString());
        EditorGUILayout.LabelField(
            "Replay cargado",
            replayPlayer.IsLoaded ? $"{replayPlayer.Count} eventos / {replayPlayer.Digest}" : "-");

        if (replayPlayer.IsLoaded && replayPlayer.Count > 0)
        {
            int requested = EditorGUILayout.IntSlider(
                "Timeline", replayPlayer.Index, 0, replayPlayer.Count - 1);
            replayPlayer.Seek(requested);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("<")) replayPlayer.StepBackward();
                if (GUILayout.Button("Reset")) replayPlayer.Reset();
                if (GUILayout.Button(">")) replayPlayer.StepForward();
            }

            BistroBuilderNavigationReplayEvent e = replayPlayer.Current;
            if (e != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Evento", $"#{e.sequence} {e.eventType}");
                EditorGUILayout.LabelField("Owner", string.IsNullOrEmpty(e.ownerId) ? "-" : e.ownerId);
                EditorGUILayout.LabelField("Estado", e.state.ToString());
                EditorGUILayout.LabelField("Wait / Recovery", $"{e.waitingReason} / {e.recoveryStage}");
                EditorGUILayout.LabelField("Blocker", string.IsNullOrEmpty(e.blockerId) ? "-" : e.blockerId);
                EditorGUILayout.LabelField("Detalle", string.IsNullOrEmpty(e.detail) ? "-" : e.detail);
                EditorGUILayout.LabelField("Posicion", e.position.ToString("F2"));
                EditorGUILayout.EndVertical();
            }
        }
    }

    private void CaptureReplayFromRuntime()
    {
        if (navigation == null) return;
        BistroBuilderNavigationReplayBundle bundle = navigation.CaptureNavigationReplay();
        if (!replayPlayer.Load(bundle, out string error))
        {
            replayStatus = "Replay invalido: " + error;
            return;
        }
        replayStatus = $"Replay capturado: {bundle.events.Count} eventos / {bundle.deterministicDigest}";
    }

    private void SaveReplay()
    {
        if (navigation == null) return;
        BistroBuilderNavigationReplayBundle bundle = navigation.CaptureNavigationReplay();
        string path = EditorUtility.SaveFilePanel(
            "Guardar Navigation Replay", Directory.GetParent(Application.dataPath).FullName,
            "NavigationReplay", "json");
        if (string.IsNullOrWhiteSpace(path)) return;
        File.WriteAllText(path, JsonUtility.ToJson(bundle, true));
        replayStatus = "Replay guardado: " + path;
        replayPlayer.Load(bundle, out _);
    }

    private void LoadReplay()
    {
        string path = EditorUtility.OpenFilePanel(
            "Cargar Navigation Replay", Directory.GetParent(Application.dataPath).FullName, "json");
        if (string.IsNullOrWhiteSpace(path)) return;
        BistroBuilderNavigationReplayBundle bundle =
            JsonUtility.FromJson<BistroBuilderNavigationReplayBundle>(File.ReadAllText(path));
        if (!replayPlayer.Load(bundle, out string error))
        {
            replayStatus = "Replay rechazado: " + error;
            return;
        }
        replayStatus = $"Replay valido: {bundle.events.Count} eventos / {bundle.deterministicDigest}";
    }

    private void DrawTrafficHeat()
    {
        EditorGUILayout.Space(8f);
        showTrafficHeat = EditorGUILayout.Foldout(showTrafficHeat, "Traffic Heat", true);
        if (!showTrafficHeat) return;
        int count = Mathf.Min(20, heat.Count);
        if (count == 0)
        {
            EditorGUILayout.LabelField("Sin celdas de trafico activas.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            BistroBuilderNavigationTrafficCellSnapshot cell = heat[i];
            EditorGUILayout.LabelField(
                $"[{cell.x},{cell.z}] occ={cell.smoothedOccupancy:0.00} speed={cell.smoothedMeanSpeed:0.00} penalty={cell.penalty:0.00}");
        }
    }
}
