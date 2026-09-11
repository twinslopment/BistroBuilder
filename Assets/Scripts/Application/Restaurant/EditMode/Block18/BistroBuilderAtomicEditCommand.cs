using System;
using System.Collections.Generic;

/// <summary>One history entry in the existing Block 18 session. Child commands,
/// validation and inverse operations run on a disposable canonical document.
/// Only a successful result is copied into Draft; no second history is created.</summary>
public sealed class BistroBuilderAtomicEditCommand : BistroBuilderEditCommandBase
{
    private readonly IBistroBuilderEditCommand[] commands;
    private readonly string description;
    private bool executed;
    public override string Description => description;

    public BistroBuilderAtomicEditCommand(string description, params IBistroBuilderEditCommand[] commands)
    {
        this.description = description ?? "Construction gesture";
        this.commands = commands != null ? (IBistroBuilderEditCommand[])commands.Clone() : Array.Empty<IBistroBuilderEditCommand>();
    }

    public override bool TryExecute(BistroBuilderEditDocument document, out BistroBuilderEditChangeSet changeSet, out string error)
    {
        changeSet = null;
        error = string.Empty;
        if (document == null || commands.Length == 0 || executed)
        { error = "Invalid or already executed atomic command."; return false; }
        try
        {
            var candidate = document.DeepClone();
            var combined = new BistroBuilderEditChangeSet();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var command in commands)
            {
                if (command == null || !ids.Add(command.CommandId))
                { error = "Null or duplicate child command."; return false; }
                if (!command.TryExecute(candidate, out var change, out error)) return false;
                if (change == null) continue;
                AddUnique(combined.created, change.created);
                AddUnique(combined.modified, change.modified);
                AddUnique(combined.removed, change.removed);
                AddUnique(combined.affected.entityIds, change.affected.entityIds);
            }
            if (!Validate(candidate, out error)) return false;
            PublishCopy(candidate, document);
            executed = true;
            changeSet = combined;
            return true;
        }
        catch (Exception exception)
        { error = "Atomic command rejected: " + exception.Message; return false; }
    }

    public override bool TryUndo(BistroBuilderEditDocument document, out string error) => Replay(document, true, out error);
    public override bool TryRedo(BistroBuilderEditDocument document, out string error) => Replay(document, false, out error);

    private bool Replay(BistroBuilderEditDocument document, bool undo, out string error)
    {
        error = string.Empty;
        if (!executed || document == null) { error = "Command has not executed."; return false; }
        try
        {
            var candidate = document.DeepClone();
            for (int i = 0; i < commands.Length; i++)
            {
                var command = commands[undo ? commands.Length - 1 - i : i];
                bool ok = undo ? command.TryUndo(candidate, out error) : command.TryRedo(candidate, out error);
                if (!ok) return false;
            }
            if (!Validate(candidate, out error)) return false;
            PublishCopy(candidate, document);
            return true;
        }
        catch (Exception exception)
        { error = "Atomic inverse rejected: " + exception.Message; return false; }
    }

    private static bool Validate(BistroBuilderEditDocument candidate, out string error)
    {
        var diagnostics = new List<BistroBuilderEditDiagnostic>();
        new BistroBuilderIntrinsicEditValidationProvider().Validate(candidate, candidate.revision, diagnostics);
        foreach (var d in diagnostics)
            if (d.severity == BistroBuilderEditDiagnosticSeverity.Blocking)
            { error = d.code + ": " + d.message; return false; }
        error = string.Empty;
        return true;
    }

    private static void AddUnique(List<BistroBuilderEditId> target, List<BistroBuilderEditId> source)
    { foreach (var id in source) if (!target.Contains(id)) target.Add(id); }

    private static void PublishCopy(BistroBuilderEditDocument candidate, BistroBuilderEditDocument target)
    {
        // A child command retaining its staging document cannot subsequently mutate Draft.
        var source = candidate.DeepClone();
        target.documentId = source.documentId; target.schemaVersion = source.schemaVersion; target.revision = source.revision;
        target.walls = source.walls; target.openings = source.openings; target.rooms = source.rooms;
        target.surfaces = source.surfaces; target.zones = source.zones;
    }
}
