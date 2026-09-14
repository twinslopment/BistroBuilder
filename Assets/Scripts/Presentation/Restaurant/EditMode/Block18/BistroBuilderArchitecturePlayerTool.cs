using System;
using UnityEngine;

public enum BistroBuilderArchitecturePlayerToolMode { None = 0, Wall = 1, RoomRectangle = 2 }
public enum BistroBuilderArchitectureRoomPurpose { Dining = 0, Kitchen = 1, Bathroom = 2 }

/// <summary>Compatibility adapter for saved scenes and the initial-design flow.
/// All input, preview and commands belong to the single construction runtime.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderArchitecturePlayerTool : MonoBehaviour
{
    private BistroBuilderConstructionAuthoringRuntimeTool tool;
    private BistroBuilderArchitectureRoomPurpose purpose;
    public event Action Changed;
    private BistroBuilderConstructionAuthoringRuntimeTool Tool
    {
        get
        {
            if (tool == null) tool = FindFirstObjectByType<BistroBuilderConstructionAuthoringRuntimeTool>();
            return tool;
        }
    }
    public BistroBuilderArchitecturePlayerToolMode Mode => Tool == null ? BistroBuilderArchitecturePlayerToolMode.None :
        Tool.Mode == BistroBuilderConstructionRuntimeMode.Wall ? BistroBuilderArchitecturePlayerToolMode.Wall :
        Tool.Mode == BistroBuilderConstructionRuntimeMode.Room ? BistroBuilderArchitecturePlayerToolMode.RoomRectangle : BistroBuilderArchitecturePlayerToolMode.None;
    public BistroBuilderArchitectureRoomPurpose RoomPurpose => purpose;
    public bool HasFirstPoint => Tool != null && Tool.HasActiveGesture;
    public string StatusMessage => Tool != null ? Tool.StatusMessage : string.Empty;
    public bool HasDraftSession => Tool != null && Tool.HasDraftSession;
    public bool HasDraftChanges => Tool != null && Tool.HasDraftChanges;
    public bool CanUndo => Tool != null && Tool.CanUndo;
    public bool CanRedo => Tool != null && Tool.CanRedo;
    public int DraftWallCount => Tool != null ? Tool.WallCount : 0;
    public int DraftRoomCount => Tool != null ? Tool.RoomCount : 0;
    public void SetMode(BistroBuilderArchitecturePlayerToolMode mode)
    {
        Tool?.SetMode(mode == BistroBuilderArchitecturePlayerToolMode.Wall ? BistroBuilderConstructionRuntimeMode.Wall :
            mode == BistroBuilderArchitecturePlayerToolMode.RoomRectangle ? BistroBuilderConstructionRuntimeMode.Room : BistroBuilderConstructionRuntimeMode.Furniture);
        Changed?.Invoke();
    }
    public void SetRoomMode(BistroBuilderArchitectureRoomPurpose next)
    {
        purpose = next;
        Tool?.SetRoomZone(next == BistroBuilderArchitectureRoomPurpose.Kitchen ? "zone.kitchen" :
            next == BistroBuilderArchitectureRoomPurpose.Bathroom ? "zone.bathroom" : "zone.dining");
        Changed?.Invoke();
    }
    public bool TryUndo(out string error) { error = string.Empty; return Tool != null && Tool.TryUndo(out error); }
    public bool TryRedo(out string error) { error = string.Empty; return Tool != null && Tool.TryRedo(out error); }
    public bool TryCommitDraft(out string error) { error = string.Empty; return Tool == null || Tool.TryCommitDraft(out error); }
    public bool TryCancelDraft(out string error) { error = string.Empty; return Tool == null || Tool.TryCancelDraft(out error); }
}