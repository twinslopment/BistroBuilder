using System;
using UnityEngine;

public enum BistroBuilderEditorV2ToolFamily
{
    None = 0,
    Furniture = 1,
    Construction = 2,
    Surfaces = 3
}

public enum BistroBuilderEditorV2OperationState
{
    Inactive = 0,
    Ready = 1,
    ToolActive = 2,
    OperationActive = 3
}

[Serializable]
public sealed class BistroBuilderEditorV2Snapshot
{
    public bool editModeActive;
    public BistroBuilderEditorV2OperationState state;
    public BistroBuilderEditorV2ToolFamily activeFamily;
    public string activeToolId = string.Empty;
    public bool hasActiveOperation;
    public int registeredAdapterCount;
    public long transitionSequence;
    public string lastTransition = string.Empty;
    public string lastError = string.Empty;
}

public interface IBistroBuilderEditorV2ToolAdapter
{
    BistroBuilderEditorV2ToolFamily Family { get; }
    bool IsAvailable { get; }
    bool IsActive { get; }
    bool HasActiveOperation { get; }
    string ActiveToolId { get; }

    bool TryActivate(string toolId, out string error);
    bool TryCancelActiveOperation(out string error);
    void Deactivate();
}
