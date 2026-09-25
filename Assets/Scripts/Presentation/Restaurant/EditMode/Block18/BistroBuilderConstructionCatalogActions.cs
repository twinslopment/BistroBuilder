using System;
using BistroBuilder.ConstructionAuthoring;
using UnityEngine;

public sealed partial class BistroBuilderConstructionAuthoringRuntimeTool
{
    public void ConfigureWallFromInterface(float height, float thickness)
    {
        wallHeight = Mathf.Clamp(height, 1f, 6f);
        wallThickness = Mathf.Clamp(thickness, .05f, .6f);
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
