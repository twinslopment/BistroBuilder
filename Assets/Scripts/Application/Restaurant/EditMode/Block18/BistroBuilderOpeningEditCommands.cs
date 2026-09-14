using System;

public sealed class BistroBuilderDeleteOpeningCommand : BistroBuilderEditCommandBase
{
    private readonly BistroBuilderEditId openingId;
    private BistroBuilderOpeningRecord removed;
    public override string Description => "Eliminar opening";

    public BistroBuilderDeleteOpeningCommand(BistroBuilderEditId openingId)
    {
        this.openingId = openingId;
    }

    public override bool TryExecute(BistroBuilderEditDocument document,
        out BistroBuilderEditChangeSet changeSet, out string error)
    {
        changeSet = new BistroBuilderEditChangeSet();
        error = string.Empty;
        if (document == null || !openingId.IsValid)
        {
            error = "Opening inválido.";
            return false;
        }
        BistroBuilderOpeningRecord current = document.FindOpening(openingId);
        if (current == null)
        {
            error = "El opening no existe.";
            return false;
        }
        removed = current.DeepClone();
        for (int i = document.openings.Count - 1; i >= 0; i--)
        {
            if (document.openings[i] != null && document.openings[i].openingId == openingId)
            {
                document.openings.RemoveAt(i);
                break;
            }
        }
        changeSet.removed.Add(openingId);
        changeSet.affected.entityIds.Add(openingId);
        changeSet.affected.entityIds.Add(removed.hostWallId);
        return true;
    }

    public override bool TryUndo(BistroBuilderEditDocument document, out string error)
    {
        error = string.Empty;
        if (document == null || removed == null || document.FindOpening(openingId) != null ||
            document.FindWall(removed.hostWallId) == null)
        {
            error = "No se puede restaurar el opening.";
            return false;
        }
        document.openings.Add(removed.DeepClone());
        return true;
    }

    public override bool TryRedo(BistroBuilderEditDocument document, out string error)
    {
        error = string.Empty;
        if (document == null || removed == null || document.FindOpening(openingId) == null)
        {
            error = "No se puede rehacer la eliminación del opening.";
            return false;
        }
        for (int i = document.openings.Count - 1; i >= 0; i--)
        {
            if (document.openings[i] != null && document.openings[i].openingId == openingId)
            {
                document.openings.RemoveAt(i);
                return true;
            }
        }
        error = "El opening no existe.";
        return false;
    }
}
