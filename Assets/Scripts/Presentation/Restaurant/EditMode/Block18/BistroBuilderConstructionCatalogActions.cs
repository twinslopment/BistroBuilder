using System;
using System.Collections.Generic;
using BistroBuilder.ConstructionAuthoring;
using UnityEngine;

public sealed partial class BistroBuilderConstructionAuthoringRuntimeTool
{
    public void ConfigureWallFromInterface(float height, float thickness)
    {
        wallHeight = Mathf.Clamp(height, 1f, 6f);
        wallThickness = Mathf.Clamp(thickness, .05f, .6f);
    }

    public bool TryRefreshSelectedRoomFloorFinishPreview(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (universalPreviewService == null)
            return true;
        if (coordinator == null || !coordinator.HasSession)
        {
            ClearSurfacePreview();
            error = "No existe una sesión de edición activa.";
            return false;
        }

        RefreshQueries();
        if (selection.Kind != EntityKind.Room)
        {
            ClearSurfacePreview();
            return true;
        }

        BistroBuilderRoomProjection selectedRoom = null;
        foreach (var room in queries.Rooms)
        {
            if (room.room.roomId == selection.Id)
            {
                selectedRoom = room;
                break;
            }
        }

        if (selectedRoom == null || selectedRoom.boundary.Count < 3)
        {
            ClearSurfacePreview();
            error = "La habitación no tiene un contorno válido.";
            return false;
        }

        int boundaryHash = 17;
        for (int i = 0; i < selectedRoom.boundary.Count; i++)
        {
            boundaryHash = boundaryHash * 31 + selectedRoom.boundary[i].GetHashCode();
        }

        string key = selection.Id.Value + "|finish.floor.default|" + boundaryHash;
        if (string.Equals(lastSurfacePreviewKey, key, StringComparison.Ordinal) &&
            universalPreviewService.Current.OwnerId == BistroBuilderUniversalPreviewService.ConstructionOwner &&
            universalPreviewService.Current.Domain == BistroBuilderPreviewDomain.Surface &&
            universalPreviewService.Current.IsVisible)
        {
            return true;
        }

        universalPreviewSegments.Clear();
        universalPreviewVolumes.Clear();
        Vector2 min = selectedRoom.boundary[0];
        Vector2 max = selectedRoom.boundary[0];
        for (int i = 0; i < selectedRoom.boundary.Count; i++)
        {
            Vector2 a = selectedRoom.boundary[i];
            Vector2 b = selectedRoom.boundary[(i + 1) % selectedRoom.boundary.Count];
            AddUniversalSegment(a, b, universalPreviewSegments, 0.035f);
            min = Vector2.Min(min, a);
            max = Vector2.Max(max, a);
        }

        if (IsAxisAlignedRectangle(selectedRoom.boundary))
        {
            Vector2 center = (min + max) * 0.5f;
            Vector2 size = max - min;
            universalPreviewVolumes.Add(
                new BistroBuilderPreviewBox(
                    new Vector3(center.x, 0.0125f, center.y),
                    Quaternion.identity,
                    new Vector3(
                        Mathf.Max(0.02f, size.x),
                        0.025f,
                        Mathf.Max(0.02f, size.y))));
        }

        universalPreviewService.PublishConstruction(
            BistroBuilderPreviewDomain.Surface,
            BistroBuilderPreviewValidity.Valid,
            BistroBuilderPreviewPhase.Ready,
            universalPreviewSegments,
            null,
            universalPreviewVolumes,
            false,
            Vector3.zero,
            "Superficie lista para aplicar.");

        lastSurfacePreviewKey = key;
        HideUnusedPreviewLines(0);
        return true;
    }

    private static bool IsAxisAlignedRectangle(
        IReadOnlyList<Vector2> boundary)
    {
        if (boundary == null || boundary.Count != 4)
            return false;

        const float epsilon = 0.001f;
        for (int i = 0; i < boundary.Count; i++)
        {
            Vector2 delta =
                boundary[(i + 1) % boundary.Count] -
                boundary[i];

            bool horizontal =
                Mathf.Abs(delta.y) <= epsilon &&
                Mathf.Abs(delta.x) > epsilon;
            bool vertical =
                Mathf.Abs(delta.x) <= epsilon &&
                Mathf.Abs(delta.y) > epsilon;

            if (!horizontal && !vertical)
                return false;
        }

        return true;
    }

    public void ClearSurfacePreview()
    {
        lastSurfacePreviewKey = string.Empty;
        if (universalPreviewService != null &&
            universalPreviewService.Current.OwnerId == BistroBuilderUniversalPreviewService.ConstructionOwner &&
            universalPreviewService.Current.Domain == BistroBuilderPreviewDomain.Surface)
        {
            universalPreviewService.ClearOwner(
                BistroBuilderUniversalPreviewService.ConstructionOwner);
        }
    }

    public bool TryApplySelectedRoomFloorFinish(out string error)
    {
        error = string.Empty;
        if (!EnsureSession(out error)) return false;
        RefreshQueries();
        if (selection.Kind != EntityKind.Room)
        {
            error = "Selecciona el suelo de una habitación cerrada.";
            return false;
        }
        BistroBuilderRoomProjection selectedRoom = null;
        foreach (var room in queries.Rooms)
            if (room.room.roomId == selection.Id) { selectedRoom = room; break; }
        if (selectedRoom == null || selectedRoom.boundary.Count < 3)
        {
            error = "La habitación no tiene un contorno válido.";
            return false;
        }
        var id = new BistroBuilderEditId("floor_finish_" + selection.Id.Value);
        var patch = new BistroBuilderSurfaceFinishPatchRecord
        {
            surfacePatchId = id,
            buildPlaneId = selectedRoom.room.buildPlaneId,
            surfaceRole = "floor",
            finishDefinitionId = "finish.floor.default"
        };
        patch.fallbackBoundary.AddRange(selectedRoom.boundary);
        foreach (var current in coordinator.Session.Draft.surfaces)
        {
            if (current.surfacePatchId != id || current.finishDefinitionId != patch.finishDefinitionId ||
                current.fallbackBoundary.Count != patch.fallbackBoundary.Count) continue;
            bool same = true;
            for (int i = 0; i < patch.fallbackBoundary.Count; i++)
                same &= current.fallbackBoundary[i] == patch.fallbackBoundary[i];
            if (same) { error = "La habitación ya tiene este acabado."; return false; }
        }
        if (!coordinator.TryExecute(new BistroBuilderApplySurfaceFinishCommand(patch), out _, out error))
            return false;
        RefreshAfterDraftMutation("Acabado preparado en el borrador. Aplica los cambios para confirmar.");
        return true;
    }
}
