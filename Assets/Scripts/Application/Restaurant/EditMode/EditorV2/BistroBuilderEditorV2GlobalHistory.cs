using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// B4: global chronological history for Editor V2.
///
/// This class does not replace either specialist history. It records only
/// ordering metadata and delegates Undo/Redo back to the existing Placement
/// and Architecture authorities. Therefore validation, Finance participants,
/// IDs, resource ownership and Draft semantics remain in their current owners.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Editor V2/Global History")]
public sealed class BistroBuilderEditorV2GlobalHistory : MonoBehaviour
{
    private enum Source
    {
        Placement = 0,
        Architecture = 1
    }

    private sealed class Entry
    {
        public Source source;
        public IRestaurantEditHistoryCommand placementCommand;
        public IBistroBuilderEditCommand architectureCommand;

        public string Description =>
            source == Source.Placement
                ? placementCommand?.Description ?? "Mobiliario"
                : architectureCommand?.Description ?? "Arquitectura";
    }

    [SerializeField] private RestaurantPlacementHistoryService placementHistory;
    [SerializeField] private BistroBuilderEditRuntimeCoordinator architectureRuntime;
    [SerializeField, Min(1)] private int maximumEntries = 50;

    private readonly List<Entry> undo = new List<Entry>(50);
    private readonly List<Entry> redo = new List<Entry>(50);
    private bool subscribed;
    private bool executingGlobalOperation;

    public event Action HistoryChanged;

    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public int UndoCount => undo.Count;
    public int RedoCount => redo.Count;
    public int MaximumEntries => Mathf.Max(1, maximumEntries);
    public string NextUndoDescription => CanUndo ? undo[undo.Count - 1].Description : string.Empty;
    public string NextRedoDescription => CanRedo ? redo[redo.Count - 1].Description : string.Empty;

    public int SetMaximumEntriesRuntime(int value)
    {
        int previous = MaximumEntries;
        maximumEntries = Mathf.Max(1, value);
        Trim(undo);
        Trim(redo);
        return previous;
    }

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void Configure(
        RestaurantPlacementHistoryService placement,
        BistroBuilderEditRuntimeCoordinator architecture)
    {
        bool sameAuthorities =
            ReferenceEquals(placementHistory, placement) &&
            ReferenceEquals(architectureRuntime, architecture);

        if (sameAuthorities)
        {
            Subscribe();
            return;
        }

        Unsubscribe();
        placementHistory = placement;
        architectureRuntime = architecture;
        Subscribe();
        ClearGlobalOrdering();
    }

    public bool TryUndo(out string error)
    {
        error = string.Empty;
        if (undo.Count == 0)
        {
            error = "No hay operación global que deshacer.";
            return false;
        }

        Entry entry = undo[undo.Count - 1];
        if (!TryVerifyTop(entry, true, out error))
            return false;

        executingGlobalOperation = true;
        bool ok;
        try
        {
            if (entry.source == Source.Placement)
            {
                ok = placementHistory.TryUndo(
                    out _,
                    out RestaurantPlacementHistoryFailureReason reason,
                    out _);
                if (!ok)
                    error = "Placement rechazó Undo global: " + reason + ".";
            }
            else
            {
                ok = architectureRuntime.TryUndo(out error);
            }
        }
        finally
        {
            executingGlobalOperation = false;
        }

        if (!ok)
            return false;

        undo.RemoveAt(undo.Count - 1);
        redo.Add(entry);
        Trim(redo);
        HistoryChanged?.Invoke();
        return true;
    }

    public bool TryRedo(out string error)
    {
        error = string.Empty;
        if (redo.Count == 0)
        {
            error = "No hay operación global que rehacer.";
            return false;
        }

        Entry entry = redo[redo.Count - 1];
        if (!TryVerifyTop(entry, false, out error))
            return false;

        executingGlobalOperation = true;
        bool ok;
        try
        {
            if (entry.source == Source.Placement)
            {
                ok = placementHistory.TryRedo(
                    out _,
                    out RestaurantPlacementHistoryFailureReason reason,
                    out _);
                if (!ok)
                    error = "Placement rechazó Redo global: " + reason + ".";
            }
            else
            {
                ok = architectureRuntime.TryRedo(out error);
            }
        }
        finally
        {
            executingGlobalOperation = false;
        }

        if (!ok)
            return false;

        redo.RemoveAt(redo.Count - 1);
        undo.Add(entry);
        Trim(undo);
        HistoryChanged?.Invoke();
        return true;
    }

    public void ClearGlobalOrdering()
    {
        bool changed = undo.Count > 0 || redo.Count > 0;
        undo.Clear();
        redo.Clear();
        if (changed)
            HistoryChanged?.Invoke();
    }

    private void HandlePlacementRecorded(IRestaurantEditHistoryCommand command)
    {
        if (executingGlobalOperation || command == null)
            return;

        InvalidateRedoFromNewCommand(Source.Placement);
        undo.Add(new Entry
        {
            source = Source.Placement,
            placementCommand = command
        });
        Trim(undo);
        HistoryChanged?.Invoke();
    }

    private void HandleArchitectureExecuted(IBistroBuilderEditCommand command)
    {
        if (executingGlobalOperation || command == null)
            return;

        InvalidateRedoFromNewCommand(Source.Architecture);
        undo.Add(new Entry
        {
            source = Source.Architecture,
            architectureCommand = command
        });
        Trim(undo);
        HistoryChanged?.Invoke();
    }

    private void InvalidateRedoFromNewCommand(Source source)
    {
        if (redo.Count == 0)
            return;

        // A new command creates one new timeline. The other specialist cannot
        // retain a redo branch that is no longer reachable globally.
        if (source != Source.Placement)
            placementHistory?.DiscardRedoHistory();

        if (source != Source.Architecture &&
            architectureRuntime != null &&
            architectureRuntime.Session != null)
        {
            architectureRuntime.Session.DiscardRedoHistory();
        }

        redo.Clear();
    }

    private bool TryVerifyTop(Entry entry, bool undoDirection, out string error)
    {
        error = string.Empty;

        if (entry.source == Source.Placement)
        {
            if (placementHistory == null)
            {
                error = "Falta el historial de Placement.";
                return false;
            }

            IRestaurantEditHistoryCommand actual = undoDirection
                ? placementHistory.PeekUndoCommand()
                : placementHistory.PeekRedoCommand();

            if (!ReferenceEquals(actual, entry.placementCommand))
            {
                error = "El historial global y Placement han divergido.";
                return false;
            }

            return true;
        }

        if (architectureRuntime == null || architectureRuntime.Session == null)
        {
            error = "Falta la sesión arquitectónica.";
            return false;
        }

        IBistroBuilderEditCommand architectureActual = undoDirection
            ? architectureRuntime.Session.PeekUndoCommand()
            : architectureRuntime.Session.PeekRedoCommand();

        if (!ReferenceEquals(architectureActual, entry.architectureCommand))
        {
            error = "El historial global y Construction/Surfaces han divergido.";
            return false;
        }

        return true;
    }

    private void Trim(List<Entry> stack)
    {
        int limit = Mathf.Max(1, maximumEntries);
        int overflow = stack.Count - limit;
        if (overflow > 0)
            stack.RemoveRange(0, overflow);
    }

    private void Subscribe()
    {
        if (subscribed)
            return;

        if (placementHistory != null)
            placementHistory.CommandRecorded += HandlePlacementRecorded;

        if (architectureRuntime != null)
            architectureRuntime.CommandExecuted += HandleArchitectureExecuted;

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (placementHistory != null)
            placementHistory.CommandRecorded -= HandlePlacementRecorded;

        if (architectureRuntime != null)
            architectureRuntime.CommandExecuted -= HandleArchitectureExecuted;

        subscribed = false;
    }

    private void CacheDependencies()
    {
        if (placementHistory == null)
            placementHistory = FindFirstObjectByType<RestaurantPlacementHistoryService>(
                FindObjectsInactive.Include);

        if (architectureRuntime == null)
            architectureRuntime = FindFirstObjectByType<BistroBuilderEditRuntimeCoordinator>(
                FindObjectsInactive.Include);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maximumEntries = Mathf.Max(1, maximumEntries);
    }
#endif
}
