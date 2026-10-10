using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BistroBuilder.CameraSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>B13 real scene acceptance, isolated GameObjects, no scene writes.</summary>
[InitializeOnLoad]
public static class BistroBuilderEditorV2B13CameraRuntimeSelfTest
{
    private const string Stage = "BB.B13.Stage";
    private const string Passed = "BB.B13.Passed";
    private const string Report = "EditorV2_B13_Camera_Report.txt";
    static BistroBuilderEditorV2B13CameraRuntimeSelfTest()
    {
        EditorApplication.playModeStateChanged -= OnChanged;
        EditorApplication.playModeStateChanged += OnChanged;
    }
    [MenuItem("Bistro Builder/QA/Editor V2/B13 Camera Context Runtime")]
    public static void RunFromMenu() => Begin(false);
    public static void RunFromCommandLine() => Begin(true);
    private static void Begin(bool cli)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Unity already playing.");
        File.WriteAllText(Path.GetFullPath(Report), "B13 running");
        SessionState.SetString(Stage, cli ? "enter_cli" : "enter_menu");
        SessionState.SetBool(Passed, false);
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity",
            OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    private static void OnChanged(PlayModeStateChange state)
    {
        var stage = SessionState.GetString(Stage, "");
        if (stage.Length == 0) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("__B13CameraQA").AddComponent<BistroBuilderEditorV2B13CameraDriver>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(Stage);
            if (stage.EndsWith("cli", StringComparison.Ordinal))
                EditorApplication.Exit(SessionState.GetBool(Passed, false) ? 0 : 1);
        }
    }
    public static void End(bool success, string detail)
    {
        string stage = SessionState.GetString(Stage, "");
        File.WriteAllText(Path.GetFullPath(Report),
            "B13 camera/visibility actual Play Mode\n" +
            (success ? "PASS " : "FAIL ") + detail);
        SessionState.SetBool(Passed, success);
        SessionState.SetString(Stage,
            stage.EndsWith("cli", StringComparison.Ordinal) ? "exit_cli" : "exit_menu");
        if (success) Debug.Log("[B13] PASS " + detail);
        else Debug.LogError("[B13] FAIL " + detail);
        EditorApplication.ExitPlaymode();
    }
}

public sealed class BistroBuilderEditorV2B13CameraDriver : MonoBehaviour
{
    private IEnumerator Start()
    {
        for (int i = 0; i < 20; i++) yield return null;
        var mode = Object.FindFirstObjectByType<RestaurantEditModeService>(
            FindObjectsInactive.Include);
        var context = Object.FindFirstObjectByType<
            BistroBuilderEditorV2CameraVisibilityContext>(FindObjectsInactive.Include);
        var camera = Object.FindFirstObjectByType<
            BistroBuilderCameraInspectionService>(FindObjectsInactive.Include);
        var views = Object.FindFirstObjectByType<
            BistroBuilderCameraViewService>(FindObjectsInactive.Include);
        var controllers = Object.FindObjectsByType<
            BistroBuilderProfessionalCameraController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (mode == null || context == null || camera == null ||
            views == null || !context.HasCameraAuthority ||
            controllers.Length != 1 || !controllers[0].IsInitialized)
        {
            BistroBuilderEditorV2B13CameraRuntimeSelfTest.End(false,
                "Missing canonical 369A/369B/369C or B13 bridge; cameras=" +
                controllers.Length + " mode=" + (mode != null) +
                " context=" + (context != null));
            yield break;
        }
        var before = controllers[0].CurrentState;
        var wallGo = new GameObject("__B13QAWallRenderer");
        wallGo.transform.position = new Vector3(200f, 40f, 200f);
        var wall = wallGo.AddComponent<MeshRenderer>();
        var wallCollider = wallGo.AddComponent<BoxCollider>();
        wallCollider.size = Vector3.one * 2f;
        var offAxisGo = new GameObject("__B13OffAxis");
        offAxisGo.transform.position = new Vector3(200f, 40f, 207f);
        var offAxis = offAxisGo.AddComponent<MeshRenderer>();
        offAxisGo.AddComponent<BoxCollider>();
        var triggerGo = new GameObject("__B13TriggerNotObstacle");
        triggerGo.transform.position = new Vector3(205f, 40f, 200f);
        var triggerVisual = triggerGo.AddComponent<MeshRenderer>();
        var triggerCollider = triggerGo.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        var behindGo = new GameObject("__B13BehindTarget");
        behindGo.transform.position = new Vector3(230f, 40f, 200f);
        var behind = behindGo.AddComponent<MeshRenderer>();
        behindGo.AddComponent<BoxCollider>();
        var rayStart = new Vector3(190f, 40f, 200f);
        var rayTarget = new Vector3(220f, 40f, 200f);
        var allowedVisuals = new Renderer[] {
            wall, offAxis, triggerVisual, behind, wall
        };
        var detected = new List<Renderer>(8);
        detected.Add(wall);
        bool beforeEditOcclusionGuard =
            !context.TryFindOccludingCandidates(
                rayStart, rayTarget, allowedVisuals, detected) &&
            detected.Count == 0;
        var disabledGo = new GameObject("__B13QADisabledRenderer");
        var disabled = disabledGo.AddComponent<MeshRenderer>();
        disabled.enabled = false;
        bool outsideGuard = !context.TryHideObstruction(wall);
        bool entered = mode.IsEditModeActive ||
            mode.TryEnterEditMode(out _, out _);
        yield return null;
        bool memoryEdit = context.IsEditCameraContext &&
            camera.CurrentMode == BistroBuilderCameraContextMode.Edit;
        Physics.SyncTransforms(); // QA objects moved explicitly this frame.
        bool obstructionFound = context.TryFindOccludingCandidates(
            rayStart, rayTarget, allowedVisuals, detected) &&
            detected.Count == 1 && detected[0] == wall &&
            wall.enabled && wallCollider.enabled &&
            triggerCollider.isTrigger && context.HiddenRendererCount == 0;
        bool strictWhitelist = context.TryFindOccludingCandidates(
            rayStart, rayTarget, new Renderer[] { offAxis }, detected) &&
            detected.Count == 0;
        // Stress the fixed 128-ray-hit buffer. A saturated query MUST
        // fail closed rather than returning a misleading partial list.
        var crowdedRoot = new GameObject("__B13QACrowdedPhysics");
        for (int index = 0; index < 140; index++)
        {
            var node = new GameObject("Occluder_" + index);
            node.transform.SetParent(crowdedRoot.transform, false);
            node.transform.position = new Vector3(
                194f + index * 0.07f, 40f, 200f);
            var collider = node.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.05f;
        }
        Physics.SyncTransforms();
        detected.Add(wall);
        bool saturationFailClosed = !context.TryFindOccludingCandidates(
            rayStart, rayTarget, allowedVisuals, detected) &&
            detected.Count == 0 && wall.enabled;
        Destroy(crowdedRoot);
        detected.Add(wall);
        bool badRayRejected = !context.TryFindOccludingCandidates(
            new Vector3(float.NaN, 0f, 0f), rayTarget,
            allowedVisuals, detected) && detected.Count == 0;
        bool idempotent = entered && context.TryHideObstruction(wall) &&
            context.TryHideObstruction(wall) && !wall.enabled &&
            context.HiddenRendererCount == 1;
        bool disabledKept = context.TryHideObstruction(disabled) &&
            !disabled.enabled && context.HiddenRendererCount == 2;
        bool selectedRestore = context.TryRestoreObstruction(disabled) &&
            !disabled.enabled && context.HiddenRendererCount == 1;
        bool precision = context.TryEnterPrecisionTopDown() &&
            views.ActiveView == BistroBuilderCameraViewId.TopDown;
        bool free = precision && context.TryRestorePreviousFreeView() &&
            views.ActiveView == BistroBuilderCameraViewId.None;
        bool exited = entered && mode.TryExitEditMode(true, out _);
        yield return null;
        bool cleaned = wall.enabled &&
            !disabled.enabled && context.HiddenRendererCount == 0 &&
            !context.IsEditCameraContext;
        var serviceState = controllers[0].TargetState;
        bool remembered = before.IsFinite && serviceState.IsFinite &&
            Vector3.Distance(before.FocusPoint, serviceState.FocusPoint) < 0.75f;
        bool afterExitGuard = !context.TryHideObstruction(wall);
        detected.Add(wall);
        bool afterExitQueryGuard = !context.TryFindOccludingCandidates(
            rayStart, rayTarget, allowedVisuals, detected) &&
            detected.Count == 0;
        // Adversarial: leave edit mode WHILE TopDown remains active.
        // This must clear pitch override and restore hidden visuals.
        bool secondEntered = mode.TryEnterEditMode(out _, out _);
        yield return null;
        bool secondTopDown = secondEntered &&
            context.TryEnterPrecisionTopDown() &&
            views.ActiveView == BistroBuilderCameraViewId.TopDown;
        bool secondHidden = context.TryHideObstruction(wall) &&
            !wall.enabled;
        bool secondExited = mode.TryExitEditMode(true, out _);
        yield return null;
        bool directReturnSafe = secondTopDown && secondHidden &&
            secondExited && wall.enabled && context.HiddenRendererCount == 0 &&
            camera.CurrentMode == BistroBuilderCameraContextMode.Service &&
            views.ActiveView == BistroBuilderCameraViewId.None &&
            !controllers[0].ExternalPitchRangeActive;
        // Ensure no mutated user scene or stray mesh component survives QA.
        Destroy(wallGo);
        Destroy(disabledGo);
        Destroy(offAxisGo);
        Destroy(triggerGo);
        Destroy(behindGo);
        BistroBuilderEditorV2B13CameraRuntimeSelfTest.End(
            outsideGuard && entered && memoryEdit &&
            beforeEditOcclusionGuard && obstructionFound &&
            strictWhitelist && saturationFailClosed &&
            badRayRejected && afterExitQueryGuard &&
            idempotent &&
            disabledKept && selectedRestore && precision && free &&
            exited && cleaned && remembered && afterExitGuard &&
            directReturnSafe,
            "guard=" + outsideGuard + " entered=" + entered +
            " contextEdit=" + memoryEdit +
            " queryOutsideGuard=" + beforeEditOcclusionGuard +
            " obstructionExact=" + obstructionFound +
            " whitelist=" + strictWhitelist +
            " saturationFailClosed=" + saturationFailClosed +
            " invalidRayFailClosed=" + badRayRejected +
            " queryAfterExitGuard=" + afterExitQueryGuard +
            " rendererIdempotent=" + idempotent +
            " disabledOriginal=" + disabledKept + " individualRestore=" +
            selectedRestore + " topDown=" + precision + " freeView=" + free +
            " exited=" + exited + " restoredVisuals=" + cleaned +
            " cameraMemory=" + remembered +
            " postExitGuard=" + afterExitGuard +
            " exitDuringTopDown=" + directReturnSafe +
            " controllers=" + controllers.Length);
    }
}
