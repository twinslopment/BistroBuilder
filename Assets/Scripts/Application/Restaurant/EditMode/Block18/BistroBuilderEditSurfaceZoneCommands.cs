using System;

public sealed class BistroBuilderApplySurfaceFinishCommand : BistroBuilderEditCommandBase
{
    private readonly BistroBuilderSurfaceFinishPatchRecord desired;
    private BistroBuilderSurfaceFinishPatchRecord previous;
    private bool existed;
    public override string Description => "Aplicar acabado de superficie";

    public BistroBuilderApplySurfaceFinishCommand(BistroBuilderSurfaceFinishPatchRecord patch)
    { desired = patch != null ? patch.DeepClone() : null; }

    public override bool TryExecute(BistroBuilderEditDocument document, out BistroBuilderEditChangeSet changeSet, out string error)
    {
        changeSet = new BistroBuilderEditChangeSet(); error = string.Empty;
        if (document == null || desired == null || !desired.surfacePatchId.IsValid || desired.fallbackBoundary.Count < 3)
        { error = "SurfaceFinishPatch inválido."; return false; }
        int index = Find(document, desired.surfacePatchId);
        existed = index >= 0;
        if (existed) { previous = document.surfaces[index].DeepClone(); document.surfaces[index] = desired.DeepClone(); changeSet.modified.Add(desired.surfacePatchId); }
        else { document.surfaces.Add(desired.DeepClone()); changeSet.created.Add(desired.surfacePatchId); }
        changeSet.affected.entityIds.Add(desired.surfacePatchId); return true;
    }

    public override bool TryUndo(BistroBuilderEditDocument document, out string error)
    {
        error = string.Empty; int index = Find(document, desired.surfacePatchId);
        if (index < 0) { error = "SurfaceFinishPatch no disponible."; return false; }
        if (existed) document.surfaces[index] = previous.DeepClone(); else document.surfaces.RemoveAt(index);
        return true;
    }
    public override bool TryRedo(BistroBuilderEditDocument document, out string error)
    {
        error = string.Empty; int index = Find(document, desired.surfacePatchId);
        if (existed)
        {
            if (index < 0) { error = "SurfaceFinishPatch no disponible."; return false; }
            document.surfaces[index] = desired.DeepClone(); return true;
        }
        if (index >= 0) { error = "SurfaceFinishPatch ya existe."; return false; }
        document.surfaces.Add(desired.DeepClone()); return true;
    }

    private static int Find(BistroBuilderEditDocument document, BistroBuilderEditId id)
    {
        for (int i = 0; i < document.surfaces.Count; i++)
            if (document.surfaces[i] != null && document.surfaces[i].surfacePatchId == id) return i;
        return -1;
    }
}

public sealed class BistroBuilderSetFunctionalZoneCommand : BistroBuilderEditCommandBase
{
    private readonly BistroBuilderFunctionalZoneRecord desired;
    private BistroBuilderFunctionalZoneRecord previous;
    private bool existed;
    public override string Description => "Definir zona funcional";
    public BistroBuilderSetFunctionalZoneCommand(BistroBuilderFunctionalZoneRecord zone)
    { desired = zone != null ? zone.DeepClone() : null; }
    public override bool TryExecute(BistroBuilderEditDocument document, out BistroBuilderEditChangeSet changeSet, out string error)
    {
        changeSet = new BistroBuilderEditChangeSet(); error = string.Empty;
        if (document == null || desired == null || !desired.zoneId.IsValid || string.IsNullOrWhiteSpace(desired.zoneDefinitionId))
        { error = "FunctionalZone inválida."; return false; }
        int index = Find(document, desired.zoneId); existed = index >= 0;
        if (existed) { previous = document.zones[index].DeepClone(); document.zones[index] = desired.DeepClone(); changeSet.modified.Add(desired.zoneId); }
        else { document.zones.Add(desired.DeepClone()); changeSet.created.Add(desired.zoneId); }
        changeSet.affected.entityIds.Add(desired.zoneId); return true;
    }

    public override bool TryUndo(BistroBuilderEditDocument document, out string error)
    {
        error = string.Empty; int index = Find(document, desired.zoneId);
        if (index < 0) { error = "FunctionalZone no disponible."; return false; }
        if (existed) document.zones[index] = previous.DeepClone(); else document.zones.RemoveAt(index);
        return true;
    }

    public override bool TryRedo(BistroBuilderEditDocument document, out string error)
    {
        error = string.Empty; int index = Find(document, desired.zoneId);
        if (existed)
        {
            if (index < 0) { error = "FunctionalZone no disponible."; return false; }
            document.zones[index] = desired.DeepClone(); return true;
        }
        if (index >= 0) { error = "FunctionalZone ya existe."; return false; }
        document.zones.Add(desired.DeepClone()); return true;
    }
    private static int Find(BistroBuilderEditDocument document, BistroBuilderEditId id)
    {
        for (int i = 0; i < document.zones.Count; i++)
            if (document.zones[i] != null && document.zones[i].zoneId == id) return i;
        return -1;
    }
}
