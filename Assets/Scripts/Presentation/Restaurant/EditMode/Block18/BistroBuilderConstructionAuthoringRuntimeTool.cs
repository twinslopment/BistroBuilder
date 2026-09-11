using System;
using BistroBuilder.ConstructionAuthoring;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public enum BistroBuilderConstructionRuntimeMode
{
    Furniture = 0,
    Select = 1,
    Wall = 2,
    Room = 3,
    Door = 4,
    Window = 5
}

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/18N Construction Authoring Runtime Tool")]
public sealed class BistroBuilderConstructionAuthoringRuntimeTool : MonoBehaviour
{
    [SerializeField] private BistroBuilderEditRuntimeCoordinator coordinator;
    [SerializeField] private RestaurantEditModeService editModeService;
    [SerializeField] private RestaurantEditInteractionController furnitureController;
    [SerializeField] private Camera interactionCamera;

    [Header("Construction V1")]
    [SerializeField, Min(0.05f)] private float wallThickness = 0.12f;
    [SerializeField, Min(1f)] private float wallHeight = 2.8f;
    [SerializeField, Min(0.25f)] private float minimumRoomSide = 1f;
    [SerializeField, Min(0.05f)] private float selectionRadius = 0.22f;
    [SerializeField, Min(0.05f)] private float handleRadius = 0.28f;
    [SerializeField] private bool showPlaytestPanel = true;

    private readonly ArchitectureQueryCache queries = new ArchitectureQueryCache();
    private readonly ArchitectureSnapService snapService = new ArchitectureSnapService();
    private readonly SnapSettings snapSettings = new SnapSettings();
    private readonly ArchitectureSelection selection = new ArchitectureSelection();
    private ConstructionGesture gesture = new ConstructionGesture();
    private ConstructionDefinitionCatalog definitions;
    private BistroBuilderConstructionRuntimeMode mode = BistroBuilderConstructionRuntimeMode.Furniture;
    private string zoneDefinitionId = "zone.dining";
    private string status = "Modo mobiliario activo.";
    private Vector2 gestureAnchor;
    private bool twoClickGesture;
    private bool dragGesture;
    private bool furnitureControllerWasEnabled;
    private bool furnitureInputSuspended;
    private bool observedEditModeActive;
    private SnapKind lastSnapKind;
    private Rect panelRect;
    private GameObject visualRoot;
    private LineRenderer[] previewLines = Array.Empty<LineRenderer>();
    private LineRenderer[] draftLines = Array.Empty<LineRenderer>();
    private LineRenderer selectionLine;
    private LineRenderer snapMarker;
    private Material lineMaterial;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle statusStyle;

    private static readonly Color ValidColor = new Color(0.2f, 0.9f, 0.35f, 0.95f);
    private static readonly Color InvalidColor = new Color(0.95f, 0.25f, 0.2f, 0.95f);
    private static readonly Color SelectionColor = new Color(1f, 0.78f, 0.15f, 1f);
    private static readonly Color SnapColor = new Color(0.2f, 0.8f, 1f, 1f);

    public BistroBuilderConstructionRuntimeMode Mode => mode;
    public string StatusMessage => status;
    public EntityKind SelectedKind => selection.Kind;
    public BistroBuilderEditId SelectedId => selection.Id;
    public bool HasActiveGesture => IsGestureActive();
    public bool HasDraftSession => coordinator != null && coordinator.HasSession;
    public bool HasDraftChanges => coordinator != null && coordinator.IsDirty;
    public bool CanUndo => coordinator != null && coordinator.CanUndo;
    public bool CanRedo => coordinator != null && coordinator.CanRedo;

    private void Awake()
    {
        CacheDependencies();
        ResolveDefinitions();
        EnsureVisuals();
    }
    private void OnEnable()
    {
        CacheDependencies();
        ResolveDefinitions();
        EnsureVisuals();
    }

    private void OnDisable()
    {
        CancelGesture(string.Empty);
        RestoreFurnitureInput();
        SetVisualsVisible(false);
    }

    private void Update()
    {
        CacheDependencies();
        if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            showPlaytestPanel = !showPlaytestPanel;

        if (editModeService == null || !editModeService.IsEditModeActive)
        {
            if (observedEditModeActive && coordinator != null && coordinator.HasSession)
                TryCancelDraft(out _);
            observedEditModeActive = false;
            if (mode != BistroBuilderConstructionRuntimeMode.Furniture)
                SetMode(BistroBuilderConstructionRuntimeMode.Furniture);
            return;
        }
        observedEditModeActive = true;

        if (HandleHistoryShortcuts()) return;
        if (HandleEscapeOrDelete()) return;

        if (mode == BistroBuilderConstructionRuntimeMode.Furniture || Mouse.current == null)
            return;
        if (!EnsureSession(out string sessionError))
        {
            SetStatus(sessionError);
            return;
        }
        RefreshQueries();
        if (!TryGetPlanPoint(out Vector2 rawPoint)) return;
        UpdateHoverOrGesture(rawPoint);
        if (Mouse.current.leftButton.wasPressedThisFrame && !PointerHitsUi())
            HandlePointerPressed(rawPoint);
        if (Mouse.current.leftButton.wasReleasedThisFrame && dragGesture)
            FinishDragGesture();
    }

    private bool HandleEscapeOrDelete()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;
        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (IsGestureActive()) CancelGesture("Operación cancelada. La herramienta sigue activa.");
            else if (selection.Kind != EntityKind.None) { selection.Clear(); RefreshVisuals(); }
            else SetMode(BistroBuilderConstructionRuntimeMode.Furniture);
            return true;
        }
        if ((kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) &&
            selection.Kind != EntityKind.None)
        {
            TryDeleteSelection(out _);
            return true;
        }
        return false;
    }

    private bool HandleHistoryShortcuts()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || coordinator == null || !coordinator.HasSession) return false;
        bool control = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
        bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        if (control && (kb.yKey.wasPressedThisFrame || (shift && kb.zKey.wasPressedThisFrame)))
        {
            if (coordinator.TryRedo(out string error)) RefreshAfterDraftMutation("Cambio rehecho.");
            else if (!string.IsNullOrWhiteSpace(error)) SetStatus(error);
            return true;
        }
        if (control && kb.zKey.wasPressedThisFrame)
        {
            if (coordinator.TryUndo(out string error)) RefreshAfterDraftMutation("Cambio deshecho.");
            else if (!string.IsNullOrWhiteSpace(error)) SetStatus(error);
            return true;
        }
        return false;
    }
    public void SetMode(BistroBuilderConstructionRuntimeMode next)
    {
        CacheDependencies();
        if (next != BistroBuilderConstructionRuntimeMode.Furniture &&
            (editModeService == null || !editModeService.IsEditModeActive))
        {
            SetStatus("Activa el modo edición antes de construir.");
            return;
        }
        if (mode == next && !IsGestureActive()) return;
        CancelGesture(string.Empty);
        mode = next;
        selection.Clear();
        if (mode == BistroBuilderConstructionRuntimeMode.Furniture)
        {
            RestoreFurnitureInput();
            SetStatus("Modo mobiliario activo.");
        }
        else
        {
            SuspendFurnitureInput();
            SetStatus(ModeHelp(next));
        }
        RefreshVisuals();
    }

    public void SetRoomZone(string definitionId)
    {
        if (definitions == null || !definitions.ContainsZone(definitionId))
        {
            SetStatus("La zona " + definitionId + " no está publicada para construcción.");
            return;
        }
        zoneDefinitionId = definitionId;
        SetMode(BistroBuilderConstructionRuntimeMode.Room);
        SetStatus("Habitación " + ZoneLabel(definitionId) + ": marca dos esquinas opuestas.");
    }
    public bool TryUndo(out string error)
    {
        error = string.Empty;
        if (coordinator == null || !coordinator.HasSession || !coordinator.TryUndo(out error)) return false;
        RefreshAfterDraftMutation("Cambio deshecho.");
        return true;
    }

    public bool TryRedo(out string error)
    {
        error = string.Empty;
        if (coordinator == null || !coordinator.HasSession || !coordinator.TryRedo(out error)) return false;
        RefreshAfterDraftMutation("Cambio rehecho.");
        return true;
    }

    public bool TryCommitDraft(out string error)
    {
        error = string.Empty;
        CancelGesture(string.Empty);
        if (coordinator == null || !coordinator.HasSession) return true;
        if (!coordinator.IsDirty)
        {
            coordinator.CancelSession();
            SetStatus("No había cambios de construcción pendientes.");
            return true;
        }
        if (!coordinator.TryReview(out var diagnostics))
        {
            error = DiagnosticSummary(diagnostics); SetStatus(error); return false;
        }
        if (!coordinator.TryCommit(out _, out diagnostics, out error))
        {
            if (string.IsNullOrWhiteSpace(error)) error = DiagnosticSummary(diagnostics);
            SetStatus(error); return false;
        }
        selection.Clear();
        ClearDraftOverlay();
        SetStatus("Construcción aplicada y publicada.");
        RefreshVisuals();
        return true;
    }
    public bool TryCancelDraft(out string error)
    {
        error = string.Empty;
        CancelGesture(string.Empty);
        if (coordinator == null || !coordinator.HasSession) return true;
        if (!coordinator.CancelSession())
        {
            error = "No se pudo descartar la construcción.";
            SetStatus(error);
            return false;
        }
        selection.Clear();
        ClearDraftOverlay();
        SetStatus("Construcción pendiente descartada.");
        RefreshVisuals();
        return true;
    }

    public bool TryDeleteSelection(out string error)
    {
        error = string.Empty;
        if (selection.Kind == EntityKind.None || !EnsureSession(out error)) return false;
        IBistroBuilderEditCommand command = selection.Kind == EntityKind.Wall
            ? new BistroBuilderDeleteWallCommand(selection.Id)
            : selection.Kind == EntityKind.Opening
                ? new BistroBuilderDeleteOpeningCommand(selection.Id)
                : null;
        if (command == null)
        {
            error = "Selecciona una pared, puerta o ventana para eliminarla.";
            SetStatus(error); return false;
        }
        if (!coordinator.TryExecute(command, out _, out error))
        {
            SetStatus(error); return false;
        }
        selection.Clear();
        RefreshAfterDraftMutation("Elemento eliminado.");
        return true;
    }
    private bool EnsureSession(out string error)
    {
        error = string.Empty;
        if (coordinator == null) { error = "Falta el coordinador de construcción."; return false; }
        if (coordinator.HasSession) return true;
        if (!coordinator.TryBeginSession(out error)) return false;
        RefreshQueries();
        RefreshDraftOverlay();
        return true;
    }

    private void RefreshQueries()
    {
        if (coordinator == null || coordinator.Session == null) return;
        if (queries.Refresh(coordinator.Session)) selection.Reconcile(queries);
    }

    private void UpdateHoverOrGesture(Vector2 rawPoint)
    {
        if (IsGestureActive()) UpdateGesture(rawPoint);
        else UpdateHover(rawPoint);
    }

    private void HandlePointerPressed(Vector2 rawPoint)
    {
        if (IsGestureActive() && twoClickGesture) { ConfirmTwoClick(rawPoint); return; }
        switch (mode)
        {
            case BistroBuilderConstructionRuntimeMode.Select: BeginSelectionOrDrag(rawPoint); break;
            case BistroBuilderConstructionRuntimeMode.Wall: BeginWall(rawPoint); break;
            case BistroBuilderConstructionRuntimeMode.Room: BeginRoom(rawPoint); break;
            case BistroBuilderConstructionRuntimeMode.Door:
            case BistroBuilderConstructionRuntimeMode.Window: PlaceOpening(rawPoint); break;
        }
    }
    private void BeginWall(Vector2 rawPoint)
    {
        Vector2 start = ResolvePoint(rawPoint, null, default, null);
        gesture = new ConstructionGesture();
        if (!gesture.BeginWall(coordinator.Session, queries, start, CreateWallTemplate(), out string error))
        { SetStatus(error); return; }
        gestureAnchor = start;
        twoClickGesture = true;
        dragGesture = false;
        SetStatus("Pared: mueve el ratón y haz clic para confirmar. Escape cancela.");
    }

    private void BeginRoom(Vector2 rawPoint)
    {
        Vector2 start = ResolvePoint(rawPoint, null, default, null);
        gesture = new ConstructionGesture();
        if (!gesture.BeginRectangle(coordinator.Session, queries, start, CreateWallTemplate(),
            definitions, zoneDefinitionId, minimumRoomSide, out string error))
        { SetStatus(error); return; }
        gestureAnchor = start;
        twoClickGesture = true;
        dragGesture = false;
        SetStatus("Habitación: mueve el ratón y haz clic en la esquina opuesta.");
    }

    private BistroBuilderWallRecord CreateWallTemplate()
    {
        return new BistroBuilderWallRecord
        {
            wallId = BistroBuilderEditId.NewId(), buildPlaneId = "default",
            axisStart = Vector2.zero, axisEnd = Vector2.right,
            baseElevation = 0f, height = wallHeight, thickness = wallThickness,
            wallDefinitionId = "wall.default"
        };
    }
    private void ConfirmTwoClick(Vector2 rawPoint)
    {
        UpdateGesture(rawPoint);
        if (gesture.State != ConstructionGestureState.Ready)
        { SetStatus(TranslateDiagnostic(gesture.Diagnostic)); return; }
        Vector2 confirmedPoint = ResolvePoint(rawPoint, gestureAnchor, default, null);
        ConstructionGestureKind kind = gesture.Kind;
        if (!gesture.Confirm(coordinator.TryExecute, out string error))
        { SetStatus(string.IsNullOrWhiteSpace(error) ? "No se pudo confirmar." : error); return; }
        RefreshAfterDraftMutation(kind == ConstructionGestureKind.Wall ? "Pared creada." : "Habitación creada.");
        if (kind == ConstructionGestureKind.Wall)
        {
            RefreshQueries();
            gesture = new ConstructionGesture();
            if (gesture.BeginWall(coordinator.Session, queries, confirmedPoint, CreateWallTemplate(), out error))
            {
                gestureAnchor = confirmedPoint;
                twoClickGesture = true;
                dragGesture = false;
                SetStatus("Pared creada. Continúa desde el último punto o pulsa Escape.");
                return;
            }
        }
        ResetGestureState();
    }

    private void UpdateGesture(Vector2 rawPoint)
    {
        if (!IsGestureActive()) return;
        BistroBuilderEditId excluded = gesture.Kind == ConstructionGestureKind.MoveWall ? selection.Id : default;
        Vector2? excludedPoint = gesture.Kind == ConstructionGestureKind.MoveJunction ? gestureAnchor : (Vector2?)null;
        Vector2 point = ResolvePoint(rawPoint, gestureAnchor, excluded, excludedPoint);
        if (gesture.Kind == ConstructionGestureKind.Wall && ShiftPressed() &&
            lastSnapKind != SnapKind.Endpoint && lastSnapKind != SnapKind.Intersection && lastSnapKind != SnapKind.Wall)
            point = LockAngle45(gestureAnchor, point);
        gesture.Update(point);
        RenderGesture(point);
        if (gesture.State == ConstructionGestureState.Invalid && gesture.Diagnostic != "NO_CHANGE")
            SetStatus(TranslateDiagnostic(gesture.Diagnostic));
    }
    private void BeginSelectionOrDrag(Vector2 point)
    {
        ArchitectureHit hit = queries.Pick(point, selectionRadius, "default");
        if (!hit.IsValid)
        {
            selection.Clear(); RefreshVisuals(); SetStatus("Nada seleccionado."); return;
        }
        selection.Select(queries, hit);
        RefreshVisuals();
        if (hit.Kind != EntityKind.Wall)
        {
            SetStatus(hit.Kind == EntityKind.Room ? "Habitación seleccionada." : "Puerta/ventana seleccionada.");
            return;
        }

        BistroBuilderWallRecord wall = queries.CaptureWall(hit.Id);
        if (wall == null) return;
        bool nearStart = Vector2.Distance(point, wall.axisStart) <= handleRadius;
        bool nearEnd = Vector2.Distance(point, wall.axisEnd) <= handleRadius;
        gesture = new ConstructionGesture();
        string error;
        bool began;
        if (nearStart || nearEnd)
        {
            Vector2 endpoint = nearStart ? wall.axisStart : wall.axisEnd;
            began = gesture.BeginJunction(coordinator.Session, queries, endpoint, wall.buildPlaneId, out error);
            gestureAnchor = endpoint;
        }
        else
        {
            began = gesture.BeginMoveWall(coordinator.Session, queries, hit.Id, point, out error);
            gestureAnchor = point;
        }
        if (!began) { SetStatus(error); ResetGestureState(); return; }
        dragGesture = true;
        twoClickGesture = false;
        SetStatus(nearStart || nearEnd ? "Arrastra la esquina/unión." : "Arrastra la pared perpendicularmente.");
    }
    private void FinishDragGesture()
    {
        if (!dragGesture) return;
        if (gesture.State == ConstructionGestureState.Ready)
        {
            if (gesture.Confirm(coordinator.TryExecute, out string error))
                RefreshAfterDraftMutation("Movimiento aplicado al borrador.");
            else SetStatus(error);
        }
        else if (gesture.State == ConstructionGestureState.Invalid && gesture.Diagnostic != "NO_CHANGE")
            SetStatus(TranslateDiagnostic(gesture.Diagnostic));
        ResetGestureState();
        RefreshVisuals();
    }

    private void PlaceOpening(Vector2 point)
    {
        ArchitectureHit wallHit = queries.PickWall(point, selectionRadius * 2f, "default");
        if (!wallHit.IsValid) { SetStatus("Acerca el cursor a una pared válida."); return; }
        BistroBuilderOpeningRecord template = CreateOpeningTemplate(mode);
        gesture = new ConstructionGesture();
        if (!gesture.BeginOpening(coordinator.Session, queries, wallHit.Id, template, out string error))
        { SetStatus(error); ResetGestureState(); return; }
        if (!gesture.Update(point) || gesture.State != ConstructionGestureState.Ready)
        { SetStatus(TranslateDiagnostic(gesture.Diagnostic)); ResetGestureState(); return; }
        if (!gesture.Confirm(coordinator.TryExecute, out error))
        { SetStatus(error); ResetGestureState(); return; }
        RefreshAfterDraftMutation(mode == BistroBuilderConstructionRuntimeMode.Door ? "Puerta colocada." : "Ventana colocada.");
        ResetGestureState();
    }
    private BistroBuilderOpeningRecord CreateOpeningTemplate(BistroBuilderConstructionRuntimeMode openingMode)
    {
        bool window = openingMode == BistroBuilderConstructionRuntimeMode.Window;
        return new BistroBuilderOpeningRecord
        {
            openingId = BistroBuilderEditId.NewId(),
            openingType = window ? "window" : "door",
            fillDefinitionId = window ? "window" : "door",
            width = window ? 1.2f : 0.9f,
            bottomElevation = window ? 1f : 0f,
            height = window ? 1.2f : 2.1f,
            flipped = false
        };
    }

    private void UpdateHover(Vector2 point)
    {
        if (mode == BistroBuilderConstructionRuntimeMode.Door || mode == BistroBuilderConstructionRuntimeMode.Window)
        {
            RenderOpeningHover(point); return;
        }
        HideSnapMarker();
        if (mode == BistroBuilderConstructionRuntimeMode.Select) RefreshVisuals();
    }

    private void RenderOpeningHover(Vector2 point)
    {
        ArchitectureHit wallHit = queries.PickWall(point, selectionRadius * 2f, "default");
        if (!wallHit.IsValid) { ClearPreviewLines(); HideSnapMarker(); return; }
        BistroBuilderWallRecord host = queries.CaptureWall(wallHit.Id);
        BistroBuilderOpeningRecord template = CreateOpeningTemplate(mode);
        bool ok = ConstructionGeometry.TryOpening(host, point, template.width, template.bottomElevation,
            template.height, out _, out Vector2 center, out string error);
        Vector2 direction = (host.axisEnd - host.axisStart).normalized;
        Vector2 half = direction * (template.width * 0.5f);
        EnsurePreviewLineCount(1);
        SetLine(previewLines[0], center - half, center + half, ok ? ValidColor : InvalidColor, 0.075f);
        HideUnusedPreviewLines(1);
        ShowSnapMarker(center, SnapKind.Wall);
        if (!ok) SetStatus(TranslateDiagnostic(error));
    }

    private Vector2 ResolvePoint(Vector2 raw, Vector2? anchor, BistroBuilderEditId excludedWall, Vector2? excludedPoint)
    {
        bool alt = AltPressed();
        bool oldGrid = snapSettings.GridEnabled;
        bool oldAxis = snapSettings.AxisEnabled;
        if (alt) { snapSettings.GridEnabled = false; snapSettings.AxisEnabled = false; }
        SnapResult result = snapService.Resolve(queries, raw, snapSettings, anchor, "default", excludedWall, excludedPoint);
        snapSettings.GridEnabled = oldGrid;
        snapSettings.AxisEnabled = oldAxis;
        lastSnapKind = result.Kind;
        Vector2 point = result.IsSnapped ? result.Point : raw;
        ShowSnapMarker(point, result.Kind);
        return point;
    }

    private static Vector2 LockAngle45(Vector2 anchor, Vector2 point)
    {
        Vector2 delta = point - anchor;
        if (delta.sqrMagnitude < 0.000001f) return point;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float locked = Mathf.Round(angle / 45f) * 45f * Mathf.Deg2Rad;
        return anchor + new Vector2(Mathf.Cos(locked), Mathf.Sin(locked)) * delta.magnitude;
    }
    private void RenderGesture(Vector2 currentPoint)
    {
        Color color = gesture.State == ConstructionGestureState.Ready ? ValidColor : InvalidColor;
        int count = gesture.PreviewWalls.Count;
        if (count > 0)
        {
            EnsurePreviewLineCount(count);
            for (int i = 0; i < count; i++)
                SetLine(previewLines[i], gesture.PreviewWalls[i].Start, gesture.PreviewWalls[i].End, color, 0.055f);
            HideUnusedPreviewLines(count);
            return;
        }
        if (gesture.Kind == ConstructionGestureKind.Wall)
        {
            EnsurePreviewLineCount(1);
            SetLine(previewLines[0], gestureAnchor, currentPoint, InvalidColor, 0.055f);
            HideUnusedPreviewLines(1);
        }
        else if (gesture.Kind == ConstructionGestureKind.Rectangle)
            RenderRectangleFallback(gestureAnchor, currentPoint, color);
        else
            ClearPreviewLines();
    }

    private void RenderRectangleFallback(Vector2 a, Vector2 b, Color color)
    {
        Vector2 min = Vector2.Min(a, b), max = Vector2.Max(a, b);
        Vector2 p0 = min, p1 = new Vector2(max.x, min.y), p2 = max, p3 = new Vector2(min.x, max.y);
        EnsurePreviewLineCount(4);
        SetLine(previewLines[0], p0, p1, color, 0.055f);
        SetLine(previewLines[1], p1, p2, color, 0.055f);
        SetLine(previewLines[2], p2, p3, color, 0.055f);
        SetLine(previewLines[3], p3, p0, color, 0.055f);
        HideUnusedPreviewLines(4);
    }
    private void RefreshVisuals()
    {
        EnsureVisuals();
        if (selection.Kind == EntityKind.None)
        { selectionLine.enabled = false; return; }

        if (selection.Kind == EntityKind.Wall)
        {
            BistroBuilderWallRecord wall = queries.CaptureWall(selection.Id);
            if (wall == null) { selectionLine.enabled = false; return; }
            SetPolyline(selectionLine, new[] { wall.axisStart, wall.axisEnd }, SelectionColor, 0.075f, false);
            return;
        }
        if (selection.Kind == EntityKind.Opening)
        {
            BistroBuilderOpeningRecord opening = FindOpening(selection.Id);
            BistroBuilderWallRecord host = opening != null ? queries.CaptureWall(opening.hostWallId) : null;
            if (opening == null || host == null) { selectionLine.enabled = false; return; }
            Vector2 center = Vector2.Lerp(host.axisStart, host.axisEnd, opening.axisPosition01);
            Vector2 half = (host.axisEnd - host.axisStart).normalized * (opening.width * 0.5f);
            SetPolyline(selectionLine, new[] { center - half, center + half }, SelectionColor, 0.09f, false);
            return;
        }
        foreach (BistroBuilderRoomProjection room in queries.Rooms)
        {
            if (room.room.roomId != selection.Id || room.boundary.Count < 3) continue;
            Vector2[] points = new Vector2[room.boundary.Count];
            room.boundary.CopyTo(points, 0);
            SetPolyline(selectionLine, points, SelectionColor, 0.055f, true);
            return;
        }
        selectionLine.enabled = false;
    }
    private BistroBuilderOpeningRecord FindOpening(BistroBuilderEditId id)
    {
        foreach (BistroBuilderOpeningRecord opening in queries.Openings)
            if (opening != null && opening.openingId == id) return opening.DeepClone();
        return null;
    }

    private void EnsureVisuals()
    {
        if (visualRoot != null) return;
        visualRoot = new GameObject("BB18N_ConstructionAuthoringVisuals");
        visualRoot.transform.SetParent(transform, false);
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
            lineMaterial = new Material(shader)
            { name = "BB18N_RuntimeLineMaterial", hideFlags = HideFlags.HideAndDontSave };
        selectionLine = CreateLine("Selection");
        snapMarker = CreateLine("Snap");
        previewLines = Array.Empty<LineRenderer>();
    }

    private LineRenderer CreateLine(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(visualRoot.transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = lineMaterial;
        line.numCapVertices = 2;
        line.enabled = false;
        return line;
    }

    private void EnsureDraftLineCount(int count)
    {
        if (draftLines.Length >= count) return;
        int old = draftLines.Length;
        Array.Resize(ref draftLines, count);
        for (int i=old;i<count;i++) draftLines[i]=CreateLine("Draft_"+i.ToString("D2"));
    }

    private void ClearDraftOverlay()
    {
        for (int i=0;i<draftLines.Length;i++)
            if (draftLines[i] != null) draftLines[i].enabled=false;
    }

    private void EnsurePreviewLineCount(int count)
    {
        if (previewLines.Length >= count) return;
        int old = previewLines.Length;
        Array.Resize(ref previewLines, count);
        for (int i = old; i < count; i++)
            previewLines[i] = CreateLine("Preview_" + i.ToString("D2"));
    }

    private void SetLine(LineRenderer line, Vector2 a, Vector2 b, Color color, float width)
    {
        SetPolyline(line, new[] { a, b }, color, width, false);
    }

    private void SetPolyline(LineRenderer line, Vector2[] points, Color color, float width, bool loop)
    {
        if (line == null || points == null || points.Length < 2) return;
        line.enabled = true;
        line.loop = loop;
        line.positionCount = points.Length;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        for (int i = 0; i < points.Length; i++)
            line.SetPosition(i, new Vector3(points[i].x, 0.06f, points[i].y));
    }
    private void HideUnusedPreviewLines(int used)
    {
        for (int i = used; i < previewLines.Length; i++) previewLines[i].enabled = false;
    }

    private void ClearPreviewLines()
    {
        for (int i = 0; i < previewLines.Length; i++) previewLines[i].enabled = false;
    }

    private void ShowSnapMarker(Vector2 point, SnapKind kind)
    {
        EnsureVisuals();
        float r = 0.11f;
        snapMarker.enabled = true;
        snapMarker.loop = false;
        snapMarker.positionCount = 4;
        snapMarker.startWidth = snapMarker.endWidth = 0.035f;
        snapMarker.startColor = snapMarker.endColor = kind == SnapKind.None
            ? new Color(0.7f, 0.7f, 0.7f, 0.8f) : SnapColor;
        snapMarker.SetPosition(0, new Vector3(point.x-r, 0.07f, point.y-r));
        snapMarker.SetPosition(1, new Vector3(point.x+r, 0.07f, point.y+r));
        snapMarker.SetPosition(2, new Vector3(point.x-r, 0.07f, point.y+r));
        snapMarker.SetPosition(3, new Vector3(point.x+r, 0.07f, point.y-r));
    }

    private void HideSnapMarker()
    {
        if (snapMarker != null) snapMarker.enabled = false;
    }
    private bool TryGetPlanPoint(out Vector2 point)
    {
        point = default;
        CacheDependencies();
        if (interactionCamera == null || Mouse.current == null) return false;
        Vector2 pointer = Mouse.current.position.ReadValue();
        Ray ray = interactionCamera.ScreenPointToRay(pointer);
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float distance)) return false;
        Vector3 world = ray.GetPoint(distance);
        point = new Vector2(world.x, world.z);
        return ConstructionGeometry.Finite(point);
    }

    private bool PointerHitsUi()
    {
        if (Mouse.current == null) return true;
        Vector2 pointer = Mouse.current.position.ReadValue();
        Vector2 guiPoint = new Vector2(pointer.x, Screen.height - pointer.y);
        if (showPlaytestPanel && panelRect.Contains(guiPoint)) return true;
        if (BistroBuilderRuntimePointerUiGuard.IsPointerBlocked(pointer)) return true;
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private static bool ShiftPressed()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
    }

    private static bool AltPressed()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
    }
    private bool IsGestureActive()
    {
        return gesture != null &&
            (gesture.State == ConstructionGestureState.Previewing ||
             gesture.State == ConstructionGestureState.Ready ||
             gesture.State == ConstructionGestureState.Invalid);
    }

    private void CancelGesture(string message)
    {
        if (gesture != null && IsGestureActive()) gesture.Cancel();
        ResetGestureState();
        if (!string.IsNullOrWhiteSpace(message)) SetStatus(message);
        RefreshVisuals();
    }

    private void ResetGestureState()
    {
        gesture = new ConstructionGesture();
        twoClickGesture = false;
        dragGesture = false;
        snapService.Reset();
        ClearPreviewLines();
        HideSnapMarker();
    }

    private void RefreshAfterDraftMutation(string message)
    {
        RefreshQueries();
        RefreshDraftOverlay();
        if (!string.IsNullOrWhiteSpace(message)) SetStatus(message);
        RefreshVisuals();
    }
    private void RefreshDraftOverlay()
    {
        EnsureVisuals();
        if (coordinator == null || coordinator.Session == null)
        {
            ClearDraftOverlay();
            return;
        }
        int count = queries.Walls.Count + queries.Openings.Count;
        EnsureDraftLineCount(count);
        int index = 0;
        Color wallColor = new Color(0.2f, 0.82f, 0.95f, 0.65f);
        foreach (BistroBuilderWallRecord wall in queries.Walls)
            SetLine(draftLines[index++], wall.axisStart, wall.axisEnd, wallColor, 0.035f);
        foreach (BistroBuilderOpeningRecord opening in queries.Openings)
        {
            BistroBuilderWallRecord host = queries.CaptureWall(opening.hostWallId);
            if (host == null) continue;
            Vector2 center = Vector2.Lerp(host.axisStart, host.axisEnd, opening.axisPosition01);
            Vector2 half = (host.axisEnd-host.axisStart).normalized * (opening.width*0.5f);
            SetLine(draftLines[index++], center-half, center+half, SelectionColor, 0.065f);
        }
        for (int i=index;i<draftLines.Length;i++) draftLines[i].enabled=false;
    }

    private void SetStatus(string message)
    {
        status = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
    }

    private void SetVisualsVisible(bool visible)
    {
        if (visualRoot != null) visualRoot.SetActive(visible);
    }

    private void SuspendFurnitureInput()
    {
        if (furnitureInputSuspended) return;
        CacheDependencies();
        if (furnitureController == null) return;
        furnitureControllerWasEnabled = furnitureController.enabled;
        furnitureInputSuspended = true;
        if (furnitureControllerWasEnabled) furnitureController.enabled = false;
    }

    private void RestoreFurnitureInput()
    {
        if (!furnitureInputSuspended) return;
        CacheDependencies();
        if (furnitureController != null && furnitureControllerWasEnabled)
            furnitureController.enabled = true;
        furnitureControllerWasEnabled = false;
        furnitureInputSuspended = false;
    }

    private void ResolveDefinitions()
    {
        try
        {
            BistroBuilderEditFinanceTariffTable table = Resources.Load<BistroBuilderEditFinanceTariffTable>(
                "BistroBuilder/Finance/BB_EditMode_PlaytestTariffs");
            if (table != null) definitions = ConstructionCatalogAdapter.FromTariffs(table);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Construction catalog unavailable: " + exception.Message, this);
        }
        if (definitions == null)
            definitions = new ConstructionDefinitionCatalog(Array.Empty<BistroBuilderEditCatalogDefinition>());
        if (!definitions.ContainsZone(zoneDefinitionId) && definitions.ZoneIds.Count > 0)
            zoneDefinitionId = definitions.ZoneIds[0];
    }

    private void CacheDependencies()
    {
        if (coordinator == null) coordinator = FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>();
        if (editModeService == null) editModeService = FindFirstObjectByType<RestaurantEditModeService>();
        if (furnitureController == null) furnitureController = FindFirstObjectByType<RestaurantEditInteractionController>();
        if (interactionCamera == null)
            interactionCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
    }

    private static string DiagnosticSummary(System.Collections.Generic.IReadOnlyList<BistroBuilderEditDiagnostic> diagnostics)
    {
        if (diagnostics == null || diagnostics.Count == 0) return "La construcción no pudo validarse.";
        for (int i = 0; i < diagnostics.Count; i++)
        {
            BistroBuilderEditDiagnostic d = diagnostics[i];
            if (d != null && d.severity == BistroBuilderEditDiagnosticSeverity.Blocking)
                return string.IsNullOrWhiteSpace(d.message) ? d.code : d.message;
        }
        return diagnostics[0] != null ? diagnostics[0].message : "La construcción necesita revisión.";
    }
    private static string TranslateDiagnostic(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        switch (code)
        {
            case "WALL_TOO_SHORT_OR_NONFINITE": return "La pared es demasiado corta.";
            case "RECTANGLE_TOO_SMALL": return "La habitación es demasiado pequeña.";
            case "NO_CHANGE": return "Sin cambios.";
            case "MOVE_WOULD_DETACH_HOSTED_ENDPOINT": return "Ese movimiento rompería una unión existente.";
            case "MOVE_WOULD_LOSE_CROSSING": return "Ese movimiento rompería una intersección.";
            case "CONNECTED_WALL_TOO_SHORT": return "Una pared conectada quedaría demasiado corta.";
            case "INTERIOR_JUNCTION_MUST_STAY_ON_STRAIGHT_HOST": return "La unión debe mantenerse sobre su pared principal.";
            case "OPENING_DOES_NOT_FIT_HOST": return "La puerta o ventana no cabe en esa pared.";
            case "OPENING_INVALID_DIMENSIONS": return "La puerta o ventana no es válida para esa pared.";
            case "GESTURE_STALE_OR_NONFINITE": return "La construcción cambió; vuelve a intentarlo.";
            default: return code.Replace('_', ' ');
        }
    }

    private static string ModeHelp(BistroBuilderConstructionRuntimeMode value)
    {
        switch (value)
        {
            case BistroBuilderConstructionRuntimeMode.Select: return "Seleccionar: clic en pared/espacio/abertura; arrastra paredes o esquinas.";
            case BistroBuilderConstructionRuntimeMode.Wall: return "Pared: clic inicio, mueve, clic final. Continúa encadenando.";
            case BistroBuilderConstructionRuntimeMode.Room: return "Espacio: clic en dos esquinas opuestas.";
            case BistroBuilderConstructionRuntimeMode.Door: return "Puerta: haz clic sobre una pared.";
            case BistroBuilderConstructionRuntimeMode.Window: return "Ventana: haz clic sobre una pared.";
            default: return "Modo mobiliario activo.";
        }
    }
    private static string ZoneLabel(string id)
    {
        if (id == "zone.dining") return "Salón";
        if (id == "zone.kitchen") return "Cocina";
        if (id == "zone.bathroom") return "Baño";
        if (id == "zone.bar") return "Barra";
        if (id == "zone.terrace") return "Terraza";
        return id ?? string.Empty;
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !showPlaytestPanel) return;
        EnsureGuiStyles();
        float width = Mathf.Min(980f, Screen.width - 24f);
        panelRect = new Rect((Screen.width - width) * 0.5f,
            Mathf.Max(12f, Screen.height - 178f), width, 166f);
        BistroBuilderRuntimePointerUiGuard.PublishBlockedGuiRect(panelRect);
        GUI.Box(panelRect, GUIContent.none);
        GUILayout.BeginArea(new Rect(panelRect.x + 12f, panelRect.y + 8f,
            panelRect.width - 24f, panelRect.height - 16f));
        GUILayout.Label("BISTRO BUILDER · CONSTRUCCIÓN", titleStyle);
        if (editModeService == null || !editModeService.IsEditModeActive)
            DrawEnterEditMode();
        else DrawConstructionPanel();
        GUILayout.EndArea();
    }

    private void DrawEnterEditMode()
    {
        GUILayout.Label("Activa el modo edición para construir y editar el local.", labelStyle);
        GUILayout.Space(8f);
        if (GUILayout.Button("ENTRAR EN MODO EDICIÓN", GUILayout.Height(38f)))
        {
            CacheDependencies();
            bool entered = furnitureController != null && furnitureController.TryEnterEditMode();
            if (!entered && editModeService != null)
                entered = editModeService.TryEnterEditMode(out _, out _);
            SetStatus(entered ? "Modo edición activado." : "No se pudo activar el modo edición.");
        }
        GUILayout.Label(status + " · F8 oculta/muestra este panel.", statusStyle);
    }

    private void DrawConstructionPanel()
    {
        GUILayout.BeginHorizontal();
        ToolButton("Mobiliario", BistroBuilderConstructionRuntimeMode.Furniture);
        ToolButton("Seleccionar", BistroBuilderConstructionRuntimeMode.Select);
        ToolButton("Pared", BistroBuilderConstructionRuntimeMode.Wall);
        ToolButton("Espacio", BistroBuilderConstructionRuntimeMode.Room);
        ToolButton("Puerta", BistroBuilderConstructionRuntimeMode.Door);
        ToolButton("Ventana", BistroBuilderConstructionRuntimeMode.Window);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Zona:", labelStyle, GUILayout.Width(44f));
        if (definitions != null)
            foreach (string zoneId in definitions.ZoneIds)
                ZoneButton(ZoneLabel(zoneId), zoneId);
        GUILayout.FlexibleSpace();
        GUI.enabled = CanUndo;
        if (GUILayout.Button("Deshacer", GUILayout.Width(88f))) TryUndo(out _);
        GUI.enabled = CanRedo;
        if (GUILayout.Button("Rehacer", GUILayout.Width(88f))) TryRedo(out _);
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        string selected = selection.Kind == EntityKind.None ? "Nada" : selection.Kind + " " + selection.Id.Value;
        GUILayout.Label("Selección: " + selected, labelStyle, GUILayout.Width(300f));
        GUILayout.Label(BuildDimensionText(), labelStyle, GUILayout.Width(360f));
        GUILayout.Label("Snap: " + lastSnapKind, labelStyle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(status, statusStyle, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Cancelar cambios", GUILayout.Width(145f), GUILayout.Height(28f)))
            TryCancelDraft(out _);
        if (GUILayout.Button("APLICAR CAMBIOS", GUILayout.Width(160f), GUILayout.Height(28f)))
            TryCommitDraft(out _);
        GUILayout.EndHorizontal();
    }

    private void ToolButton(string label, BistroBuilderConstructionRuntimeMode target)
    {
        bool active = mode == target;
        bool pressed = GUILayout.Toggle(active, label, GUI.skin.button, GUILayout.Height(28f));
        if (pressed && !active) SetMode(target);
    }

    private void ZoneButton(string label, string id)
    {
        if (definitions == null || !definitions.ContainsZone(id)) return;
        bool active = zoneDefinitionId == id;
        bool pressed = GUILayout.Toggle(active, label, GUI.skin.button, GUILayout.Width(78f), GUILayout.Height(24f));
        if (pressed && !active) SetRoomZone(id);
    }

    private string BuildDimensionText()
    {
        if (!IsGestureActive()) return "Escala 0,25 m · Shift 45° · Alt libre";
        ConstructionDimensions d = gesture.Dimensions;
        if (gesture.Kind == ConstructionGestureKind.Rectangle)
            return "Ancho " + d.Width.ToString("0.00") + " m · Fondo " + d.Depth.ToString("0.00") +
                   " m · Área " + d.Area.ToString("0.00") + " m²";
        if (gesture.Kind == ConstructionGestureKind.Opening)
            return "Hueco " + d.Width.ToString("0.00") + " × " + d.Depth.ToString("0.00") + " m";
        return "Longitud " + d.Length.ToString("0.00") + " m · Ángulo " +
               d.AngleDegrees.ToString("0") + "°";
    }

    private void EnsureGuiStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
    }

    private void OnDestroy()
    {
        if (lineMaterial == null) return;
        if (Application.isPlaying) Destroy(lineMaterial);
        else DestroyImmediate(lineMaterial);
        lineMaterial = null;
    }
}
