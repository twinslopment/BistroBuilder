using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using BistroBuilder.ConstructionAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

internal sealed class BistroBuilderB8GraphProvider :
    MonoBehaviour,
    IRestaurantPlacementLinkedGroupProvider
{
    private readonly Dictionary<int, List<RestaurantAreaMember>> edges =
        new Dictionary<int, List<RestaurantAreaMember>>();

    public int Priority => 0;
    public bool IsLinkEnabled => true;

    public void AddEdge(
        RestaurantAreaMember from,
        params RestaurantAreaMember[] to)
    {
        if (from == null)
            return;

        if (!edges.TryGetValue(
                from.GetInstanceID(),
                out List<RestaurantAreaMember> list))
        {
            list = new List<RestaurantAreaMember>();
            edges.Add(from.GetInstanceID(), list);
        }

        if (to == null)
            return;

        for (int i = 0; i < to.Length; i++)
            list.Add(to[i]);
    }

    public void CollectLinkedMembers(
        RestaurantAreaMember rootMember,
        List<RestaurantAreaMember> results)
    {
        if (rootMember == null || results == null)
            return;

        if (!edges.TryGetValue(
                rootMember.GetInstanceID(),
                out List<RestaurantAreaMember> list))
        {
            return;
        }

        for (int i = 0; i < list.Count; i++)
            results.Add(list[i]);
    }

    public void NotifyLinkedGroupPoseApplied(
        RestaurantAreaMember rootMember,
        IReadOnlyList<RestaurantAreaMember> linkedMembers)
    {
    }
}

internal sealed class BistroBuilderB8ProbeCommand :
    IRestaurantEditHistoryCommand
{
    public sealed class State
    {
        public int Value;
    }

    private readonly State state;
    private readonly RestaurantEditHistoryCommandType type;
    private readonly bool failUndo;
    private readonly bool failRedo;

    public int ReleaseCount { get; private set; }

    public RestaurantEditHistoryCommandType CommandType => type;
    public string Description => "B8 probe";
    public Object PrimaryTarget => null;
    public bool IsValid => state != null;

    public BistroBuilderB8ProbeCommand(
        State state,
        RestaurantEditHistoryCommandType type,
        bool failUndo = false,
        bool failRedo = false)
    {
        this.state = state;
        this.type = type;
        this.failUndo = failUndo;
        this.failRedo = failRedo;
    }

    public bool TryUndo(out RestaurantEditHistoryCommandResult result)
    {
        if (failUndo)
        {
            result = RestaurantEditHistoryCommandResult.Failure(
                RestaurantEditHistoryCommandFailureReason.CommandUnavailable,
                null,
                null,
                default,
                "Injected undo failure.");
            return false;
        }

        state.Value = 0;
        result = RestaurantEditHistoryCommandResult.Success(
            null,
            null,
            "undo");
        return true;
    }

    public bool TryRedo(out RestaurantEditHistoryCommandResult result)
    {
        if (failRedo)
        {
            result = RestaurantEditHistoryCommandResult.Failure(
                RestaurantEditHistoryCommandFailureReason.CommandUnavailable,
                null,
                null,
                default,
                "Injected redo failure.");
            return false;
        }

        state.Value = 1;
        result = RestaurantEditHistoryCommandResult.Success(
            null,
            null,
            "redo");
        return true;
    }

    public void ReleaseResources()
    {
        ReleaseCount++;
    }
}

public static class BistroBuilderEditorV2B8MultiSelectionGroupsSelfTest
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";

    private static readonly List<string> Lines =
        new List<string>(320);

    private static readonly List<Object> Cleanup =
        new List<Object>(128);

    private static int pass;
    private static int fail;

    public static void RunFromCommandLine()
    {
        bool ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    [MenuItem(
        "Bistro Builder/QA/Editor V2/B8 Multiselection Groups Self Test")]
    public static void RunFromMenu()
    {
        Run();
    }

    private static bool Run()
    {
        Lines.Clear();
        Cleanup.Clear();
        pass = 0;
        fail = 0;

        Lines.Add(
            "EDITOR V2 - B8 MULTISELECTION / GROUPS SELF TEST");

        try
        {
            TestSelectionSetContract();
            TestCompoundHistoryAtomicity();
            TestSemanticGraphAndStress();
            TestRealSceneIntegration();
        }
        catch (Exception exception)
        {
            fail++;
            Lines.Add(
                "FAIL - Excepción no controlada: " +
                exception.GetType().Name + " - " +
                exception.Message);
            Lines.Add(exception.StackTrace ?? string.Empty);
        }
        finally
        {
            for (int i = Cleanup.Count - 1; i >= 0; i--)
            {
                Object target = Cleanup[i];
                if (target != null)
                    Object.DestroyImmediate(target);
            }

            Cleanup.Clear();
        }

        return Finish();
    }

    private static void TestSelectionSetContract()
    {
        GameObject go = NewGo("__B8_SelectionSet");
        BistroBuilderEditorV2SelectionCoordinator coordinator =
            go.AddComponent<BistroBuilderEditorV2SelectionCoordinator>();

        BistroBuilderEditorV2Selection moveRotate =
            MakeSelection(
                "zeta",
                BistroBuilderEditorV2SelectionCapability.Inspect |
                BistroBuilderEditorV2SelectionCapability.Move |
                BistroBuilderEditorV2SelectionCapability.Rotate |
                BistroBuilderEditorV2SelectionCapability.Delete |
                BistroBuilderEditorV2SelectionCapability.Duplicate);

        BistroBuilderEditorV2Selection moveOnly =
            MakeSelection(
                "alpha",
                BistroBuilderEditorV2SelectionCapability.Inspect |
                BistroBuilderEditorV2SelectionCapability.Move |
                BistroBuilderEditorV2SelectionCapability.Delete |
                BistroBuilderEditorV2SelectionCapability.Duplicate);

        BistroBuilderEditorV2Selection duplicate =
            MakeSelection(
                "alpha",
                moveOnly.capabilities);

        var unordered =
            new List<BistroBuilderEditorV2Selection>
            {
                moveRotate,
                moveOnly,
                duplicate
            };

        bool replaced = coordinator.ReplaceSelectionSet(
            unordered,
            "zeta",
            out string error);

        Check(
            replaced && string.IsNullOrEmpty(error),
            "B8 acepta un Selection Set explícito válido");

        Check(
            coordinator.SelectionCount == 2,
            "B8 deduplica identidades repetidas");

        Check(
            coordinator.PrimarySelection.stableId == "zeta",
            "B8 conserva una selección primaria explícita");

        Check(
            coordinator.SelectionSetSupports(
                BistroBuilderEditorV2SelectionCapability.Move) &&
            !coordinator.SelectionSetSupports(
                BistroBuilderEditorV2SelectionCapability.Rotate),
            "B8 calcula capacidades por intersección real del conjunto");

        var copied =
            new List<BistroBuilderEditorV2Selection>();

        coordinator.CopySelectionSet(copied, true);

        Check(
            copied.Count == 2 &&
            copied[0].stableId == "alpha" &&
            copied[1].stableId == "zeta",
            "B8 expone orden estable independiente del orden de entrada");

        var mixed =
            new List<BistroBuilderEditorV2Selection>
            {
                moveOnly,
                new BistroBuilderEditorV2Selection
                {
                    family =
                        BistroBuilderEditorV2ToolFamily.Construction,
                    kind =
                        BistroBuilderEditorV2SelectionKind.Wall,
                    stableId = "wall",
                    displayName = "Wall",
                    capabilities =
                        BistroBuilderEditorV2SelectionCapability.Inspect,
                    persistentIdentity = true
                }
            };

        Check(
            !coordinator.ReplaceSelectionSet(
                mixed,
                null,
                out _),
            "B8 rechaza mezclar autoridades distintas en un mismo set");

        const int stressCount = 1000;
        var stress =
            new List<BistroBuilderEditorV2Selection>(
                stressCount + 100);

        for (int i = stressCount - 1; i >= 0; i--)
        {
            stress.Add(
                MakeSelection(
                    "stress_" + i.ToString("D4"),
                    BistroBuilderEditorV2SelectionCapability.Inspect |
                    BistroBuilderEditorV2SelectionCapability.Move));

            if ((i % 10) == 0)
                stress.Add(stress[stress.Count - 1]);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        bool stressAccepted = coordinator.ReplaceSelectionSet(
            stress,
            "stress_0500",
            out _);

        stopwatch.Stop();

        copied.Clear();
        coordinator.CopySelectionSet(copied, true);

        Check(
            stressAccepted &&
            coordinator.SelectionCount == stressCount &&
            copied.Count == stressCount,
            "B8 Selection Set soporta 1000 miembros sin duplicados");

        Check(
            copied[0].stableId == "stress_0000" &&
            copied[copied.Count - 1].stableId == "stress_0999",
            "B8 Selection Set de estrés mantiene determinismo");

        Lines.Add(
            "METRIC - SELECTION_1000_MS=" +
            stopwatch.Elapsed.TotalMilliseconds.ToString("0.000"));
    }

    private static void TestCompoundHistoryAtomicity()
    {
        var a = new BistroBuilderB8ProbeCommand.State { Value = 1 };
        var b = new BistroBuilderB8ProbeCommand.State { Value = 1 };
        var c = new BistroBuilderB8ProbeCommand.State { Value = 1 };

        var ca = new BistroBuilderB8ProbeCommand(
            a,
            RestaurantEditHistoryCommandType.Delete);
        var cb = new BistroBuilderB8ProbeCommand(
            b,
            RestaurantEditHistoryCommandType.Delete);
        var cc = new BistroBuilderB8ProbeCommand(
            c,
            RestaurantEditHistoryCommandType.Delete);

        var compound =
            new BistroBuilderEditorV2CompoundPlaceableHistoryCommand(
                RestaurantEditHistoryCommandType.Delete,
                "probe",
                new IRestaurantEditHistoryCommand[]
                {
                    ca,
                    cb,
                    cc
                });

        Check(
            compound.IsValid,
            "B8 comando compuesto válido exige hijos homogéneos");

        bool undone = compound.TryUndo(out _);

        Check(
            undone &&
            a.Value == 0 &&
            b.Value == 0 &&
            c.Value == 0,
            "B8 Undo compuesto aplica todos los miembros");

        bool redone = compound.TryRedo(out _);

        Check(
            redone &&
            a.Value == 1 &&
            b.Value == 1 &&
            c.Value == 1,
            "B8 Redo compuesto aplica todos los miembros");

        compound.ReleaseResources();

        Check(
            ca.ReleaseCount == 1 &&
            cb.ReleaseCount == 1 &&
            cc.ReleaseCount == 1,
            "B8 libera exactamente una vez los recursos de cada hijo");

        var u0 = new BistroBuilderB8ProbeCommand.State { Value = 1 };
        var u1 = new BistroBuilderB8ProbeCommand.State { Value = 1 };
        var u2 = new BistroBuilderB8ProbeCommand.State { Value = 1 };

        var undoFault =
            new BistroBuilderEditorV2CompoundPlaceableHistoryCommand(
                RestaurantEditHistoryCommandType.Delete,
                "fault undo",
                new IRestaurantEditHistoryCommand[]
                {
                    new BistroBuilderB8ProbeCommand(
                        u0,
                        RestaurantEditHistoryCommandType.Delete),
                    new BistroBuilderB8ProbeCommand(
                        u1,
                        RestaurantEditHistoryCommandType.Delete,
                        failUndo: true),
                    new BistroBuilderB8ProbeCommand(
                        u2,
                        RestaurantEditHistoryCommandType.Delete)
                });

        Check(
            !undoFault.TryUndo(out _) &&
            u0.Value == 1 &&
            u1.Value == 1 &&
            u2.Value == 1,
            "B8 fault injection Undo revierte el conjunto completo");

        var r0 = new BistroBuilderB8ProbeCommand.State { Value = 0 };
        var r1 = new BistroBuilderB8ProbeCommand.State { Value = 0 };
        var r2 = new BistroBuilderB8ProbeCommand.State { Value = 0 };

        var redoFault =
            new BistroBuilderEditorV2CompoundPlaceableHistoryCommand(
                RestaurantEditHistoryCommandType.Create,
                "fault redo",
                new IRestaurantEditHistoryCommand[]
                {
                    new BistroBuilderB8ProbeCommand(
                        r0,
                        RestaurantEditHistoryCommandType.Create),
                    new BistroBuilderB8ProbeCommand(
                        r1,
                        RestaurantEditHistoryCommandType.Create,
                        failRedo: true),
                    new BistroBuilderB8ProbeCommand(
                        r2,
                        RestaurantEditHistoryCommandType.Create)
                });

        Check(
            !redoFault.TryRedo(out _) &&
            r0.Value == 0 &&
            r1.Value == 0 &&
            r2.Value == 0,
            "B8 fault injection Redo revierte el conjunto completo");

        var invalid =
            new BistroBuilderEditorV2CompoundPlaceableHistoryCommand(
                RestaurantEditHistoryCommandType.Create,
                "mixed",
                new IRestaurantEditHistoryCommand[]
                {
                    new BistroBuilderB8ProbeCommand(
                        new BistroBuilderB8ProbeCommand.State(),
                        RestaurantEditHistoryCommandType.Create),
                    new BistroBuilderB8ProbeCommand(
                        new BistroBuilderB8ProbeCommand.State(),
                        RestaurantEditHistoryCommandType.Delete)
                });

        Check(
            !invalid.IsValid,
            "B8 rechaza comandos compuestos de tipos incompatibles");
    }

    private static void TestSemanticGraphAndStress()
    {
        GameObject rig = NewGo("__B8_GraphRig");
        BistroBuilderB8GraphProvider provider =
            rig.AddComponent<BistroBuilderB8GraphProvider>();

        RestaurantPlacementLinkedGroupService linked =
            rig.AddComponent<RestaurantPlacementLinkedGroupService>();

        RestaurantAreaMember a =
            NewMember("__B8_A", new Vector3(0f, 0f, 0f));
        RestaurantAreaMember b =
            NewMember("__B8_B", new Vector3(1f, 0f, 0f));
        RestaurantAreaMember c =
            NewMember("__B8_C", new Vector3(2f, 0f, 0f));

        provider.AddEdge(a, b, b, a);
        provider.AddEdge(b, c);
        provider.AddEdge(c, a, b);

        linked.RefreshProviders();

        var graph = new List<RestaurantAreaMember>();

        int graphCount = linked.CopyLinkedMembers(a, graph);

        Check(
            graphCount == 2 &&
            graph.Contains(b) &&
            graph.Contains(c) &&
            !graph.Contains(a),
            "B8 cierra relaciones transitivas sin duplicados ni raíz");

        var graphAgain = new List<RestaurantAreaMember>();
        linked.CopyLinkedMembers(a, graphAgain);

        Check(
            SameReferenceOrder(graph, graphAgain),
            "B8 grafo semántico es determinista entre ejecuciones");

        const int followerCount = 24;
        var followers = new List<RestaurantAreaMember>(followerCount);
        RestaurantAreaMember stressRoot =
            NewMember(
                "__B8_StressRoot",
                new Vector3(20f, 0f, 20f));

        for (int i = 0; i < followerCount; i++)
        {
            float angle =
                (Mathf.PI * 2f * i) / followerCount;

            RestaurantAreaMember follower =
                NewMember(
                    "__B8_Stress_" + i.ToString("D2"),
                    stressRoot.transform.position +
                    new Vector3(
                        Mathf.Cos(angle) * 2f,
                        0f,
                        Mathf.Sin(angle) * 2f));

            followers.Add(follower);
            provider.AddEdge(stressRoot, follower);
        }

        linked.RefreshProviders();

        var beforePositions = new Vector3[followerCount];
        var beforeRotations = new Quaternion[followerCount];

        for (int i = 0; i < followerCount; i++)
        {
            beforePositions[i] = followers[i].transform.position;
            beforeRotations[i] = followers[i].transform.rotation;
        }

        Vector3 rootBefore = stressRoot.transform.position;
        Quaternion rootRotationBefore =
            stressRoot.transform.rotation;
        RestaurantPlacementStateSnapshot rootSnapshot =
            RestaurantPlacementStateSnapshot.Capture(stressRoot);

        linked.BeginSession(stressRoot);

        for (int i = 0; i < 64; i++)
        {
            linked.PreparePreviewPose(
                stressRoot,
                rootBefore + new Vector3(i * 0.001f, 0f, 0f),
                Quaternion.AngleAxis(
                    i * 0.1f,
                    Vector3.up));
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocatedBefore =
            GC.GetAllocatedBytesForCurrentThread();

        Stopwatch stopwatch = Stopwatch.StartNew();

        const int iterations = 10000;

        bool finite = true;

        for (int i = 0; i < iterations; i++)
        {
            float angle = (i % 360) * 0.5f;

            Vector3 target =
                rootBefore +
                new Vector3(
                    Mathf.Sin(i * 0.017f) * 0.75f,
                    0f,
                    Mathf.Cos(i * 0.013f) * 0.75f);

            Quaternion rotation =
                Quaternion.AngleAxis(
                    angle,
                    Vector3.up);

            linked.PreparePreviewPose(
                stressRoot,
                target,
                rotation);

            finite &= Finite(stressRoot.transform.position);

            if ((i & 255) == 0)
            {
                for (int j = 0; j < followers.Count; j++)
                    finite &= Finite(
                        followers[j].transform.position);
            }
        }

        stopwatch.Stop();

        long allocated =
            GC.GetAllocatedBytesForCurrentThread() -
            allocatedBefore;

        linked.CancelSession(stressRoot);
        rootSnapshot.Restore(stressRoot);

        bool restored =
            Nearly(rootBefore, stressRoot.transform.position) &&
            Nearly(
                rootRotationBefore,
                stressRoot.transform.rotation);

        for (int i = 0; i < followerCount; i++)
        {
            restored &=
                Nearly(
                    beforePositions[i],
                    followers[i].transform.position) &&
                Nearly(
                    beforeRotations[i],
                    followers[i].transform.rotation);
        }

        Check(
            finite,
            "B8 fuzz/stress 10000 transformaciones no produce NaN/Infinity");

        Check(
            restored,
            "B8 cancelación restaura exactamente raíz y 24 seguidores");

        Check(
            stopwatch.Elapsed.TotalMilliseconds < 3000.0,
            "B8 stress 10000 poses queda bajo 3 s");

        Check(
            allocated < 1024L * 1024L,
            "B8 stress no genera más de 1 MB tras warmup");

        Lines.Add(
            "METRIC - GROUP_10000_MS=" +
            stopwatch.Elapsed.TotalMilliseconds.ToString("0.000"));

        Lines.Add(
            "METRIC - GROUP_10000_ALLOCATED_BYTES=" +
            allocated);
    }

    private static void TestRealSceneIntegration()
    {
        EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);

        InvokeBootstrap(
            typeof(BistroBuilderConstructionAuthoringRuntimeBootstrap));
        InvokeBootstrap(
            typeof(BistroBuilderEditorV2RuntimeBootstrap));

        RestaurantEditModeService editMode =
            Find<RestaurantEditModeService>();
        BistroBuilderEditorV2Coordinator editor =
            Find<BistroBuilderEditorV2Coordinator>();
        BistroBuilderEditorV2SelectionCoordinator selection =
            Find<BistroBuilderEditorV2SelectionCoordinator>();
        BistroBuilderEditorV2GroupOperationService groups =
            Find<BistroBuilderEditorV2GroupOperationService>();
        RestaurantEditInteractionController controller =
            Find<RestaurantEditInteractionController>();
        RestaurantPlacementLinkedGroupService linked =
            Find<RestaurantPlacementLinkedGroupService>();
        RestaurantPlacementHistoryService history =
            Find<RestaurantPlacementHistoryService>();
        BistroBuilderEditorV2GlobalHistory globalHistory =
            Find<BistroBuilderEditorV2GlobalHistory>();
        RestaurantPlaceableRegistry registry =
            Find<RestaurantPlaceableRegistry>();
        RestaurantPlacementTransactionService transaction =
            Find<RestaurantPlacementTransactionService>();
        RestaurantPlaceableCreationService creation =
            Find<RestaurantPlaceableCreationService>();
        RestaurantPlaceableDeletionService deletion =
            Find<RestaurantPlaceableDeletionService>();

        Check(
            editMode != null &&
            editor != null &&
            selection != null &&
            groups != null &&
            controller != null &&
            linked != null &&
            history != null &&
            globalHistory != null &&
            registry != null &&
            transaction != null &&
            creation != null &&
            deletion != null,
            "B8 bootstrap instala todas las autoridades necesarias");

        if (editMode == null ||
            editor == null ||
            selection == null ||
            groups == null ||
            controller == null ||
            linked == null ||
            history == null ||
            globalHistory == null ||
            registry == null ||
            transaction == null ||
            creation == null ||
            deletion == null)
        {
            return;
        }

        InitializeRuntimeAuthorities(
            registry,
            transaction,
            history,
            creation,
            deletion);

        int groupServiceCount =
            Object.FindObjectsByType<
                BistroBuilderEditorV2GroupOperationService>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length;

        int explicitProviderCount =
            Object.FindObjectsByType<
                BistroBuilderEditorV2ExplicitSelectionLinkedGroupProvider>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length;

        InvokeBootstrap(
            typeof(BistroBuilderEditorV2RuntimeBootstrap));

        Check(
            groupServiceCount == 1 &&
            explicitProviderCount == 1 &&
            Object.FindObjectsByType<
                BistroBuilderEditorV2GroupOperationService>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length == 1 &&
            Object.FindObjectsByType<
                BistroBuilderEditorV2ExplicitSelectionLinkedGroupProvider>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length == 1,
            "B8 bootstrap es idempotente");

        bool entered =
            editMode.IsEditModeActive ||
            editMode.TryEnterEditMode(out _, out _);

        Check(
            entered,
            "B8 entra en modo edición real");

        if (!entered)
            return;

        bool furnitureActive =
            editor.TryActivateTool(
                BistroBuilderEditorV2ToolFamily.Furniture,
                "furniture",
                out _);

        Check(
            furnitureActive,
            "B8 activa Furniture mediante Editor V2 Coordinator");

        List<RestaurantPlaceableObject> candidates =
            FindMovablePlaceables(registry);

        Lines.Add(
            "METRIC - REAL_MOVABLE_CANDIDATES=" +
            candidates.Count);

        Check(
            candidates.Count >= 2,
            "B8 encuentra al menos dos colocables reales para integración");

        if (candidates.Count < 2)
            return;

        RestaurantPlaceableObject p0 = candidates[0];
        RestaurantPlaceableObject p1 = candidates[1];

        bool s0 = controller.TrySelectPlaceable(p0);
        bool s1 = controller.TrySelectPlaceable(p1, true, true);

        Check(
            s0 && s1 &&
            selection.SelectionCount == 2 &&
            selection.HasMultipleSelection,
            "B8 selección programática reproduce clic + Shift+clic");

        Check(
            selection.PrimarySelection.stableId == p1.InstanceId,
            "B8 último Shift+clic se convierte en primario");

        bool toggledOff =
            controller.TrySelectPlaceable(p1, true, true);

        Check(
            toggledOff &&
            selection.SelectionCount == 1 &&
            !selection.ContainsSelection(
                BistroBuilderEditorV2ToolFamily.Furniture,
                BistroBuilderEditorV2SelectionKind.Furniture,
                p1.InstanceId) &&
            controller.SelectedMember != null &&
            ReferenceEquals(
                controller.SelectedMember,
                p0.GetComponent<RestaurantAreaMember>()),
            "B8 Shift+clic sobre miembro seleccionado lo retira y recupera primario");

        bool readded =
            controller.TrySelectPlaceable(p1, true, true);

        Check(
            readded &&
            selection.SelectionCount == 2 &&
            selection.ContainsSelection(
                BistroBuilderEditorV2ToolFamily.Furniture,
                BistroBuilderEditorV2SelectionKind.Furniture,
                p1.InstanceId),
            "B8 miembro retirado puede reincorporarse sin duplicarse");

        TestRealMoveAndHistory(
            groups,
            selection,
            controller,
            linked,
            history,
            globalHistory);

        TestRealDeleteAtomicity(
            p0,
            p1,
            selection,
            controller,
            groups,
            history,
            globalHistory,
            registry);

        TestRealDuplicateAtomicity(
            p0,
            p1,
            selection,
            controller,
            groups,
            history,
            globalHistory,
            registry);

        if (controller.HasActivePlacement)
            controller.CancelActivePlacement();

        controller.ClearSelection();

        if (editMode.IsEditModeActive)
        {
            Check(
                editMode.TryExitEditMode(true, out _),
                "B8 sale limpio del modo edición");
        }
    }

    private static void TestRealMoveAndHistory(
        BistroBuilderEditorV2GroupOperationService groups,
        BistroBuilderEditorV2SelectionCoordinator selection,
        RestaurantEditInteractionController controller,
        RestaurantPlacementLinkedGroupService linked,
        RestaurantPlacementHistoryService history,
        BistroBuilderEditorV2GlobalHistory globalHistory)
    {
        RestaurantAreaMember root =
            controller.SelectedMember;

        string beginError = string.Empty;
        bool began =
            root != null &&
            groups.TryBeginMoveSelection(out beginError);

        Check(
            began,
            "B8 inicia movimiento grupal sobre Placement real" +
            (string.IsNullOrEmpty(beginError)
                ? string.Empty
                : ": " + beginError));

        if (root == null ||
            !controller.HasActivePlacement)
        {
            return;
        }

        var linkedMembers = new List<RestaurantAreaMember>();
        linked.CopyLinkedMembers(root, linkedMembers);

        Check(
            linkedMembers.Count >=
                Mathf.Max(0, selection.SelectionCount - 1),
            "B8 Linked Groups absorbe todos los miembros explícitos");

        Vector3 rootBefore = root.transform.position;
        Quaternion rootRotationBefore = root.transform.rotation;

        var beforePositions =
            new Dictionary<int, Vector3>();
        var beforeRotations =
            new Dictionary<int, Quaternion>();

        beforePositions[root.GetInstanceID()] = rootBefore;
        beforeRotations[root.GetInstanceID()] = rootRotationBefore;

        for (int i = 0; i < linkedMembers.Count; i++)
        {
            RestaurantAreaMember member = linkedMembers[i];
            beforePositions[member.GetInstanceID()] =
                member.transform.position;
            beforeRotations[member.GetInstanceID()] =
                member.transform.rotation;
        }

        Quaternion previewRotation =
            Quaternion.AngleAxis(17f, Vector3.up) *
            rootRotationBefore;

        Vector3 previewPosition =
            rootBefore + new Vector3(0.23f, 0f, -0.17f);

        bool previewed =
            controller.TryPreviewActivePlacementAtWorldPose(
                previewPosition,
                previewRotation,
                out _,
                out _);

        bool rigid = previewed;

        Quaternion deltaRotation =
            previewRotation *
            Quaternion.Inverse(rootRotationBefore);

        for (int i = 0; i < linkedMembers.Count; i++)
        {
            RestaurantAreaMember member = linkedMembers[i];

            Vector3 expectedPosition =
                previewPosition +
                deltaRotation *
                (beforePositions[member.GetInstanceID()] -
                 rootBefore);

            Quaternion expectedRotation =
                deltaRotation *
                beforeRotations[member.GetInstanceID()];

            rigid &=
                Nearly(
                    expectedPosition,
                    member.transform.position,
                    0.0001f) &&
                Nearly(
                    expectedRotation,
                    member.transform.rotation,
                    0.001f);
        }

        Check(
            rigid,
            "B8 aplica transformación rígida matemática al conjunto");

        bool cancelled = controller.CancelActivePlacement();

        bool restored =
            cancelled &&
            Nearly(
                rootBefore,
                root.transform.position,
                0.0001f) &&
            Nearly(
                rootRotationBefore,
                root.transform.rotation,
                0.001f);

        for (int i = 0; i < linkedMembers.Count; i++)
        {
            RestaurantAreaMember member = linkedMembers[i];
            restored &=
                Nearly(
                    beforePositions[member.GetInstanceID()],
                    member.transform.position,
                    0.0001f) &&
                Nearly(
                    beforeRotations[member.GetInstanceID()],
                    member.transform.rotation,
                    0.001f);
        }

        Check(
            restored,
            "B8 cancelar movimiento grupal restaura snapshot exacto");

        int historyBefore = history.UndoCount;

        bool beganSecond =
            groups.TryBeginMoveSelection(out _);

        bool validCandidate = false;
        Vector3 committedRootPosition = rootBefore;

        Vector3[] offsets =
        {
            new Vector3(0.01f, 0f, 0f),
            new Vector3(-0.01f, 0f, 0f),
            new Vector3(0f, 0f, 0.01f),
            new Vector3(0f, 0f, -0.01f),
            new Vector3(0.02f, 0f, 0.02f),
            new Vector3(-0.02f, 0f, -0.02f),
            new Vector3(0.05f, 0f, 0f),
            new Vector3(0f, 0f, 0.05f)
        };

        if (beganSecond)
        {
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 candidate =
                    rootBefore + offsets[i];

                bool ok =
                    controller.TryPreviewActivePlacementAtWorldPose(
                        candidate,
                        rootRotationBefore,
                        out RestaurantPlacementValidationResult validation,
                        out _);

                if (ok && validation.IsValid)
                {
                    validCandidate = true;
                    committedRootPosition = candidate;
                    break;
                }
            }
        }

        Check(
            beganSecond && validCandidate,
            "B8 encuentra una pose real válida para commit atómico");

        if (!beganSecond || !validCandidate)
        {
            controller.CancelActivePlacement();
            return;
        }

        InvokePrivate(
            controller,
            "CommitActivePlacement");

        bool oneCommand =
            !controller.HasActivePlacement &&
            history.UndoCount == historyBefore + 1;

        Check(
            oneCommand,
            "B8 movimiento de varios objetos genera un único comando histórico");

        bool undo =
            globalHistory.TryUndo(out string undoError);

        bool undoRestored =
            undo &&
            Nearly(
                rootBefore,
                root.transform.position,
                0.0001f) &&
            Nearly(
                rootRotationBefore,
                root.transform.rotation,
                0.001f);

        Check(
            undoRestored,
            "B8 Ctrl+Z global restaura el grupo en una sola acción" +
            (string.IsNullOrEmpty(undoError)
                ? string.Empty
                : ": " + undoError));

        bool redo =
            globalHistory.TryRedo(out string redoError);

        Check(
            redo &&
            Nearly(
                committedRootPosition,
                root.transform.position,
                0.0001f),
            "B8 Ctrl+Y global rehace el grupo en una sola acción" +
            (string.IsNullOrEmpty(redoError)
                ? string.Empty
                : ": " + redoError));

        bool finalUndo =
            globalHistory.TryUndo(out _);

        Check(
            finalUndo &&
            Nearly(
                rootBefore,
                root.transform.position,
                0.0001f),
            "B8 segundo Undo devuelve la escena a baseline");
    }

    private static void TestRealDeleteAtomicity(
        RestaurantPlaceableObject p0,
        RestaurantPlaceableObject p1,
        BistroBuilderEditorV2SelectionCoordinator selection,
        RestaurantEditInteractionController controller,
        BistroBuilderEditorV2GroupOperationService groups,
        RestaurantPlacementHistoryService history,
        BistroBuilderEditorV2GlobalHistory globalHistory,
        RestaurantPlaceableRegistry registry)
    {
        if (p0 == null || p1 == null ||
            !p0.gameObject.activeInHierarchy ||
            !p1.gameObject.activeInHierarchy)
        {
            Check(false, "B8 delete integration dispone de objetivos activos");
            return;
        }

        string id0 = p0.InstanceId;
        string id1 = p1.InstanceId;

        RestaurantAreaMember m0 =
            p0.GetComponent<RestaurantAreaMember>();
        RestaurantAreaMember m1 =
            p1.GetComponent<RestaurantAreaMember>();

        RestaurantPlacementStateSnapshot s0 =
            RestaurantPlacementStateSnapshot.Capture(m0);
        RestaurantPlacementStateSnapshot s1 =
            RestaurantPlacementStateSnapshot.Capture(m1);

        controller.TrySelectPlaceable(p0);
        controller.TrySelectPlaceable(p1, true, true);

        int historyBefore = history.UndoCount;

        bool deleted =
            groups.TryDeleteSelection(out string deleteError);

        Check(
            deleted &&
            history.UndoCount == historyBefore + 1 &&
            history.PeekUndoCommand() is
                BistroBuilderEditorV2CompoundPlaceableHistoryCommand,
            "B8 eliminar selección publica un único comando compuesto" +
            (string.IsNullOrEmpty(deleteError)
                ? string.Empty
                : ": " + deleteError));

        if (!deleted)
            return;

        Check(
            !p0.gameObject.activeSelf &&
            !p1.gameObject.activeSelf &&
            !registry.TryGetByInstanceId(id0, out _) &&
            !registry.TryGetByInstanceId(id1, out _),
            "B8 delete desactiva el conjunto completo");

        bool undone =
            globalHistory.TryUndo(out string undoError);

        bool identitiesRestored =
            undone &&
            p0.gameObject.activeSelf &&
            p1.gameObject.activeSelf &&
            registry.TryGetByInstanceId(
                id0,
                out RestaurantPlaceableObject restored0) &&
            registry.TryGetByInstanceId(
                id1,
                out RestaurantPlaceableObject restored1) &&
            ReferenceEquals(restored0, p0) &&
            ReferenceEquals(restored1, p1);

        Check(
            identitiesRestored,
            "B8 Undo delete restaura exactamente identidades y registros" +
            (string.IsNullOrEmpty(undoError)
                ? string.Empty
                : ": " + undoError));

        bool poseRestored =
            identitiesRestored &&
            SnapshotMatches(s0, m0) &&
            SnapshotMatches(s1, m1);

        Check(
            poseRestored,
            "B8 Undo delete restaura poses exactas");

        bool individuallyEditable =
            controller.TrySelectPlaceable(p0) &&
            selection.SelectionCount == 1 &&
            controller.TrySelectPlaceable(p1) &&
            selection.SelectionCount == 1;

        Check(
            individuallyEditable,
            "B8 elementos restaurados siguen siendo editables individualmente");

        bool redone =
            globalHistory.TryRedo(out _);

        Check(
            redone &&
            !p0.gameObject.activeSelf &&
            !p1.gameObject.activeSelf,
            "B8 Redo delete vuelve a retirar todo el conjunto");

        bool restoredAgain =
            globalHistory.TryUndo(out _);

        Check(
            restoredAgain &&
            p0.gameObject.activeSelf &&
            p1.gameObject.activeSelf,
            "B8 Undo final de delete recupera baseline");
    }

    private static void TestRealDuplicateAtomicity(
        RestaurantPlaceableObject p0,
        RestaurantPlaceableObject p1,
        BistroBuilderEditorV2SelectionCoordinator selection,
        RestaurantEditInteractionController controller,
        BistroBuilderEditorV2GroupOperationService groups,
        RestaurantPlacementHistoryService history,
        BistroBuilderEditorV2GlobalHistory globalHistory,
        RestaurantPlaceableRegistry registry)
    {
        controller.TrySelectPlaceable(p0);
        controller.TrySelectPlaceable(p1, true, true);

        int activeBefore =
            registry.RegisteredPlaceables.Count;
        int historyBefore =
            history.UndoCount;

        Vector3[] offsets =
        {
            new Vector3(0.5f, 0f, 0f),
            new Vector3(-0.5f, 0f, 0f),
            new Vector3(0f, 0f, 0.5f),
            new Vector3(0f, 0f, -0.5f),
            new Vector3(1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, -1f),
            new Vector3(1.5f, 0f, 0f),
            new Vector3(-1.5f, 0f, 0f),
            new Vector3(0f, 0f, 1.5f),
            new Vector3(0f, 0f, -1.5f),
            new Vector3(2f, 0f, 0f),
            new Vector3(-2f, 0f, 0f),
            new Vector3(0f, 0f, 2f),
            new Vector3(0f, 0f, -2f),
            new Vector3(3f, 0f, 0f),
            new Vector3(-3f, 0f, 0f),
            new Vector3(0f, 0f, 3f),
            new Vector3(0f, 0f, -3f)
        };

        IReadOnlyList<RestaurantPlaceableObject> created =
            Array.Empty<RestaurantPlaceableObject>();

        bool duplicated = false;
        Vector3 usedOffset = Vector3.zero;
        string lastError = string.Empty;

        for (int i = 0; i < offsets.Length; i++)
        {
            if (groups.TryDuplicateSelection(
                    offsets[i],
                    out created,
                    out lastError))
            {
                duplicated = true;
                usedOffset = offsets[i];
                break;
            }

            Check(
                registry.RegisteredPlaceables.Count == activeBefore &&
                history.UndoCount == historyBefore,
                "B8 duplicate fallido #" + (i + 1) +
                " no deja estado parcial");
        }

        // En una escena poblada dos artículos arbitrarios pueden carecer
        // de destino común; no equivale a un fallo del motor de duplicación.
        // Si el primer par solo produce rechazos espaciales correctos,
        // buscar otra pareja real, manteniendo la comprobación de rollback.
        if (!duplicated)
        {
            var candidates = FindMovablePlaceables(registry);
            int examinedPairs = 0;
            bool rollbackIntact = true;
            for (int i = 0; i < candidates.Count && !duplicated; i++)
            {
                for (int j = i + 1; j < candidates.Count && !duplicated; j++)
                {
                    var a = candidates[i];
                    var b = candidates[j];
                    if (a == null || b == null ||
                        a.ItemDefinition == null || b.ItemDefinition == null ||
                        a.ItemDefinition.Category != b.ItemDefinition.Category)
                        continue;
                    if (++examinedPairs > 100) break;
                    if (!controller.TrySelectPlaceable(a) ||
                        !controller.TrySelectPlaceable(b, true, true))
                        continue;

                    for (int k = 0; k < offsets.Length; k++)
                    {
                        if (groups.TryDuplicateSelection(
                                offsets[k], out created, out lastError))
                        {
                            p0 = a;
                            p1 = b;
                            usedOffset = offsets[k];
                            duplicated = true;
                            break;
                        }
                        rollbackIntact &=
                            registry.RegisteredPlaceables.Count == activeBefore &&
                            history.UndoCount == historyBefore;
                        if (!rollbackIntact) break;
                    }
                    if (!rollbackIntact) break;
                }
                if (examinedPairs > 100 || !rollbackIntact) break;
            }
            Check(rollbackIntact,
                "B8 búsqueda adversarial de pareja duplicable no deja estados parciales");
            Lines.Add("METRIC - B8_DUPLICATE_PAIRS_EXAMINED=" + examinedPairs);
        }

        Check(
            duplicated,
            "B8 encuentra destino real para duplicar dos artículos" +
            (duplicated ? string.Empty : ": " + lastError));

        if (!duplicated)
            return;

        Check(
            created.Count == 2 &&
            history.UndoCount == historyBefore + 1 &&
            history.PeekUndoCommand() is
                BistroBuilderEditorV2CompoundPlaceableHistoryCommand,
            "B8 duplicate de dos miembros crea un único historial");

        var sourceById =
            new List<RestaurantPlaceableObject>
            {
                p0,
                p1
            };

        sourceById.Sort(
            (a, b) => string.Compare(
                a.InstanceId,
                b.InstanceId,
                StringComparison.Ordinal));

        bool clonesCorrect =
            created.Count == sourceById.Count;

        var cloneIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0;
             clonesCorrect && i < created.Count;
             i++)
        {
            RestaurantPlaceableObject clone = created[i];
            RestaurantPlaceableObject source = sourceById[i];

            clonesCorrect &=
                clone != null &&
                clone.gameObject.activeSelf &&
                clone.InstanceId != source.InstanceId &&
                cloneIds.Add(clone.InstanceId) &&
                ReferenceEquals(
                    clone.ItemDefinition,
                    source.ItemDefinition) &&
                Nearly(
                    source.transform.position + usedOffset,
                    clone.transform.position,
                    0.001f) &&
                Nearly(
                    source.transform.rotation,
                    clone.transform.rotation,
                    0.001f);
        }

        Check(
            clonesCorrect,
            "B8 duplicate asigna IDs nuevos y preserva geometría relativa");

        bool undo =
            globalHistory.TryUndo(out string undoError);

        bool allInactive = undo;

        for (int i = 0; i < created.Count; i++)
            allInactive &=
                created[i] != null &&
                !created[i].gameObject.activeSelf;

        Check(
            allInactive &&
            registry.RegisteredPlaceables.Count == activeBefore,
            "B8 Undo duplicate retira los clones en una sola acción" +
            (string.IsNullOrEmpty(undoError)
                ? string.Empty
                : ": " + undoError));

        bool redo =
            globalHistory.TryRedo(out _);

        bool allActive = redo;

        for (int i = 0; i < created.Count; i++)
            allActive &=
                created[i] != null &&
                created[i].gameObject.activeSelf;

        Check(
            allActive &&
            registry.RegisteredPlaceables.Count == activeBefore + 2,
            "B8 Redo duplicate restaura clones e identidades");

        bool finalUndo =
            globalHistory.TryUndo(out _);

        Check(
            finalUndo &&
            registry.RegisteredPlaceables.Count == activeBefore,
            "B8 Undo final de duplicate recupera baseline activa");

        selection.ClearSelectionSet();
        controller.ClearSelection();
    }

    private static List<RestaurantPlaceableObject> FindMovablePlaceables(
        RestaurantPlaceableRegistry registry)
    {
        var results =
            new List<RestaurantPlaceableObject>();

        if (registry == null)
            return results;

        IReadOnlyCollection<RestaurantPlaceableObject> registered =
            registry.RegisteredPlaceables;

        foreach (RestaurantPlaceableObject candidate in registered)
        {
            if (candidate == null ||
                !candidate.gameObject.activeInHierarchy ||
                !candidate.HasValidDefinition ||
                !candidate.TryGetComponent(
                    out RestaurantEditableObject editable) ||
                !editable.EditingEnabled ||
                !editable.HasValidDefinition ||
                !editable.CanMove)
            {
                continue;
            }

            results.Add(candidate);
        }

        results.Sort(
            (a, b) => string.Compare(
                a.InstanceId,
                b.InstanceId,
                StringComparison.Ordinal));

        return results;
    }

    private static BistroBuilderEditorV2Selection MakeSelection(
        string id,
        BistroBuilderEditorV2SelectionCapability capabilities)
    {
        return new BistroBuilderEditorV2Selection
        {
            family = BistroBuilderEditorV2ToolFamily.Furniture,
            kind = BistroBuilderEditorV2SelectionKind.Furniture,
            stableId = id,
            displayName = id,
            capabilities = capabilities,
            persistentIdentity = true
        };
    }

    private static RestaurantAreaMember NewMember(
        string name,
        Vector3 position)
    {
        GameObject go = NewGo(name);
        go.transform.position = position;
        return go.AddComponent<RestaurantAreaMember>();
    }

    private static GameObject NewGo(string name)
    {
        GameObject go = new GameObject(name);
        Cleanup.Add(go);
        return go;
    }

    private static bool SameReferenceOrder(
        IReadOnlyList<RestaurantAreaMember> first,
        IReadOnlyList<RestaurantAreaMember> second)
    {
        if (first == null ||
            second == null ||
            first.Count != second.Count)
        {
            return false;
        }

        for (int i = 0; i < first.Count; i++)
        {
            if (!ReferenceEquals(first[i], second[i]))
                return false;
        }

        return true;
    }

    private static bool SnapshotMatches(
        RestaurantPlacementStateSnapshot snapshot,
        RestaurantAreaMember member)
    {
        if (!snapshot.IsValid || member == null)
            return false;

        snapshot.GetWorldPose(
            out Vector3 expectedPosition,
            out Quaternion expectedRotation);

        return
            Nearly(
                expectedPosition,
                member.transform.position,
                0.0001f) &&
            Nearly(
                expectedRotation,
                member.transform.rotation,
                0.001f) &&
            ReferenceEquals(
                snapshot.AssignedArea,
                member.AssignedArea);
    }

    private static bool Nearly(
        Vector3 a,
        Vector3 b,
        float epsilon = 0.000001f)
    {
        return (a - b).sqrMagnitude <= epsilon * epsilon;
    }

    private static bool Nearly(
        Quaternion a,
        Quaternion b,
        float epsilonDegrees = 0.0001f)
    {
        return Quaternion.Angle(a, b) <= epsilonDegrees;
    }

    private static bool Finite(Vector3 value)
    {
        return
            Finite(value.x) &&
            Finite(value.y) &&
            Finite(value.z);
    }

    private static bool Finite(float value)
    {
        return
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }

    private static void InitializeRuntimeAuthorities(
        RestaurantPlaceableRegistry registry,
        RestaurantPlacementTransactionService transaction,
        RestaurantPlacementHistoryService placementHistory,
        RestaurantPlaceableCreationService creation,
        RestaurantPlaceableDeletionService deletion)
    {
        RestaurantAreaRegistry areaRegistry =
            Find<RestaurantAreaRegistry>();
        RestaurantAreaAssignmentService areaAssignment =
            Find<RestaurantAreaAssignmentService>();
        RestaurantPlacementRegistry placementRegistry =
            Find<RestaurantPlacementRegistry>();
        RestaurantPlacementObstacleRegistry obstacleRegistry =
            Find<RestaurantPlacementObstacleRegistry>();
        RestaurantPlacementConstraintService constraintService =
            Find<RestaurantPlacementConstraintService>();
        RestaurantPlacementValidationService validationService =
            Find<RestaurantPlacementValidationService>();
        RestaurantPlacementLinkedGroupService linkedGroups =
            Find<RestaurantPlacementLinkedGroupService>();
        RestaurantTableRegistry tableRegistry =
            Find<RestaurantTableRegistry>();
        RestaurantSeatRegistry seatRegistry =
            Find<RestaurantSeatRegistry>();
        RestaurantSeatingTopologyService topology =
            Find<RestaurantSeatingTopologyService>();

        InvokeLifecycle(areaRegistry, "OnEnable");
        InvokeLifecycle(areaRegistry, "Start");
        InvokeLifecycle(areaAssignment, "Awake");
        InvokeLifecycle(areaAssignment, "Start");
        InvokeLifecycle(registry, "Start");
        InvokeLifecycle(tableRegistry, "Awake");
        InvokeLifecycle(tableRegistry, "Start");
        InvokeLifecycle(seatRegistry, "Awake");
        InvokeLifecycle(seatRegistry, "OnEnable");
        InvokeLifecycle(seatRegistry, "Start");
        InvokeLifecycle(topology, "Awake");
        InvokeLifecycle(topology, "OnEnable");
        topology?.RebuildImmediately();
        InvokeLifecycle(
            Find<RestaurantSeatingPlacementConstraintRule>(),
            "Awake");
        InvokeLifecycle(
            Find<RestaurantSeatingLinkedGroupProvider>(),
            "Awake");
        InvokeLifecycle(
            Find<RestaurantSeatingSnapProvider>(),
            "Awake");
        InvokeLifecycle(placementRegistry, "Awake");
        InvokeLifecycle(placementRegistry, "OnEnable");
        InvokeLifecycle(placementRegistry, "Start");
        InvokeLifecycle(obstacleRegistry, "Start");
        InvokeLifecycle(validationService, "Awake");
        InvokeLifecycle(linkedGroups, "Awake");
        InvokeLifecycle(linkedGroups, "OnEnable");
        InvokeLifecycle(linkedGroups, "Start");
        InvokeLifecycle(constraintService, "Awake");
        InvokeLifecycle(constraintService, "Start");
        InvokeLifecycle(transaction, "Awake");
        InvokeLifecycle(placementHistory, "Awake");
        InvokeLifecycle(placementHistory, "OnEnable");
        InvokeLifecycle(
            Find<RestaurantPlaceableLifecycleService>(),
            "Awake");
        InvokeLifecycle(creation, "Awake");
        InvokeLifecycle(creation, "OnEnable");
        InvokeLifecycle(deletion, "Awake");
    }

    private static void InvokeLifecycle(
        object target,
        string methodName)
    {
        if (target == null)
            return;

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.Public);

        method?.Invoke(target, null);
    }

    private static void InvokeBootstrap(Type type)
    {
        MethodInfo method = type.GetMethod(
            "Install",
            BindingFlags.Static |
            BindingFlags.NonPublic |
            BindingFlags.Public);

        if (method == null)
            throw new MissingMethodException(
                type.FullName,
                "Install");

        method.Invoke(null, null);
    }

    private static void InvokePrivate(
        object target,
        string methodName)
    {
        MethodInfo method = target?.GetType().GetMethod(
            methodName,
            BindingFlags.Instance |
            BindingFlags.NonPublic);

        if (method == null)
        {
            throw new MissingMethodException(
                target?.GetType().FullName ?? "null",
                methodName);
        }

        method.Invoke(target, null);
    }

    private static T Find<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>(
            FindObjectsInactive.Include);
    }

    private static void Check(
        bool condition,
        string label)
    {
        if (condition)
        {
            pass++;
            Lines.Add("OK - " + label);
        }
        else
        {
            fail++;
            Lines.Add("FAIL - " + label);
        }
    }

    private static bool Finish()
    {
        Lines.Add(
            "Resultado: " +
            pass +
            " OK / " +
            fail +
            " fallos.");

        string path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "EditorV2_B8_MultiSelection_Groups_Report.txt");

        File.WriteAllLines(
            path,
            Lines,
            Encoding.UTF8);

        UnityEngine.Debug.Log(
            string.Join(
                Environment.NewLine,
                Lines));

        return fail == 0;
    }
}
