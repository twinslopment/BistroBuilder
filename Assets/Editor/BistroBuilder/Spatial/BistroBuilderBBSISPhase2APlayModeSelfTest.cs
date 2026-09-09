using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class BistroBuilderBBSISPhase2APlayModeSelfTest
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string StageKey = "BB.BBSIS.Phase2A.Play.Stage";
    private const string SuccessKey = "BB.BBSIS.Phase2A.Play.Success";
    private const string ReportPath = "BBSISPhase2APlayModeReport.txt";

    static BistroBuilderBBSISPhase2APlayModeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("Bistro Builder/BBSIS/Fase 2A/PlayMode real")]
    private static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);
    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("El PlayMode BBSIS Fase 2A ya está ejecutándose.");
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
        if (!EditorApplication.isPlaying || Time.frameCount < 8) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        if (!stage.StartsWith("run_", StringComparison.Ordinal)) return;
        bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
        try
        {
            RunRuntimeProbe();
            Finish(true,
                "PASS - mesas y sillas reales publican su semántica BBSIS; " +
                "los sweeps de silla/puerta se reservan y bloquean antes de uso; " +
                "Spatial Quality/Bottleneck Ledger son estables en runtime.", cli);
        }
        catch (Exception exception)
        {
            Finish(false, "BBSIS Fase 2A PlayMode: " + exception.Message, cli);
        }
    }

    private static void RunRuntimeProbe()
    {
        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        BistroBuilderSpatialAssessmentService assessment =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialAssessmentService>();
        BistroBuilderSpatialRuntimeBinder binder =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialRuntimeBinder>();
        BistroBuilderNavigationService navigation =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>();
        if (spatial == null || assessment == null || binder == null || navigation == null)
            throw new InvalidOperationException("Faltan servicios requeridos por BBSIS 2A.");
        if (!spatial.ValidateConfiguration(out string spatialError))
            throw new InvalidOperationException(spatialError);
        if (!assessment.ValidateConfiguration(out string assessmentError))
            throw new InvalidOperationException(assessmentError);
        if (!binder.ValidateConfiguration(out string binderError))
            throw new InvalidOperationException(binderError);

        spatial.ResetTransientRuntimeStateAfterLoad();
        navigation.RebuildNavigationTopology();

        RestaurantSeat[] seats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        RestaurantTableSeatingConfiguration[] tables =
            UnityEngine.Object.FindObjectsByType<RestaurantTableSeatingConfiguration>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        if (seats.Length == 0 || tables.Length == 0)
            throw new InvalidOperationException("El fixture real no contiene sillas/mesas activas.");

        ValidateRealSeat(seats[0]);
        ValidateRealTable(tables[0]);
        ValidateAssessment(assessment);
        ValidateSyntheticDoorGate(spatial, navigation);

        spatial.ResetTransientRuntimeStateAfterLoad();
        if (spatial.ActiveLeaseCount != 0 || spatial.ActiveEpisodeCount != 0)
            throw new InvalidOperationException("BBSIS 2A dejó estado espacial transitorio.");
    }
    private static void ValidateRealSeat(RestaurantSeat seat)
    {
        BistroBuilderSpatialSubject subject = seat.GetComponent<BistroBuilderSpatialSubject>();
        BistroBuilderSeatSpatialAdapter adapter = seat.GetComponent<BistroBuilderSeatSpatialAdapter>();
        if (subject == null || adapter == null || subject.Proxy == null)
            throw new InvalidOperationException("Una silla real no está vinculada a BBSIS 2A.");
        if (subject.Proxy.Mode != BistroBuilderAdaptiveSpatialProxyMode.Articulated)
            throw new InvalidOperationException("La silla real no usa proxy articulado.");

        if (!subject.TryGetPortWorld("seat", out Vector3 seatPort, out _, out _, out _) ||
            seat.SeatPoint == null || Vector3.Distance(seatPort, seat.SeatPoint.position) > 0.01f)
            throw new InvalidOperationException("Seat Port no sigue el anclaje real de la silla.");
        if (!subject.TryGetPortWorld("approach", out Vector3 approachPort, out _, out _, out _) ||
            seat.CustomerApproachPoint == null ||
            Vector3.Distance(approachPort, seat.CustomerApproachPoint.position) > 0.01f)
            throw new InvalidOperationException("Approach Port no sigue el anclaje real de la silla.");

        var semantics = new List<BistroBuilderSpatialSemanticVolume>();
        adapter.WriteSemanticVolumes(semantics);
        if (!ContainsRole(semantics, BistroBuilderSpatialSemanticRole.Approach) ||
            !ContainsRole(semantics, BistroBuilderSpatialSemanticRole.DynamicSweep))
            throw new InvalidOperationException("La silla no publica Approach + Dynamic Sweep.");
    }
    private static void ValidateRealTable(RestaurantTableSeatingConfiguration table)
    {
        BistroBuilderSpatialSubject subject = table.GetComponent<BistroBuilderSpatialSubject>();
        BistroBuilderTableSpatialAdapter adapter = table.GetComponent<BistroBuilderTableSpatialAdapter>();
        if (subject == null || adapter == null || subject.Proxy == null || subject.Contract == null)
            throw new InvalidOperationException("Una mesa real no está vinculada a BBSIS 2A.");
        if (subject.Proxy.Mode != BistroBuilderAdaptiveSpatialProxyMode.Layered)
            throw new InvalidOperationException("La mesa real no usa proxy Layered.");

        var semantics = new List<BistroBuilderSpatialSemanticVolume>();
        int count = adapter.WriteSemanticVolumes(semantics);
        if (count != table.MaximumCustomers || subject.Contract.Ports.Count != table.MaximumCustomers)
            throw new InvalidOperationException("Seat Bays de mesa no coinciden con su capacidad real.");
        for (int i = 0; i < semantics.Count; i++)
            if (semantics[i].role != BistroBuilderSpatialSemanticRole.SeatBay)
                throw new InvalidOperationException("La mesa publicó semántica ajena a Seat Bay.");
    }

    private static void ValidateAssessment(BistroBuilderSpatialAssessmentService assessment)
    {
        BistroBuilderSpatialQualityResult first = assessment.EvaluateCurrentLayout();
        int firstCount = first != null ? first.diagnostics.Count : -1;
        float firstQuality = first != null ? first.quality : -1f;
        BistroBuilderSpatialQualityResult second = assessment.EvaluateCurrentLayout();
        if (first == null || second == null || assessment.LastLedger == null ||
            firstCount != second.diagnostics.Count || !Mathf.Approximately(firstQuality, second.quality))
            throw new InvalidOperationException("Spatial Quality/Bottleneck Ledger no es estable en runtime.");
    }
    private static void ValidateSyntheticDoorGate(
        BistroBuilderSpatialInteractionService spatial,
        BistroBuilderNavigationService navigation)
    {
        GameObject doorObject = null;
        GameObject blockerObject = null;
        BistroBuilderSpatialContractDefinition doorContract = null;
        BistroBuilderSpatialContractDefinition blockerContract = null;
        try
        {
            doorObject = new GameObject("__BBSIS_2A_Door__");
            doorObject.transform.position = new Vector3(900f, 0f, 900f);
            BistroBuilderDoorCirculationEnvelope envelope =
                doorObject.AddComponent<BistroBuilderDoorCirculationEnvelope>();
            envelope.ConfigureForEditor(Vector3.zero, new Vector2(1.1f, 1.1f),
                BistroBuilderNavigationAgentMask.All, BistroBuilderDynamicSpaceKind.DoorSwing);
            doorObject.AddComponent<NavMeshObstacle>();
            BistroBuilderNavigableDoor door = doorObject.AddComponent<BistroBuilderNavigableDoor>();

            doorContract = CreateDoorContract();
            if (!BistroBuilderSpatialBindingUtility.BindDoor(
                    door, doorContract, "phase2a.synthetic.door"))
                throw new InvalidOperationException("No pudo vincularse la puerta sintética.");
            BistroBuilderSpatialSubject doorSubject =
                doorObject.GetComponent<BistroBuilderSpatialSubject>();
            spatial.RegisterSubject(doorSubject);
            BistroBuilderDoorSpatialAdapter adapter =
                doorObject.GetComponent<BistroBuilderDoorSpatialAdapter>();
            if (adapter == null || !adapter.TryReserveMotion(
                    "phase2a.door.free", 2f, out BistroBuilderSpatialLeaseDecision freeDecision) ||
                !freeDecision.granted || !adapter.HasMotionLease)
                throw new InvalidOperationException("El sweep libre de puerta no obtiene Spatial Lease.");

            var semantics = new List<BistroBuilderSpatialSemanticVolume>();
            adapter.WriteSemanticVolumes(semantics);
            if (semantics.Count != 1 ||
                semantics[0].role != BistroBuilderSpatialSemanticRole.DynamicSweep)
                throw new InvalidOperationException("La puerta no publica su Dynamic Sweep.");

            Vector3 sweepCenter = semantics[0].volume.center;
            if (navigation.CanAdvance(
                    "phase2a.outsider",
                    BistroBuilderNavigationAgentMask.Waiter,
                    sweepCenter, 0.15f, 40))
                throw new InvalidOperationException("Navegación 17 ignoró el sweep BBSIS reservado.");
            adapter.ReleaseMotionReservation();
            if (adapter.HasMotionLease)
                throw new InvalidOperationException("La puerta no liberó su Spatial Lease.");

            blockerObject = new GameObject("__BBSIS_2A_StaticBlocker__");
            blockerObject.transform.position = sweepCenter;
            BistroBuilderAdaptiveSpatialProxy blockerProxy =
                blockerObject.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
            blockerProxy.Configure(BistroBuilderAdaptiveSpatialProxyMode.Simple);
            blockerProxy.AddPart(new BistroBuilderSpatialProxyPart
            {
                partId = "blocker.body",
                layer = BistroBuilderSpatialProxyLayer.Static,
                shapeKind = BistroBuilderSpatialShapeKind.OrientedBox,
                size = new Vector2(3f, 3f)
            });
            blockerContract = ScriptableObject.CreateInstance<BistroBuilderSpatialContractDefinition>();
            blockerContract.ConfigureForEditor(
                "phase2a.synthetic.blocker.contract",
                "generic",
                BistroBuilderAdaptiveSpatialProxyMode.Simple,
                new[] { "test.static" });
            BistroBuilderSpatialSubject blockerSubject =
                blockerObject.AddComponent<BistroBuilderSpatialSubject>();
            blockerSubject.Configure(
                "phase2a.synthetic.blocker",
                blockerContract,
                blockerProxy);
            spatial.RegisterSubject(blockerSubject);

            bool blocked = adapter.TryReserveMotion(
                "phase2a.door.blocked",
                2f,
                out BistroBuilderSpatialLeaseDecision blockedDecision);
            if (blocked ||
                blockedDecision.failure != BistroBuilderSpatialLeaseFailure.StaticGeometryConflict ||
                blockedDecision.blockingSubjectId != "phase2a.synthetic.blocker")
                throw new InvalidOperationException(
                    "El preflight de puerta no rechazó geometría estática incompatible.");
            if (door.IsMoving)
                throw new InvalidOperationException(
                    "BBSIS inició movimiento de puerta; esa decisión pertenece al sistema consumidor.");
        }
        finally
        {
            if (doorObject != null)
            {
                BistroBuilderDoorSpatialAdapter adapter =
                    doorObject.GetComponent<BistroBuilderDoorSpatialAdapter>();
                adapter?.ReleaseMotionReservation();
            }
            if (blockerObject != null) UnityEngine.Object.DestroyImmediate(blockerObject);
            if (doorObject != null) UnityEngine.Object.DestroyImmediate(doorObject);
            if (blockerContract != null) UnityEngine.Object.DestroyImmediate(blockerContract);
            if (doorContract != null) UnityEngine.Object.DestroyImmediate(doorContract);
            spatial.ResetTransientRuntimeStateAfterLoad();
            navigation.RebuildNavigationTopology();
        }
    }

    private static BistroBuilderSpatialContractDefinition CreateDoorContract()
    {
        BistroBuilderSpatialContractDefinition contract =
            ScriptableObject.CreateInstance<BistroBuilderSpatialContractDefinition>();
        contract.ConfigureForEditor(
            "phase2a.synthetic.door.contract",
            "architecture.door",
            BistroBuilderAdaptiveSpatialProxyMode.Simple,
            new[] { "architecture.door", "dynamic.sweep", "traversal.gate" });
        contract.SetContractVersionForEditor(2);
        contract.ClearSemanticGeometryForEditor();
        contract.AddGateForEditor(new BistroBuilderSpatialGateDefinition
        {
            gateId = "door.passage",
            localStart = Vector3.left * 0.45f,
            localEnd = Vector3.right * 0.45f,
            minimumWidth = 0.75f,
            criticalRoute = true
        });
        return contract;
    }
    private static bool ContainsRole(
        List<BistroBuilderSpatialSemanticVolume> volumes,
        BistroBuilderSpatialSemanticRole role)
    {
        for (int i = 0; i < volumes.Count; i++)
            if (volumes[i] != null && volumes[i].role == role)
                return true;
        return false;
    }

    private static void Finish(bool success, string message, bool cli)
    {
        string report = "=== BISTRO BUILDER - BBSIS FASE 2A / PLAY MODE REAL ===\n" +
            (success ? "[PASS] " : "[FAIL] ") + message;
        File.WriteAllText(Path.GetFullPath(ReportPath), report);
        if (success) Debug.Log(report); else Debug.LogError(report);
        SessionState.SetBool(SuccessKey, success);
        SessionState.SetString(StageKey, cli ? "exit_cli" : "exit_menu");
        EditorApplication.ExitPlaymode();
    }
}
