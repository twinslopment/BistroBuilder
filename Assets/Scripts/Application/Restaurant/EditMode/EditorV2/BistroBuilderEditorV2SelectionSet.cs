using System;
using System.Collections.Generic;

/// <summary>
/// B8: estado de selección múltiple de Editor V2.
///
/// Mantiene separadas:
/// - la selección primaria, utilizada como ancla/pivote lógico;
/// - las selecciones explícitas realizadas por el jugador;
/// - la intersección de capacidades realmente disponible para el conjunto.
///
/// No modifica ninguna autoridad especializada ni ningún objeto del mundo.
/// </summary>
public sealed partial class BistroBuilderEditorV2SelectionCoordinator
{
    private readonly List<BistroBuilderEditorV2Selection> selectionSet =
        new List<BistroBuilderEditorV2Selection>(16);

    private readonly HashSet<string> selectionKeys =
        new HashSet<string>(StringComparer.Ordinal);

    private BistroBuilderEditorV2Selection primarySelection =
        BistroBuilderEditorV2Selection.None;

    private BistroBuilderEditorV2SelectionCapability aggregateCapabilities =
        BistroBuilderEditorV2SelectionCapability.None;

    private long selectionSetRevision;

    public event Action SelectionSetChanged;

    public IReadOnlyList<BistroBuilderEditorV2Selection> SelectionSet =>
        selectionSet;

    public BistroBuilderEditorV2Selection PrimarySelection =>
        primarySelection.IsValid
            ? primarySelection
            : (selectionSet.Count > 0
                ? selectionSet[0]
                : BistroBuilderEditorV2Selection.None);

    public int SelectionCount => selectionSet.Count;

    public bool HasMultipleSelection => selectionSet.Count > 1;

    public long SelectionSetRevision => selectionSetRevision;

    public BistroBuilderEditorV2SelectionCapability AggregateCapabilities =>
        aggregateCapabilities;

    public bool SelectionSetSupports(
        BistroBuilderEditorV2SelectionCapability capability)
    {
        return selectionSet.Count > 0 &&
               (aggregateCapabilities & capability) == capability;
    }

    public bool ContainsSelection(
        BistroBuilderEditorV2Selection selection)
    {
        return selection.IsValid &&
               selectionKeys.Contains(BuildSelectionKey(selection));
    }

    public bool ContainsSelection(
        BistroBuilderEditorV2ToolFamily family,
        BistroBuilderEditorV2SelectionKind kind,
        string stableId)
    {
        if (family == BistroBuilderEditorV2ToolFamily.None ||
            kind == BistroBuilderEditorV2SelectionKind.None ||
            string.IsNullOrWhiteSpace(stableId))
        {
            return false;
        }

        return selectionKeys.Contains(
            BuildSelectionKey(family, kind, stableId));
    }

    /// <summary>
    /// Adopta la selección común actual.
    ///
    /// additive=false reemplaza el conjunto.
    /// additive=true/toggle=true implementa Shift+selección.
    /// </summary>
    public bool AdoptCurrentSelection(
        bool additive,
        bool toggle,
        out string error)
    {
        if (!current.IsValid)
        {
            error = "No existe una selección común válida que adoptar.";
            return false;
        }

        if (!additive)
        {
            ReplaceSelectionSetInternal(current);
            error = string.Empty;
            return true;
        }

        if (selectionSet.Count > 0 &&
            selectionSet[0].family != current.family)
        {
            error =
                "La multiselección no puede mezclar autoridades de edición distintas.";
            return false;
        }

        string key = BuildSelectionKey(current);

        if (toggle && selectionKeys.Contains(key))
        {
            RemoveSelectionInternal(key);
            error = string.Empty;
            return true;
        }

        if (selectionKeys.Add(key))
        {
            selectionSet.Add(current);
        }
        else
        {
            ReplaceStoredSelection(key, current);
        }

        // El último elemento seleccionado explícitamente se convierte en
        // selección primaria, sin alterar el orden determinista del conjunto.
        primarySelection = current;
        RecalculateAggregateCapabilities();
        TouchSelectionSet();
        error = string.Empty;
        return true;
    }

    public bool ReplaceSelectionSet(
        IReadOnlyList<BistroBuilderEditorV2Selection> selections,
        string primaryStableId,
        out string error)
    {
        error = string.Empty;

        if (selections == null || selections.Count == 0)
        {
            return ClearSelectionSet();
        }

        BistroBuilderEditorV2ToolFamily family =
            BistroBuilderEditorV2ToolFamily.None;

        var next = new List<BistroBuilderEditorV2Selection>(selections.Count);
        var keys = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < selections.Count; i++)
        {
            BistroBuilderEditorV2Selection selection = selections[i];

            if (!selection.IsValid)
            {
                error = "El conjunto contiene una selección inválida.";
                return false;
            }

            if (family == BistroBuilderEditorV2ToolFamily.None)
            {
                family = selection.family;
            }
            else if (selection.family != family)
            {
                error =
                    "El conjunto contiene selecciones de autoridades distintas.";
                return false;
            }

            string key = BuildSelectionKey(selection);
            if (!keys.Add(key))
                continue;

            next.Add(selection);
        }

        if (next.Count == 0)
        {
            error = "El conjunto no contiene selecciones utilizables.";
            return false;
        }

        next.Sort(CompareSelectionsStable);

        selectionSet.Clear();
        selectionSet.AddRange(next);
        selectionKeys.Clear();

        for (int i = 0; i < selectionSet.Count; i++)
            selectionKeys.Add(BuildSelectionKey(selectionSet[i]));

        primarySelection = selectionSet[0];

        if (!string.IsNullOrWhiteSpace(primaryStableId))
        {
            for (int i = 0; i < selectionSet.Count; i++)
            {
                if (string.Equals(
                        selectionSet[i].stableId,
                        primaryStableId.Trim(),
                        StringComparison.Ordinal))
                {
                    primarySelection = selectionSet[i];
                    break;
                }
            }
        }

        RecalculateAggregateCapabilities();
        TouchSelectionSet();
        return true;
    }

    public int CopySelectionSet(
        List<BistroBuilderEditorV2Selection> results,
        bool stableOrder = true)
    {
        if (results == null)
            return 0;

        results.Clear();
        results.AddRange(selectionSet);

        if (stableOrder)
            results.Sort(CompareSelectionsStable);

        return results.Count;
    }

    public bool ClearSelectionSet()
    {
        if (selectionSet.Count == 0 &&
            !primarySelection.IsValid &&
            aggregateCapabilities == BistroBuilderEditorV2SelectionCapability.None)
        {
            return false;
        }

        selectionSet.Clear();
        selectionKeys.Clear();
        primarySelection = BistroBuilderEditorV2Selection.None;
        aggregateCapabilities = BistroBuilderEditorV2SelectionCapability.None;
        TouchSelectionSet();
        return true;
    }

    private void ReplaceSelectionSetInternal(
        BistroBuilderEditorV2Selection selection)
    {
        string key = BuildSelectionKey(selection);

        bool alreadyEquivalent =
            selectionSet.Count == 1 &&
            selectionKeys.Contains(key) &&
            Equivalent(selectionSet[0], selection) &&
            Equivalent(primarySelection, selection);

        if (alreadyEquivalent)
            return;

        selectionSet.Clear();
        selectionKeys.Clear();

        selectionSet.Add(selection);
        selectionKeys.Add(key);
        primarySelection = selection;
        aggregateCapabilities = selection.capabilities;
        TouchSelectionSet();
    }

    private void RemoveSelectionInternal(string key)
    {
        int index = -1;

        for (int i = 0; i < selectionSet.Count; i++)
        {
            if (string.Equals(
                    BuildSelectionKey(selectionSet[i]),
                    key,
                    StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            return;

        BistroBuilderEditorV2Selection removed = selectionSet[index];
        selectionSet.RemoveAt(index);
        selectionKeys.Remove(key);

        if (Equivalent(primarySelection, removed))
        {
            primarySelection = selectionSet.Count > 0
                ? selectionSet[selectionSet.Count - 1]
                : BistroBuilderEditorV2Selection.None;
        }

        RecalculateAggregateCapabilities();
        TouchSelectionSet();
    }

    private void ReplaceStoredSelection(
        string key,
        BistroBuilderEditorV2Selection selection)
    {
        for (int i = 0; i < selectionSet.Count; i++)
        {
            if (string.Equals(
                    BuildSelectionKey(selectionSet[i]),
                    key,
                    StringComparison.Ordinal))
            {
                selectionSet[i] = selection;
                return;
            }
        }
    }

    private void RecalculateAggregateCapabilities()
    {
        if (selectionSet.Count == 0)
        {
            aggregateCapabilities =
                BistroBuilderEditorV2SelectionCapability.None;
            return;
        }

        BistroBuilderEditorV2SelectionCapability aggregate =
            selectionSet[0].capabilities;

        for (int i = 1; i < selectionSet.Count; i++)
            aggregate &= selectionSet[i].capabilities;

        aggregateCapabilities = aggregate;
    }

    private void TouchSelectionSet()
    {
        selectionSetRevision++;
        SelectionSetChanged?.Invoke();
    }

    private static string BuildSelectionKey(
        BistroBuilderEditorV2Selection selection)
    {
        return BuildSelectionKey(
            selection.family,
            selection.kind,
            selection.stableId);
    }

    private static string BuildSelectionKey(
        BistroBuilderEditorV2ToolFamily family,
        BistroBuilderEditorV2SelectionKind kind,
        string stableId)
    {
        return ((int)family).ToString() + "|" +
               ((int)kind).ToString() + "|" +
               (stableId ?? string.Empty).Trim();
    }

    private static int CompareSelectionsStable(
        BistroBuilderEditorV2Selection a,
        BistroBuilderEditorV2Selection b)
    {
        int family = a.family.CompareTo(b.family);
        if (family != 0)
            return family;

        int kind = a.kind.CompareTo(b.kind);
        if (kind != 0)
            return kind;

        return string.Compare(
            a.stableId ?? string.Empty,
            b.stableId ?? string.Empty,
            StringComparison.Ordinal);
    }
}
