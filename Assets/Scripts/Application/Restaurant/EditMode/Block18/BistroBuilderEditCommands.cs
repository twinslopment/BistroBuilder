using System;
using System.Collections.Generic;

public abstract class BistroBuilderEditCommandBase : IBistroBuilderEditCommand
{
    public string CommandId { get; } = Guid.NewGuid().ToString("N");
    public abstract string Description { get; }
    public abstract bool TryExecute(BistroBuilderEditDocument document, out BistroBuilderEditChangeSet changeSet, out string error);
    public abstract bool TryUndo(BistroBuilderEditDocument document, out string error);
    public abstract bool TryRedo(BistroBuilderEditDocument document, out string error);
}

public sealed class BistroBuilderCreateWallCommand : BistroBuilderEditCommandBase
{
    private readonly BistroBuilderWallRecord wall;
    public override string Description => "Crear pared";
    public BistroBuilderCreateWallCommand(BistroBuilderWallRecord wall)
    { this.wall = wall != null ? wall.DeepClone() : null; }

    public override bool TryExecute(BistroBuilderEditDocument document, out BistroBuilderEditChangeSet changeSet, out string error)
    {
        changeSet = new BistroBuilderEditChangeSet(); error = string.Empty;
        if (document == null || wall == null || !wall.wallId.IsValid) { error = "Pared inválida."; return false; }
        if (document.FindWall(wall.wallId) != null) { error = "WallId duplicado."; return false; }
        document.walls.Add(wall.DeepClone()); changeSet.created.Add(wall.wallId); changeSet.affected.entityIds.Add(wall.wallId); return true;
    }
    public override bool TryUndo(BistroBuilderEditDocument document, out string error) => Remove(document, out error);
    public override bool TryRedo(BistroBuilderEditDocument document, out string error)
    { error = string.Empty; if (document.FindWall(wall.wallId) != null) { error = "WallId ya existe."; return false; } document.walls.Add(wall.DeepClone()); return true; }
    private bool Remove(BistroBuilderEditDocument document, out string error)
    {
        error = string.Empty;
        for (int i = 0; i < document.walls.Count; i++)
            if (document.walls[i] != null && document.walls[i].wallId == wall.wallId)
            { document.walls.RemoveAt(i); return true; }
        error = "La pared ya no existe."; return false;
    }
}

public sealed class BistroBuilderUpdateWallCommand : BistroBuilderEditCommandBase
{
    private readonly BistroBuilderWallRecord desired;
    private BistroBuilderWallRecord before;
    public override string Description => "Modificar pared";
    public BistroBuilderUpdateWallCommand(BistroBuilderWallRecord desired)
    { this.desired = desired != null ? desired.DeepClone() : null; }

    public override bool TryExecute(BistroBuilderEditDocument document, out BistroBuilderEditChangeSet changeSet, out string error)
    {
        changeSet = new BistroBuilderEditChangeSet(); error = string.Empty;
        var current = desired != null ? document.FindWall(desired.wallId) : null;
        if (current == null) { error = "La pared no existe."; return false; }
        before = current.DeepClone(); Replace(document, desired);
        changeSet.modified.Add(desired.wallId); changeSet.affected.entityIds.Add(desired.wallId); return true;
    }
    public override bool TryUndo(BistroBuilderEditDocument document, out string error)
    { error = string.Empty; if (before == null || document.FindWall(before.wallId) == null) { error = "No se puede restaurar la pared."; return false; } Replace(document, before); return true; }
    public override bool TryRedo(BistroBuilderEditDocument document, out string error)
    { error = string.Empty; if (desired == null || document.FindWall(desired.wallId) == null) { error = "No se puede rehacer la pared."; return false; } Replace(document, desired); return true; }
    private static void Replace(BistroBuilderEditDocument document, BistroBuilderWallRecord replacement)
    {
        for (int i = 0; i < document.walls.Count; i++)
            if (document.walls[i] != null && document.walls[i].wallId == replacement.wallId)
            { document.walls[i] = replacement.DeepClone(); return; }
    }
}

public sealed class BistroBuilderCreateOpeningCommand : BistroBuilderEditCommandBase
{
    private readonly BistroBuilderOpeningRecord opening;
    public override string Description => "Crear opening";
    public BistroBuilderCreateOpeningCommand(BistroBuilderOpeningRecord opening)
    { this.opening = opening != null ? opening.DeepClone() : null; }
    public override bool TryExecute(BistroBuilderEditDocument document, out BistroBuilderEditChangeSet changeSet, out string error)
    {
        changeSet = new BistroBuilderEditChangeSet(); error = string.Empty;
        if (opening == null || !opening.openingId.IsValid || document.FindOpening(opening.openingId) != null)
        { error = "Opening inválido o duplicado."; return false; }
        if (document.FindWall(opening.hostWallId) == null) { error = "HostWallId inexistente."; return false; }
        document.openings.Add(opening.DeepClone()); changeSet.created.Add(opening.openingId);
        changeSet.affected.entityIds.Add(opening.openingId); changeSet.affected.entityIds.Add(opening.hostWallId); return true;
    }
    public override bool TryUndo(BistroBuilderEditDocument document, out string error)
    { error = string.Empty; for (int i=0;i<document.openings.Count;i++) if(document.openings[i].openingId==opening.openingId){document.openings.RemoveAt(i);return true;} error="Opening inexistente.";return false; }
    public override bool TryRedo(BistroBuilderEditDocument document, out string error)
    { error=string.Empty;if(document.FindOpening(opening.openingId)!=null){error="Opening ya existe.";return false;}document.openings.Add(opening.DeepClone());return true; }
}
public sealed class BistroBuilderDeleteWallCommand : BistroBuilderEditCommandBase
{
    private readonly BistroBuilderEditId wallId;
    private BistroBuilderWallRecord removedWall;
    private readonly List<BistroBuilderOpeningRecord> removedOpenings = new List<BistroBuilderOpeningRecord>();
    public override string Description => "Eliminar pared";
    public BistroBuilderDeleteWallCommand(BistroBuilderEditId wallId) { this.wallId = wallId; }

    public override bool TryExecute(BistroBuilderEditDocument document, out BistroBuilderEditChangeSet changeSet, out string error)
    {
        changeSet = new BistroBuilderEditChangeSet(); error = string.Empty;
        var wall = document.FindWall(wallId); if (wall == null) { error = "La pared no existe."; return false; }
        removedWall = wall.DeepClone(); removedOpenings.Clear();
        for (int i = document.openings.Count - 1; i >= 0; i--)
        {
            var o = document.openings[i]; if (o == null || o.hostWallId != wallId) continue;
            removedOpenings.Add(o.DeepClone()); changeSet.removed.Add(o.openingId); document.openings.RemoveAt(i);
        }
        for (int i = document.walls.Count - 1; i >= 0; i--)
            if (document.walls[i] != null && document.walls[i].wallId == wallId) { document.walls.RemoveAt(i); break; }
        changeSet.removed.Add(wallId); changeSet.affected.entityIds.Add(wallId); return true;
    }

    public override bool TryUndo(BistroBuilderEditDocument document, out string error)
    {
        error = string.Empty; if (removedWall == null || document.FindWall(wallId) != null) { error = "No se puede restaurar la pared."; return false; }
        document.walls.Add(removedWall.DeepClone());
        for (int i = 0; i < removedOpenings.Count; i++) document.openings.Add(removedOpenings[i].DeepClone());
        return true;
    }
    public override bool TryRedo(BistroBuilderEditDocument document, out string error)
    {
        error = string.Empty; if (document.FindWall(wallId) == null) { error = "La pared no existe para rehacer su eliminación."; return false; }
        for (int i = document.openings.Count - 1; i >= 0; i--)
            if (document.openings[i] != null && document.openings[i].hostWallId == wallId) document.openings.RemoveAt(i);
        for (int i = document.walls.Count - 1; i >= 0; i--)
            if (document.walls[i] != null && document.walls[i].wallId == wallId) { document.walls.RemoveAt(i); return true; }
        error = "No se pudo eliminar la pared."; return false;
    }
}
