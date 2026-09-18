using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum BistroBuilderPointerKind { Normal, Hover, Blocked, Drag, Rotate }

/// <summary>One cursor owner. Reads existing interaction results; never runs placement validation.</summary>
[DefaultExecutionOrder(900)]
public sealed class BistroBuilderPointerFeedback : MonoBehaviour
{
    public static BistroBuilderPointerFeedback Instance { get; private set; }
    public static bool KeyboardFocus { get; private set; }
    public BistroBuilderPointerKind Current { get; private set; }
    private readonly Texture2D[] textures = new Texture2D[5];
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>(32);
    private PointerEventData pointer;
    private EventSystem pointerOwner;
    private RestaurantEditModeService edit;
    private RestaurantEditInteractionController furniture;
    private BistroBuilderConstructionAuthoringRuntimeTool construction;
    private Camera worldCamera;
    private BistroBuilderUiShell shell;
    private float rotateUntil;
    private bool cursorApplied;
    private LineRenderer hoverOutline, selectionOutline;
    private Material outlineMaterial;
    private readonly Vector3[] corners = new Vector3[5];
    private RestaurantEditableObject lastHover, lastSelection;
    private Renderer[] hoverRenderers, selectionRenderers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Instance == null) new GameObject("Bistro Pointer Feedback").AddComponent<BistroBuilderPointerFeedback>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        for (int i = 0; i < textures.Length; i++) textures[i] = Resources.Load<Texture2D>("BistroBuilder/UI/Cursors/" + (BistroBuilderPointerKind)i);
        SceneManager.sceneLoaded += SceneLoaded;
    }
    // AfterSceneLoad bootstraps have no ordering guarantee; Start sees all installed tools.
    private void Start() => ResolveScene();
    private void SceneLoaded(Scene scene, LoadSceneMode mode) => ResolveScene();
    private void ResolveScene()
    {
        if (hoverOutline != null) Destroy(hoverOutline.gameObject);
        if (selectionOutline != null) Destroy(selectionOutline.gameObject);
        edit = FindFirstObjectByType<RestaurantEditModeService>();
        furniture = FindFirstObjectByType<RestaurantEditInteractionController>();
        construction = FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
        shell = FindFirstObjectByType<BistroBuilderUiShell>(); worldCamera = Camera.main;
        if (worldCamera == null) worldCamera = FindFirstObjectByType<Camera>();
        if (outlineMaterial == null) outlineMaterial = new Material(Shader.Find("Sprites/Default"));
        hoverOutline = CreateOutline("Hover footprint"); selectionOutline = CreateOutline("Selected footprint");
    }
    private LineRenderer CreateOutline(string name)
    {
        var go = new GameObject(name); go.transform.SetParent(transform);
        var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = outlineMaterial;
        line.useWorldSpace = true; line.positionCount = 5; line.widthMultiplier = 0.035f;
        line.numCornerVertices = 4; line.enabled = false; return line;
    }
    private void Update()
    {
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb != null && (kb.tabKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)) KeyboardFocus = true;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) KeyboardFocus = false;
        if (kb?.tabKey.wasPressedThisFrame == true) MoveKeyboardFocus(kb.shiftKey.isPressed);
    }
    public static void MoveKeyboardFocus(bool reverse)
    {
        KeyboardFocus = true;
        AdvanceFocus(reverse);
    }
    private static void AdvanceFocus(bool reverse)
    {
        if (EventSystem.current == null) return;
        var controls = Selectable.allSelectablesArray;
        int current = -1;
        for (int i = 0; i < controls.Length; i++) if (controls[i].gameObject == EventSystem.current.currentSelectedGameObject) current = i;
        if (current < 0 && reverse) current = 0;
        for (int step = 1; step <= controls.Length; step++)
        {
            int index = (current + (reverse ? -step : step) + controls.Length * 2) % controls.Length;
            var target = controls[index];
            if (!target.IsActive() || !target.IsInteractable() || target.navigation.mode == Navigation.Mode.None) continue;
            // Hidden legacy launchers use CanvasGroup rather than disabling their GameObjects.
            bool visible = true;
            foreach (var group in target.GetComponentsInParent<CanvasGroup>()) if (group.alpha < 0.01f || !group.interactable) visible = false;
            if (!visible) continue;
            EventSystem.current.SetSelectedGameObject(target.gameObject); break;
        }
    }
    private void LateUpdate()
    {
        if (Mouse.current == null || !Application.isFocused) return;
        var position = Mouse.current.position.ReadValue();
        bool overUi = false; Selectable control = null;
        if (EventSystem.current != null)
        {
            if (pointer == null || pointerOwner != EventSystem.current) { pointerOwner = EventSystem.current; pointer = new PointerEventData(pointerOwner); }
            pointer.position = position; uiHits.Clear(); EventSystem.current.RaycastAll(pointer, uiHits);
            foreach (var hit in uiHits)
            {
                if (!(hit.module is GraphicRaycaster)) continue;
                overUi = true; control = hit.gameObject.GetComponentInParent<Selectable>(); break;
            }
        }
        BistroBuilderPointerKind kind = BistroBuilderPointerKind.Normal;
        RestaurantEditableObject hovered = null;
        if (control != null)
        {
            kind = control.IsInteractable() ? BistroBuilderPointerKind.Hover : BistroBuilderPointerKind.Blocked;
            var hint = control.GetComponent<BistroBuilderPointerHint>();
            if (control.IsInteractable() && hint != null) kind = hint.Kind;
        }
        else if (!overUi && !BistroBuilderRuntimePointerUiGuard.IsPointerBlocked(position) && edit != null && edit.IsEditModeActive && !(shell != null && shell.HasManagementScreenOpen))
        {
            if (furniture != null && furniture.HasActivePlacement)
            {
                if (furniture.LastRotationFeedbackTime > 0 && Time.unscaledTime - furniture.LastRotationFeedbackTime < 0.45f) rotateUntil = furniture.LastRotationFeedbackTime + 0.45f;
                kind = ResolvePlacementCursor(furniture.LastValidationResult.IsValid, Time.unscaledTime < rotateUntil);
            }
            else if (construction != null && construction.Mode != BistroBuilderConstructionRuntimeMode.Furniture && construction.HasActiveGesture)
                kind = construction.IsPreviewBlocked ? BistroBuilderPointerKind.Blocked : BistroBuilderPointerKind.Drag;
            else if (construction != null && construction.IsPreviewBlocked && construction.Mode != BistroBuilderConstructionRuntimeMode.Furniture)
                kind = BistroBuilderPointerKind.Blocked;
            else if (construction != null && construction.Mode != BistroBuilderConstructionRuntimeMode.Furniture && construction.LastRotationFeedbackTime > 0 && Time.unscaledTime - construction.LastRotationFeedbackTime < 0.45f)
                kind = BistroBuilderPointerKind.Rotate;
            else if (worldCamera != null && Physics.Raycast(worldCamera.ScreenPointToRay(position), out var hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
            {
                hovered = hit.collider.GetComponentInParent<RestaurantEditableObject>();
                if (hovered != null) kind = hovered.EditingEnabled ? BistroBuilderPointerKind.Hover : BistroBuilderPointerKind.Blocked;
            }
        }
        SetCursor(kind);
        bool editing = edit != null && edit.IsEditModeActive;
        UpdateOutline(hoverOutline, editing ? hovered : null, ref lastHover, ref hoverRenderers, new Color(0.82f, 0.64f, 0.28f));
        UpdateOutline(selectionOutline, editing && furniture != null ? furniture.SelectedEditableObject : null, ref lastSelection, ref selectionRenderers, new Color(0.46f, 0.66f, 0.30f));
    }
    public static BistroBuilderPointerKind ResolvePlacementCursor(bool valid, bool rotating) => !valid ? BistroBuilderPointerKind.Blocked : rotating ? BistroBuilderPointerKind.Rotate : BistroBuilderPointerKind.Drag;
    private void SetCursor(BistroBuilderPointerKind kind)
    {
        if (cursorApplied && Current == kind) return;
        Current = kind; cursorApplied = true;
        var hotspot = kind == BistroBuilderPointerKind.Drag || kind == BistroBuilderPointerKind.Rotate ? new Vector2(32, 32) : new Vector2(8, 5);
        Cursor.SetCursor(textures[(int)kind], hotspot, CursorMode.Auto);
    }
    private void UpdateOutline(LineRenderer line, RestaurantEditableObject target, ref RestaurantEditableObject previous, ref Renderer[] renderers, Color tint)
    {
        if (line == null) return;
        if (previous != target) { previous = target; renderers = target != null ? target.GetComponentsInChildren<Renderer>() : null; }
        line.enabled = target != null && target.gameObject.activeInHierarchy && renderers != null && renderers.Length > 0;
        if (!line.enabled) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) if (renderers[i] != null) bounds.Encapsulate(renderers[i].bounds);
        float y = bounds.min.y + 0.035f;
        corners[0] = new Vector3(bounds.min.x, y, bounds.min.z); corners[1] = new Vector3(bounds.min.x, y, bounds.max.z);
        corners[2] = new Vector3(bounds.max.x, y, bounds.max.z); corners[3] = new Vector3(bounds.max.x, y, bounds.min.z); corners[4] = corners[0];
        line.startColor = line.endColor = tint; line.SetPositions(corners);
    }
    private void OnApplicationFocus(bool focused) { cursorApplied = false; if (!focused) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); }
    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= SceneLoaded; Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        if (outlineMaterial != null) Destroy(outlineMaterial); Instance = null; KeyboardFocus = false;
    }
}
