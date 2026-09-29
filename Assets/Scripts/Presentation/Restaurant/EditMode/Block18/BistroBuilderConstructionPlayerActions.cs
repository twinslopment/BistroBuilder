using System;
using System.Collections.Generic;
using BistroBuilder.ConstructionAuthoring;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class BistroBuilderConstructionAuthoringRuntimeTool
{
    private Vector2 constructionPress;
    private bool constructionPointerDown;
    private float moduleLength = 1f;
    private float moduleAngle;
    private readonly WallPose[] modulePreviewPose = new WallPose[1];
    private BistroBuilderConstructionDraftView draftView;
    private float interactionPlaneHeight;
    public string DimensionsText => BuildDimensionText();
    public string ZoneDefinitionId => zoneDefinitionId;
    public IReadOnlyList<string> ZoneIds => definitions.ZoneIds;
    public float ModuleLength => moduleLength;
    public float ModuleAngle => moduleAngle;
    public float LastRotationFeedbackTime { get; private set; } = -1f;
    public bool HasHoverTarget { get; private set; }
    public int WallCount => coordinator != null && coordinator.HasSession ? coordinator.Session.Draft.walls.Count : 0;
    public int RoomCount => coordinator != null && coordinator.HasSession ? coordinator.Session.Draft.rooms.Count : 0;

    public void ConfigureModule(float length, float angle)
    {
        if (!ConstructionGeometry.Finite(length) || !ConstructionGeometry.Finite(angle)) return;
        float orthogonalAngle = Mathf.Repeat(Mathf.Round(angle / 90f) * 90f, 360f);
        if (!Mathf.Approximately(moduleAngle, orthogonalAngle)) LastRotationFeedbackTime = Time.unscaledTime;
        moduleLength = Mathf.Clamp(length, 0.25f, 8f);
        moduleAngle = orthogonalAngle;
        hasLastPointerScreen = false;
    }

    public bool CanRotateArchitecture => mode == BistroBuilderConstructionRuntimeMode.WallModule ||
        (mode != BistroBuilderConstructionRuntimeMode.Furniture && selection.Kind == EntityKind.Wall);

    public bool TryRotateArchitecture(out string error)
    {
        error = string.Empty;
        if (!CanRotateArchitecture) { error = "Selecciona una pared o un módulo para girar."; return false; }
        if (mode == BistroBuilderConstructionRuntimeMode.WallModule)
        {
            ConfigureModule(moduleLength, moduleAngle + 90f);
            SetStatus("Módulo girado 90°. Haz clic para colocarlo.");
            return true;
        }
        if (!EnsureSession(out error)) return false;
        CancelGesture(string.Empty);
        RefreshQueries();
        var wall = queries.CaptureWall(selection.Id);
        if (wall == null) { error = "La pared seleccionada ya no existe."; return false; }
        Vector2 center = (wall.axisStart + wall.axisEnd) * 0.5f;
        Vector2 half = (wall.axisEnd - wall.axisStart) * 0.5f;
        Vector2 rotated = new Vector2(-half.y, half.x);
        wall.axisStart = center - rotated;
        wall.axisEnd = center + rotated;
        if (!coordinator.TryExecute(new BistroBuilderAtomicEditCommand("Girar pared 90°",
            new BistroBuilderUpdateWallCommand(wall)), out _, out error))
        { SetStatus(error); return false; }
        LastRotationFeedbackTime = Time.unscaledTime;
        RefreshAfterDraftMutation("Pared girada 90°. Puedes deshacer el giro.");
        return true;
    }

    private static bool IsTextInputFocused()
    {
        var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return go != null && (go.GetComponent<TMP_InputField>() != null || go.GetComponent<InputField>() != null);
    }

    private bool TryPickWallSurface(Ray ray, out Vector2 point, out float elevation)
    {
        point=default; elevation=0; float closest=float.PositiveInfinity;
        foreach (var wall in queries.Walls)
        {
            Vector2 axis=wall.axisEnd-wall.axisStart;
            if(axis.sqrMagnitude<0.000001f) continue;
            var normal=new Vector3(-axis.y,0,axis.x).normalized;
            var plane=new Plane(normal,new Vector3(wall.axisStart.x,wall.baseElevation,wall.axisStart.y));
            if(!plane.Raycast(ray,out float distance) || distance>=closest) continue;
            Vector3 hit=ray.GetPoint(distance);
            if(hit.y<wall.baseElevation || hit.y>wall.baseElevation+wall.height) continue;
            Vector2 flat=new Vector2(hit.x,hit.z);
            float t=Vector2.Dot(flat-wall.axisStart,axis)/axis.sqrMagnitude;
            if(t<0 || t>1) continue;
            point=wall.axisStart+t*axis; elevation=hit.y; closest=distance;
        }
        return !float.IsPositiveInfinity(closest);
    }

    private Vector2 ModuleEnd(Vector2 start) => start +
        (moduleAngle < 45f ? Vector2.right : moduleAngle < 135f ? Vector2.up :
            moduleAngle < 225f ? Vector2.left : Vector2.down) * moduleLength;

    private void RenderModule(Vector2 raw)
    {
        Vector2 start = ResolvePoint(raw, null, default, null);
        EnsurePreviewLineCount(1);
        modulePreviewPose[0] = new WallPose(default, start, ModuleEnd(start));
        IsPreviewBlocked = ConstructionGeometry.HasCrossing(queries, modulePreviewPose, "default", 0f, wallHeight);
        SetLine(previewLines[0], start, ModuleEnd(start), IsPreviewBlocked ? InvalidColor : ValidColor, 0.065f);
        if (IsPreviewBlocked) SetStatus(BistroBuilderWallCrossingPolicy.Message);
        HideUnusedPreviewLines(1);
    }

    private void RenderSelectionHover(Vector2 point)
    {
        var hit = queries.Pick(point, selectionRadius, "default");
        HasHoverTarget = hit.IsValid;
        ClearPreviewLines();
        if (!hit.IsValid || hit.Id == selection.Id) return;
        var gold = BistroBuilderStateGraphic.Gold;
        if (hit.Kind == EntityKind.Wall)
        {
            var wall = queries.CaptureWall(hit.Id); if (wall == null) return;
            EnsurePreviewLineCount(1); SetLine(previewLines[0], wall.axisStart, wall.axisEnd, gold, 0.06f);
        }
        else if (hit.Kind == EntityKind.Opening)
        {
            var opening = FindOpening(hit.Id); var wall = opening != null ? queries.CaptureWall(opening.hostWallId) : null;
            if (wall == null) return;
            Vector2 center = Vector2.Lerp(wall.axisStart, wall.axisEnd, opening.axisPosition01);
            Vector2 half = (wall.axisEnd-wall.axisStart).normalized * opening.width * 0.5f;
            EnsurePreviewLineCount(1); SetLine(previewLines[0], center-half, center+half, gold, 0.07f);
        }
        else if (hit.Kind == EntityKind.Room)
        {
            foreach (var room in queries.Rooms)
            {
                if (room.room.roomId != hit.Id) continue;
                EnsurePreviewLineCount(room.boundary.Count);
                for(int i=0;i<room.boundary.Count;i++) SetLine(previewLines[i], room.boundary[i], room.boundary[(i+1)%room.boundary.Count], gold, 0.04f);
                break;
            }
        }
    }

    private void PlaceModule(Vector2 raw)
    {
        Vector2 start = ResolvePoint(raw, null, default, null);
        var command = CreateWallTemplate();
        command.axisStart = start;
        command.axisEnd = ModuleEnd(start);
        if (!coordinator.TryExecute(new BistroBuilderAtomicEditCommand("Colocar módulo",new BistroBuilderCreateWallCommand(command)), out _, out string error))
        { SetStatus(error); return; }
        RefreshAfterDraftMutation("Módulo colocado. Puedes seguir colocando módulos.");
    }

    public string SelectionDescription()
    {
        RefreshQueries();
        if (selection.Kind == EntityKind.Wall)
        {
            var wall = queries.CaptureWall(selection.Id);
            return wall == null ? "Sin selección" : "Pared\n\nLongitud  " + wall.Length.ToString("0.00") +
                " m\nAltura  " + wall.height.ToString("0.00") + " m\nGrosor  " + wall.thickness.ToString("0.00") +
                " m\n\nArrastra el centro para mover; los extremos para cambiar la forma.";
        }
        if (selection.Kind == EntityKind.Opening)
        {
            var opening = FindOpening(selection.Id);
            return opening == null ? "Sin selección" : (opening.openingType == "window" ? "Ventana" : "Puerta") +
                "\n\nAncho  " + opening.width.ToString("0.00") + " m\nAltura  " + opening.height.ToString("0.00") +
                " m\nAntepecho  " + opening.bottomElevation.ToString("0.00") + " m";
        }
        if (selection.Kind == EntityKind.Room)
        {
            foreach (var room in queries.Rooms)
                if (room.room.roomId == selection.Id) return "Habitación cerrada\n\n" + room.boundary.Count + " vértices";
        }
        return "Selecciona una pared, puerta, ventana o habitación para ver sus propiedades.";
    }

    public bool TryAdjustOpening(float deltaPosition, bool flip, out string error)
    {
        error = string.Empty;
        RefreshQueries();
        var opening = FindOpening(selection.Id);
        if (opening == null) { error = "Selecciona una puerta o ventana."; return false; }
        var host = queries.CaptureWall(opening.hostWallId);
        if (host == null) return false;
        opening.axisPosition01 += deltaPosition / host.Length;
        if (flip) opening.flipped = !opening.flipped;
        if (!coordinator.TryExecute(new BistroBuilderAtomicEditCommand("Editar hueco",
            new BistroBuilderDeleteOpeningCommand(opening.openingId),
            new BistroBuilderCreateOpeningCommand(opening)), out _, out error))
        { SetStatus(error); return false; }
        RefreshAfterDraftMutation("Hueco actualizado.");
        return true;
    }

    public bool TryCopySelection(out string error)
    {
        error = string.Empty;
        RefreshQueries();
        var wall = queries.CaptureWall(selection.Id);
        if (wall != null)
        {
            ConfigureModule(wall.Length, Mathf.Atan2(wall.axisEnd.y-wall.axisStart.y,
                wall.axisEnd.x-wall.axisStart.x) * Mathf.Rad2Deg);
            wallHeight = wall.height; wallThickness = wall.thickness;
            SetMode(BistroBuilderConstructionRuntimeMode.WallModule);
            SetStatus("Copia preparada. Haz clic para colocarla."); return true;
        }
        var opening = FindOpening(selection.Id);
        if (opening != null)
        {
            SetMode(opening.openingType == "window" ? BistroBuilderConstructionRuntimeMode.Window : BistroBuilderConstructionRuntimeMode.Door);
            return true;
        }
        error = "Selecciona una pared, puerta o ventana."; return false;
    }

    private void RefreshArchitecturePreview()
    {
        if (draftView == null) draftView = gameObject.AddComponent<BistroBuilderConstructionDraftView>();
        if (coordinator != null && coordinator.HasSession && coordinator.IsDirty)
            draftView.Show(coordinator.Session.Draft, coordinator.Session.RoomProjections);
        else draftView.Clear();
    }

    public void CancelCurrentGesture() => CancelGesture("Gesto cancelado.");
}
