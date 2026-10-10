using System.Collections.Generic;
using BistroBuilder.CameraSystem;
using UnityEngine;

/// <summary>
/// B13 camera/obstruction bridge. Keeps 369A as the ONLY camera mover.
/// 369C owns the per-mode camera memories and 369B owns top-down presets.
/// Renderers are presentation-only: geometry, physics and save state never change.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderEditorV2CameraVisibilityContext : MonoBehaviour
{
    [SerializeField] private RestaurantEditModeService editMode;
    [SerializeField] private BistroBuilderCameraInspectionService inspection;
    [SerializeField] private BistroBuilderCameraViewService views;

    private readonly Dictionary<Renderer, bool> originalRendererStates =
        new Dictionary<Renderer, bool>(32);
    private bool subscribed;
    private bool pendingCameraSync;
    // Explicit, on-demand ray query only. Saturation fails closed instead
    // of silently omitting a nearer obstruction. No per-frame allocations.
    private readonly RaycastHit[] occlusionHits = new RaycastHit[128];
    private readonly List<Renderer> authorizedWallVisuals = new List<Renderer>(64);

    public int HiddenRendererCount => originalRendererStates.Count;
    public bool IsEditCameraContext =>
        inspection != null &&
        inspection.CurrentMode == BistroBuilderCameraContextMode.Edit;
    public bool HasCameraAuthority => inspection != null &&
        inspection.Controller != null;

    public void Configure(
        RestaurantEditModeService mode,
        BistroBuilderCameraInspectionService cameraInspection,
        BistroBuilderCameraViewService cameraViews)
    {
        Unsubscribe();
        RestoreAllObstructions();
        editMode = mode;
        inspection = cameraInspection;
        views = cameraViews;
        Subscribe();
        pendingCameraSync = editMode != null && editMode.IsEditModeActive;
        SyncCameraContext();
    }

    private void OnEnable()
    {
        Subscribe();
        pendingCameraSync = editMode != null && editMode.IsEditModeActive;
        SyncCameraContext();
    }

    private void OnDisable()
    {
        RestoreAllObstructions();
        Unsubscribe();
        pendingCameraSync = false;
    }

    private void OnDestroy() => RestoreAllObstructions();

    private void LateUpdate()
    {
        if (pendingCameraSync)
            SyncCameraContext();
    }

    private void Subscribe()
    {
        if (subscribed || editMode == null) return;
        editMode.EditModeEntered += EnteredEditMode;
        editMode.EditModeExited += ExitedEditMode;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || editMode == null) return;
        editMode.EditModeEntered -= EnteredEditMode;
        editMode.EditModeExited -= ExitedEditMode;
        subscribed = false;
    }

    private void EnteredEditMode()
    {
        pendingCameraSync = true;
        SyncCameraContext();
    }

    private void ExitedEditMode()
    {
        RestoreAllObstructions();
        pendingCameraSync = true;
        SyncCameraContext();
    }

    private void SyncCameraContext()
    {
        if (!pendingCameraSync || editMode == null || inspection == null ||
            inspection.Controller == null ||
            !inspection.Controller.IsInitialized)
            return;
        bool editing = editMode.IsEditModeActive;
        BistroBuilderCameraContextMode expected =
            editing ? BistroBuilderCameraContextMode.Edit :
                BistroBuilderCameraContextMode.Service;
        if (inspection.CurrentMode == expected)
        {
            pendingCameraSync = false;
            return;
        }
        // Preset pitch policy belongs to 369B. Remove it before 369C
        // restores the camera memory for the other operating context.
        if (views != null &&
            views.ActiveView != BistroBuilderCameraViewId.None)
            views.ExitPresetKeepingCurrentView();
        pendingCameraSync = !inspection.TrySwitchMode(expected, true, false);
    }

    /// <summary>Use the canonical 369B precision top-down view, without
    /// taking ownership of the camera or replacing 369C edit memory.</summary>
    public bool TryEnterPrecisionTopDown(bool immediate = false)
    {
        if (editMode == null || !editMode.IsEditModeActive ||
            inspection == null || views == null)
            return false;
        SyncCameraContext();
        return IsEditCameraContext &&
            views.TryActivateView(BistroBuilderCameraViewId.TopDown, immediate);
    }

    public bool TryRestorePreviousFreeView(bool immediate = false)
    {
        if (editMode == null || !editMode.IsEditModeActive ||
            views == null || !views.HasPreviousFreeState)
            return false;
        return views.TryRestorePreviousView(immediate);
    }

    /// <summary>
    /// Discovers only explicitly authorized, visible renderers whose OWN
    /// collider intersects the camera-to-target segment. This is a read-only
    /// proposal: it does not hide, fade, mutate colliders or move the camera.
    /// The caller owns the candidate whitelist and any subsequent UI decision.
    /// Geometry transforms should be synchronized by the owning placement
    /// authority, not by this query (no global Physics.SyncTransforms).
    /// False means unsafe/unavailable input, and always clears the output.
    /// </summary>
    public bool TryFindOccludingCandidates(
        Vector3 cameraPosition,
        Vector3 targetPosition,
        IReadOnlyList<Renderer> allowedCandidates,
        List<Renderer> results)
    {
        if (results == null) return false;
        results.Clear();
        if (!isActiveAndEnabled || editMode == null ||
            !editMode.IsEditModeActive || !IsEditCameraContext ||
            !HasCameraAuthority || allowedCandidates == null ||
            !IsFinite(cameraPosition) || !IsFinite(targetPosition))
            return false;

        Vector3 path = targetPosition - cameraPosition;
        float distance = path.magnitude;
        if (distance < 0.05f || float.IsInfinity(distance)) return false;
        int hitCount = Physics.RaycastNonAlloc(
            new Ray(cameraPosition, path / distance), occlusionHits,
            distance - 0.02f, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        // Never trust a truncated raycast result: this would silently miss
        // physical occluders in crowded/complex architecture.
        if (hitCount >= occlusionHits.Length) return false;

        for (int index = 0; index < allowedCandidates.Count; index++)
        {
            Renderer candidate = allowedCandidates[index];
            if (candidate == null || !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy) continue;
            bool obstructs = false;
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider obstacle = occlusionHits[hitIndex].collider;
                if (obstacle == null) continue;
                Transform hitTransform = obstacle.transform;
                // An explicit wall renderer may own several CHILD colliders.
                // Never match a broad ancestor collider (e.g. a whole building).
                if (hitTransform == candidate.transform ||
                    hitTransform.IsChildOf(candidate.transform))
                {
                    obstructs = true;
                    break;
                }
            }
            if (obstructs && !results.Contains(candidate))
                results.Add(candidate);
        }
        return true;
    }

    /// <summary>
    /// Conservatively discovers the materializer's real, canonical wall
    /// renderers blocking an edit sightline. Returns proposals only; no fade
    /// or hiding occurs. Floors, furniture and temporary previews are excluded
    /// because only generated wall roots enter the allowlist.
    /// </summary>
    public bool TryFindOccludingArchitectureWalls(
        Vector3 cameraPosition,
        Vector3 targetPosition,
        BistroBuilderArchitectureRuntimeMaterializer materializer,
        List<Renderer> results)
    {
        if (results == null) return false;
        results.Clear();
        if (materializer == null) return false;
        materializer.CollectWallOcclusionCandidates(authorizedWallVisuals);
        return TryFindOccludingCandidates(
            cameraPosition, targetPosition, authorizedWallVisuals, results);
    }

    private static bool IsFinite(Vector3 point) =>
        !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
        !float.IsNaN(point.y) && !float.IsInfinity(point.y) &&
        !float.IsNaN(point.z) && !float.IsInfinity(point.z);

    /// <summary>
    /// Edit-only visual obstruction, with exact original enabled state.
    /// Collision, Navigation, selection colliders and scene objects remain.
    /// </summary>
    public bool TryHideObstruction(Renderer visual)
    {
        if (editMode == null || !editMode.IsEditModeActive ||
            visual == null || !isActiveAndEnabled)
            return false;
        if (!originalRendererStates.ContainsKey(visual))
            originalRendererStates.Add(visual, visual.enabled);
        visual.enabled = false;
        return true;
    }

    public bool TryRestoreObstruction(Renderer visual)
    {
        if (visual == null ||
            !originalRendererStates.TryGetValue(visual, out bool oldEnabled))
            return false;
        visual.enabled = oldEnabled;
        originalRendererStates.Remove(visual);
        return true;
    }

    public void RestoreAllObstructions()
    {
        if (originalRendererStates.Count == 0) return;
        foreach (var pair in originalRendererStates)
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        originalRendererStates.Clear();
    }
}
