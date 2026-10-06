using System;
using System.Collections.Generic;
using UnityEngine;

public enum BistroBuilderEditorV2SelectionKind
{
    None = 0,
    Furniture = 1,
    Wall = 2,
    Opening = 3,
    Room = 4,
    Surface = 5
}

[Flags]
public enum BistroBuilderEditorV2SelectionCapability
{
    None = 0,
    Inspect = 1 << 0,
    Move = 1 << 1,
    Rotate = 1 << 2,
    Delete = 1 << 3,
    Duplicate = 1 << 4,
    ApplySurface = 1 << 5
}

[Serializable]
public struct BistroBuilderEditorV2Selection
{
    public BistroBuilderEditorV2ToolFamily family;
    public BistroBuilderEditorV2SelectionKind kind;
    public string stableId;
    public string displayName;
    public BistroBuilderEditorV2SelectionCapability capabilities;
    public bool persistentIdentity;

    public bool IsValid =>
        kind != BistroBuilderEditorV2SelectionKind.None &&
        family != BistroBuilderEditorV2ToolFamily.None &&
        !string.IsNullOrWhiteSpace(stableId);

    public bool Supports(BistroBuilderEditorV2SelectionCapability capability) =>
        IsValid && (capabilities & capability) == capability;

    public static BistroBuilderEditorV2Selection None =>
        new BistroBuilderEditorV2Selection
        {
            family = BistroBuilderEditorV2ToolFamily.None,
            kind = BistroBuilderEditorV2SelectionKind.None,
            stableId = string.Empty,
            displayName = string.Empty,
            capabilities = BistroBuilderEditorV2SelectionCapability.None,
            persistentIdentity = false
        };
}

public interface IBistroBuilderEditorV2SelectionSource
{
    BistroBuilderEditorV2ToolFamily Family { get; }
    bool TryReadSelection(out BistroBuilderEditorV2Selection selection);
    bool ClearSelection();
}

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Editor V2/Selection Coordinator")]
public sealed class BistroBuilderEditorV2SelectionCoordinator : MonoBehaviour
{
    [SerializeField] private RestaurantEditModeService editModeService;
    [SerializeField] private BistroBuilderEditorV2Coordinator editorCoordinator;
    [Tooltip("Optional explicit selection-source list. Empty = discover scene sources.")]
    [SerializeField] private MonoBehaviour[] sourceBehaviours = Array.Empty<MonoBehaviour>();

    private readonly Dictionary<BistroBuilderEditorV2ToolFamily, IBistroBuilderEditorV2SelectionSource>
        sources = new Dictionary<BistroBuilderEditorV2ToolFamily, IBistroBuilderEditorV2SelectionSource>();

    private BistroBuilderEditorV2Selection current = BistroBuilderEditorV2Selection.None;
    private BistroBuilderEditorV2ToolFamily observedFamily = BistroBuilderEditorV2ToolFamily.None;
    private long revision;

    public event Action<BistroBuilderEditorV2Selection> SelectionChanged;

    public BistroBuilderEditorV2Selection Current => current;
    public bool HasSelection => current.IsValid;
    public int RegisteredSourceCount => sources.Count;
    public long Revision => revision;

    private void Awake()
    {
        CacheDependencies();
        RebuildSourceRegistry(out _);
        observedFamily = editorCoordinator != null
            ? editorCoordinator.ActiveFamily
            : BistroBuilderEditorV2ToolFamily.None;
    }

    private void OnEnable()
    {
        CacheDependencies();
        RebuildSourceRegistry(out _);
        Subscribe();
        observedFamily = editorCoordinator != null
            ? editorCoordinator.ActiveFamily
            : BistroBuilderEditorV2ToolFamily.None;
        RefreshSelection();
    }

    private void OnDisable()
    {
        Unsubscribe();
        observedFamily = BistroBuilderEditorV2ToolFamily.None;
        SetCurrent(BistroBuilderEditorV2Selection.None);
    }

    private void LateUpdate()
    {
        if (editModeService != null && editModeService.IsEditModeActive)
            RefreshSelection();
    }

    public void Configure(
        RestaurantEditModeService service,
        BistroBuilderEditorV2Coordinator coordinator,
        params MonoBehaviour[] sourcesToUse)
    {
        Unsubscribe();
        editModeService = service;
        editorCoordinator = coordinator;
        sourceBehaviours = sourcesToUse ?? Array.Empty<MonoBehaviour>();
        RebuildSourceRegistry(out _);
        Subscribe();
        observedFamily = editorCoordinator != null
            ? editorCoordinator.ActiveFamily
            : BistroBuilderEditorV2ToolFamily.None;
        RefreshSelection();
    }

    public bool RebuildSourceRegistry(out string error)
    {
        error = string.Empty;
        sources.Clear();

        MonoBehaviour[] candidates = sourceBehaviours;
        if (candidates == null || candidates.Length == 0)
        {
            candidates = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            if (!(candidates[i] is IBistroBuilderEditorV2SelectionSource source))
                continue;
            if (source.Family == BistroBuilderEditorV2ToolFamily.None)
                continue;
            if (sources.ContainsKey(source.Family))
            {
                error = "Existe más de una fuente de selección Editor V2 para " +
                    source.Family + ".";
                sources.Clear();
                return false;
            }

            sources.Add(source.Family, source);
        }

        return true;
    }

    public bool RefreshSelection()
    {
        CacheDependencies();

        if (editModeService == null ||
            !editModeService.IsEditModeActive ||
            editorCoordinator == null ||
            editorCoordinator.ActiveFamily == BistroBuilderEditorV2ToolFamily.None ||
            !sources.TryGetValue(editorCoordinator.ActiveFamily, out IBistroBuilderEditorV2SelectionSource source) ||
            source == null ||
            !source.TryReadSelection(out BistroBuilderEditorV2Selection next) ||
            !next.IsValid)
        {
            return SetCurrent(BistroBuilderEditorV2Selection.None);
        }

        return SetCurrent(next);
    }

    public bool TryClearCurrentSelection()
    {
        if (editorCoordinator == null ||
            !sources.TryGetValue(editorCoordinator.ActiveFamily, out IBistroBuilderEditorV2SelectionSource source) ||
            source == null)
        {
            return SetCurrent(BistroBuilderEditorV2Selection.None);
        }

        bool cleared = source.ClearSelection();
        bool changed = RefreshSelection();
        return cleared || changed;
    }

    private void HandleCoordinatorStateChanged(BistroBuilderEditorV2Snapshot snapshot)
    {
        BistroBuilderEditorV2ToolFamily nextFamily =
            snapshot != null && snapshot.editModeActive
                ? snapshot.activeFamily
                : BistroBuilderEditorV2ToolFamily.None;

        if (nextFamily != observedFamily)
        {
            BistroBuilderEditorV2ToolFamily previousFamily = observedFamily;
            ClearSelectionWhenLeavingAuthority(previousFamily, nextFamily);
            observedFamily = nextFamily;
            ClearInvalidArchitectureSelectionAfterFamilyChange(previousFamily, nextFamily);
        }

        if (nextFamily == BistroBuilderEditorV2ToolFamily.None)
            SetCurrent(BistroBuilderEditorV2Selection.None);
        else
            RefreshSelection();
    }

    private void HandleEditModeExited()
    {
        ClearAllSpecialistSelections();
        observedFamily = BistroBuilderEditorV2ToolFamily.None;
        SetCurrent(BistroBuilderEditorV2Selection.None);
    }

    private void ClearSelectionWhenLeavingAuthority(
        BistroBuilderEditorV2ToolFamily previous,
        BistroBuilderEditorV2ToolFamily next)
    {
        if (previous == BistroBuilderEditorV2ToolFamily.None)
            return;

        bool previousArchitecture = IsArchitectureFamily(previous);
        bool nextArchitecture = IsArchitectureFamily(next);

        if ((previous == BistroBuilderEditorV2ToolFamily.Furniture &&
             next != BistroBuilderEditorV2ToolFamily.Furniture) ||
            (previousArchitecture && !nextArchitecture))
        {
            if (sources.TryGetValue(previous, out IBistroBuilderEditorV2SelectionSource source))
                source?.ClearSelection();
        }
    }

    private void ClearInvalidArchitectureSelectionAfterFamilyChange(
        BistroBuilderEditorV2ToolFamily previous,
        BistroBuilderEditorV2ToolFamily next)
    {
        if (!IsArchitectureFamily(previous) || !IsArchitectureFamily(next) || previous == next)
            return;

        if (!sources.TryGetValue(next, out IBistroBuilderEditorV2SelectionSource nextSource) ||
            nextSource == null)
            return;

        if (!nextSource.TryReadSelection(out BistroBuilderEditorV2Selection projected) ||
            !projected.IsValid)
        {
            nextSource.ClearSelection();
        }
    }

    private void ClearAllSpecialistSelections()
    {
        var visited = new HashSet<IBistroBuilderEditorV2SelectionSource>();
        foreach (IBistroBuilderEditorV2SelectionSource source in sources.Values)
        {
            if (source != null && visited.Add(source))
                source.ClearSelection();
        }
    }

    private bool SetCurrent(BistroBuilderEditorV2Selection next)
    {
        if (Equivalent(current, next))
            return false;

        current = next;
        revision++;
        SelectionChanged?.Invoke(current);
        return true;
    }

    private static bool Equivalent(
        BistroBuilderEditorV2Selection a,
        BistroBuilderEditorV2Selection b)
    {
        return a.family == b.family &&
               a.kind == b.kind &&
               string.Equals(a.stableId ?? string.Empty, b.stableId ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(a.displayName ?? string.Empty, b.displayName ?? string.Empty, StringComparison.Ordinal) &&
               a.capabilities == b.capabilities &&
               a.persistentIdentity == b.persistentIdentity;
    }

    private static bool IsArchitectureFamily(BistroBuilderEditorV2ToolFamily family) =>
        family == BistroBuilderEditorV2ToolFamily.Construction ||
        family == BistroBuilderEditorV2ToolFamily.Surfaces;

    private void CacheDependencies()
    {
        if (editModeService == null)
            editModeService = FindFirstObjectByType<RestaurantEditModeService>(
                FindObjectsInactive.Include);
        if (editorCoordinator == null)
            editorCoordinator = FindFirstObjectByType<BistroBuilderEditorV2Coordinator>(
                FindObjectsInactive.Include);
    }

    private void Subscribe()
    {
        if (editorCoordinator != null)
        {
            editorCoordinator.StateChanged -= HandleCoordinatorStateChanged;
            editorCoordinator.StateChanged += HandleCoordinatorStateChanged;
        }
        if (editModeService != null)
        {
            editModeService.EditModeExited -= HandleEditModeExited;
            editModeService.EditModeExited += HandleEditModeExited;
        }
    }

    private void Unsubscribe()
    {
        if (editorCoordinator != null)
            editorCoordinator.StateChanged -= HandleCoordinatorStateChanged;
        if (editModeService != null)
            editModeService.EditModeExited -= HandleEditModeExited;
    }
}
