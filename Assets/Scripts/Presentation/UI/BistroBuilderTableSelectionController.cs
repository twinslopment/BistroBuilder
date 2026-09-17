using System;
using System.Collections.Generic;
using BistroBuilder.CameraSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Selección de mesas durante el servicio. Presentation pura: no altera el estado de gameplay.
/// Mantiene el zoom actual y solo recentra cuando la mesa queda fuera de la zona cómoda de pantalla.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/UI/Table Selection Controller")]
public sealed class BistroBuilderTableSelectionController : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private float doubleClickSeconds = 0.34f;
    [SerializeField] private Vector2 comfortableViewportMin = new Vector2(0.27f, 0.20f);
    [SerializeField] private Vector2 comfortableViewportMax = new Vector2(0.73f, 0.80f);

    private RestaurantTable selectedTable;
    private BistroBuilderUiSceneSelectionVisual selectionVisual;
    private BistroBuilderProfessionalCameraController cameraController;
    private BistroBuilderUiShell shell;
    private RestaurantEditModeService editMode;
    private float lastSelectionClickTime = -10f;
    private RestaurantTable lastClickedTable;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>(8);

    public RestaurantTable SelectedTable => selectedTable;
    public bool HasSelection => selectedTable != null;

    public event Action<RestaurantTable> SelectionChanged;
    public event Action<RestaurantTable> TableActivated;

    private void Awake() => ResolveDependencies();

    private void OnDisable()
    {
        ClearSelection(false);
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        ResolveDependencies();

        if (IsInteractionBlocked() && selectedTable != null)
        {
            ClearSelection(true);
            return;
        }

        if (selectedTable == null && selectionVisual != null)
            selectionVisual.SetState(BistroBuilderUiSceneSelectionState.Hidden);

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && selectedTable != null)
        {
            ClearSelection(true);
            return;
        }

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (IsInteractionBlocked() || IsPointerOverUi()) return;

        Vector2 screenPoint = Mouse.current.position.ReadValue();
        if (TryResolveTableAtScreenPoint(screenPoint, out RestaurantTable table))
        {
            bool doubleClick = ReferenceEquals(table, lastClickedTable) &&
                Time.unscaledTime - lastSelectionClickTime <= Mathf.Max(0.15f, doubleClickSeconds);
            Select(table, true);
            lastClickedTable = table;
            lastSelectionClickTime = Time.unscaledTime;
            if (doubleClick) TableActivated?.Invoke(table);
            return;
        }

        lastClickedTable = null;
        ClearSelection(true);
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();
        if (worldCamera == null || shell == null)
        {
            error = "La selecci\u00F3n de mesas necesita c\u00E1mara y HUD can\u00F3nico.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool TrySelectForTest(RestaurantTable table, bool recenter = false)
    {
        if (table == null) return false;
        Select(table, recenter);
        return selectedTable == table;
    }

    public void ClearSelection(bool notify = true)
    {
        bool changed = selectedTable != null;
        if (selectedTable != null) selectedTable.StateChanged -= HandleSelectedTableStateChanged;
        selectedTable = null;
        if (selectionVisual != null) selectionVisual.SetState(BistroBuilderUiSceneSelectionState.Hidden);
        if (changed && notify) SelectionChanged?.Invoke(null);
    }

    private void Select(RestaurantTable table, bool recenter)
    {
        if (table == null) return;
        bool changed = !ReferenceEquals(selectedTable, table);
        if (changed && selectedTable != null) selectedTable.StateChanged -= HandleSelectedTableStateChanged;
        selectedTable = table;
        selectedTable.StateChanged -= HandleSelectedTableStateChanged;
        selectedTable.StateChanged += HandleSelectedTableStateChanged;
        EnsureSelectionVisual();
        selectionVisual.Configure(table.transform);
        selectionVisual.SetState(SelectionStateFor(table));
        if (recenter) RecenterIfNeeded(table);
        if (changed) SelectionChanged?.Invoke(table);
    }

    private void HandleSelectedTableStateChanged(RestaurantTable table, TableState state)
    {
        if (!ReferenceEquals(selectedTable, table)) return;
        if (selectionVisual != null) selectionVisual.SetState(SelectionStateFor(table));
        SelectionChanged?.Invoke(table);
    }

    private bool TryResolveTableAtScreenPoint(Vector2 screenPoint, out RestaurantTable table)
    {
        table = null;
        if (worldCamera == null) return false;
        Ray ray = worldCamera.ScreenPointToRay(screenPoint);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            RestaurantTable candidate = collider != null ? collider.GetComponentInParent<RestaurantTable>() : null;
            if (candidate == null || hits[i].distance >= bestDistance) continue;
            table = candidate;
            bestDistance = hits[i].distance;
        }
        return table != null;
    }

    private bool IsInteractionBlocked()
    {
        if (editMode != null && editMode.IsEditModeActive) return true;
        return shell != null && shell.HasManagementScreenOpen;
    }

    private bool IsPointerOverUi()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;
        uiHits.Clear();
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        EventSystem.current.RaycastAll(pointer, uiHits);
        return uiHits.Count > 0;
    }

    private void RecenterIfNeeded(RestaurantTable table)
    {
        if (worldCamera == null || table == null) return;
        Vector3 focus = ResolveVisualCenter(table.transform);
        Vector3 viewport = worldCamera.WorldToViewportPoint(focus);
        bool comfortable = viewport.z > 0f &&
            viewport.x >= comfortableViewportMin.x && viewport.x <= comfortableViewportMax.x &&
            viewport.y >= comfortableViewportMin.y && viewport.y <= comfortableViewportMax.y;
        if (comfortable) return;

        if (cameraController == null)
            cameraController = worldCamera.GetComponent<BistroBuilderProfessionalCameraController>();
        if (cameraController == null) return;

        // SetFocusPoint preserves yaw, pitch and distance: no automatic zoom.
        Vector3 currentFocus = cameraController.TargetState.FocusPoint;
        cameraController.SetFocusPoint(new Vector3(focus.x, currentFocus.y, focus.z), false);
    }

    private static Vector3 ResolveVisualCenter(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return target.position;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            if (renderers[i] != null) bounds.Encapsulate(renderers[i].bounds);
        return bounds.center;
    }

    private void EnsureSelectionVisual()
    {
        if (selectionVisual != null) return;
        GameObject host = new GameObject("BB_UIUX_TableSelectionVisual");
        host.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        selectionVisual = host.AddComponent<BistroBuilderUiSceneSelectionVisual>();
    }

    private static BistroBuilderUiSceneSelectionState SelectionStateFor(RestaurantTable table)
    {
        if (table == null) return BistroBuilderUiSceneSelectionState.Hidden;
        switch (table.CurrentState)
        {
            case TableState.Dirty:
                return BistroBuilderUiSceneSelectionState.Critical;
            case TableState.WaitingForWaiter:
            case TableState.WaitingForFood:
            case TableState.WaitingForBill:
                return BistroBuilderUiSceneSelectionState.Attention;
            default:
                return BistroBuilderUiSceneSelectionState.Selected;
        }
    }

    private void ResolveDependencies()
    {
        if (shell == null) shell = GetComponent<BistroBuilderUiShell>();
        if (editMode == null) editMode = FindFirstObjectByType<RestaurantEditModeService>(FindObjectsInactive.Include);
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) worldCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
        if (cameraController == null && worldCamera != null)
            cameraController = worldCamera.GetComponent<BistroBuilderProfessionalCameraController>();
    }
}
