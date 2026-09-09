using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BistroBuilderEditAffectedSet
{
    public readonly List<BistroBuilderEditId> entityIds = new List<BistroBuilderEditId>();
    public Rect affectedBounds;
}

public sealed class BistroBuilderEditChangeSet
{
    public readonly List<BistroBuilderEditId> created = new List<BistroBuilderEditId>();
    public readonly List<BistroBuilderEditId> modified = new List<BistroBuilderEditId>();
    public readonly List<BistroBuilderEditId> removed = new List<BistroBuilderEditId>();
    public readonly BistroBuilderEditAffectedSet affected = new BistroBuilderEditAffectedSet();
}

public interface IBistroBuilderEditCommand
{
    string CommandId { get; }
    string Description { get; }
    bool TryExecute(BistroBuilderEditDocument document, out BistroBuilderEditChangeSet changeSet, out string error);
    bool TryUndo(BistroBuilderEditDocument document, out string error);
    bool TryRedo(BistroBuilderEditDocument document, out string error);
}

public enum BistroBuilderEditSessionState
{
    ActiveClean = 0, ActiveDirty = 1, ReviewingCommit = 2, Committed = 3, Cancelled = 4
}
public interface IBistroBuilderEditValidationProvider
{
    string SourceSystem { get; }
    void Validate(BistroBuilderEditDocument draft, long draftRevision, List<BistroBuilderEditDiagnostic> diagnostics);
}

public sealed class BistroBuilderIntrinsicEditValidationProvider : IBistroBuilderEditValidationProvider
{
    private readonly BistroBuilderArchitectureGeometryPolicy policy;
    public string SourceSystem => "EditMode.Architecture";
    public BistroBuilderIntrinsicEditValidationProvider(BistroBuilderArchitectureGeometryPolicy policy = null)
    { this.policy = policy ?? BistroBuilderArchitectureGeometryPolicy.Default; }
    public void Validate(BistroBuilderEditDocument draft, long draftRevision, List<BistroBuilderEditDiagnostic> diagnostics)
    {
        var topology = new BistroBuilderWallTopologyBuilder(policy).Build(draft.walls, draftRevision);
        diagnostics.AddRange(topology.diagnostics);
        BistroBuilderOpeningIntrinsicValidator.Validate(draft, policy, diagnostics, draftRevision);
    }
}

public sealed class BistroBuilderEditValidationOrchestrator
{
    private readonly List<IBistroBuilderEditValidationProvider> providers = new List<IBistroBuilderEditValidationProvider>();
    public void Register(IBistroBuilderEditValidationProvider provider)
    { if (provider != null && !providers.Contains(provider)) providers.Add(provider); }
    public List<BistroBuilderEditDiagnostic> Validate(BistroBuilderEditDocument draft, long draftRevision)
    {
        var diagnostics = new List<BistroBuilderEditDiagnostic>();
        for (int i = 0; i < providers.Count; i++) providers[i].Validate(draft, draftRevision, diagnostics);
        diagnostics.Sort((a,b) => string.CompareOrdinal(a.code, b.code));
        return diagnostics;
    }
}
public sealed class BistroBuilderEditSession
{
    private sealed class HistoryEntry
    {
        public IBistroBuilderEditCommand command;
        public BistroBuilderEditChangeSet changeSet;
        public List<BistroBuilderRoomProjection> roomsBefore;
        public List<BistroBuilderRoomProjection> roomsAfter;
    }

    private readonly BistroBuilderArchitectureGeometryPolicy policy;
    private readonly BistroBuilderEditValidationOrchestrator validation;
    private readonly List<HistoryEntry> undo = new List<HistoryEntry>();
    private readonly List<HistoryEntry> redo = new List<HistoryEntry>();
    private readonly HashSet<string> executedCommandIds = new HashSet<string>(StringComparer.Ordinal);
    private List<BistroBuilderRoomProjection> roomProjections = new List<BistroBuilderRoomProjection>();

    public string SessionId { get; } = Guid.NewGuid().ToString("N");
    public long BaselineRevision { get; }
    public long DraftRevision { get; private set; }
    public BistroBuilderEditDocument Baseline { get; }
    public BistroBuilderEditDocument Draft { get; private set; }
    public BistroBuilderEditSessionState State { get; private set; }
    public bool CanUndo => State == BistroBuilderEditSessionState.ActiveDirty && undo.Count > 0;
    public bool CanRedo => (State == BistroBuilderEditSessionState.ActiveDirty || State == BistroBuilderEditSessionState.ActiveClean) && redo.Count > 0;

    public BistroBuilderEditSession(BistroBuilderEditDocument committed,
        BistroBuilderArchitectureGeometryPolicy policy = null,
        BistroBuilderEditValidationOrchestrator validation = null)
    {
        if (committed == null) throw new ArgumentNullException(nameof(committed));
        this.policy = policy ?? BistroBuilderArchitectureGeometryPolicy.Default;
        this.validation = validation ?? new BistroBuilderEditValidationOrchestrator();
        this.validation.Register(new BistroBuilderIntrinsicEditValidationProvider(this.policy));
        Baseline = committed.DeepClone(); BaselineRevision = committed.revision;
        Draft = committed.DeepClone(); DraftRevision = committed.revision;
        State = BistroBuilderEditSessionState.ActiveClean;
        RebuildRooms();
    }
    public bool TryExecute(IBistroBuilderEditCommand command, out BistroBuilderEditChangeSet changeSet, out string error)
    {
        changeSet = null; error = string.Empty;
        if (State != BistroBuilderEditSessionState.ActiveClean && State != BistroBuilderEditSessionState.ActiveDirty)
        { error = "La sesión no acepta comandos en su estado actual."; return false; }
        if (command == null || string.IsNullOrWhiteSpace(command.CommandId))
        { error = "Comando inválido."; return false; }
        if (executedCommandIds.Contains(command.CommandId))
        { error = "El CommandId ya fue ejecutado en esta sesión."; return false; }

        var roomsBefore = CloneRoomProjections(roomProjections);
        if (!command.TryExecute(Draft, out changeSet, out error)) return false;
        DraftRevision++;
        RebuildRooms();
        var entry = new HistoryEntry
        {
            command = command, changeSet = changeSet,
            roomsBefore = roomsBefore, roomsAfter = CloneRoomProjections(roomProjections)
        };
        for (int i = 0; i < redo.Count; i++) executedCommandIds.Remove(redo[i].command.CommandId);
        redo.Clear(); undo.Add(entry); executedCommandIds.Add(command.CommandId);
        State = BistroBuilderEditSessionState.ActiveDirty;
        return true;
    }

    public bool TryUndo(out string error)
    {
        error = string.Empty; if (!CanUndo) { error = "No hay operación que deshacer."; return false; }
        HistoryEntry entry = undo[undo.Count - 1];
        if (!entry.command.TryUndo(Draft, out error)) return false;
        undo.RemoveAt(undo.Count - 1); redo.Add(entry); DraftRevision++;
        RestoreRooms(entry.roomsBefore); State = undo.Count == 0 ? BistroBuilderEditSessionState.ActiveClean : BistroBuilderEditSessionState.ActiveDirty;
        return true;
    }
    public bool TryRedo(out string error)
    {
        error = string.Empty; if (!CanRedo) { error = "No hay operación que rehacer."; return false; }
        HistoryEntry entry = redo[redo.Count - 1];
        if (!entry.command.TryRedo(Draft, out error)) return false;
        redo.RemoveAt(redo.Count - 1); undo.Add(entry); DraftRevision++;
        RestoreRooms(entry.roomsAfter); State = BistroBuilderEditSessionState.ActiveDirty;
        return true;
    }

    public void Cancel()
    {
        if (State == BistroBuilderEditSessionState.Committed || State == BistroBuilderEditSessionState.Cancelled) return;
        Draft = Baseline.DeepClone(); roomProjections.Clear(); undo.Clear(); redo.Clear();
        State = BistroBuilderEditSessionState.Cancelled;
    }

    public bool TryReview(out List<BistroBuilderEditDiagnostic> diagnostics)
    {
        diagnostics = validation.Validate(Draft, DraftRevision);
        for (int i = 0; i < diagnostics.Count; i++)
            if (diagnostics[i].severity == BistroBuilderEditDiagnosticSeverity.Blocking) return false;
        State = BistroBuilderEditSessionState.ReviewingCommit; return true;
    }

    public bool TryPrepareCommit(out BistroBuilderEditDocument candidate, out List<BistroBuilderEditDiagnostic> diagnostics)
    {
        candidate = null;
        if (!TryReview(out diagnostics))
        {
            State = undo.Count == 0 ? BistroBuilderEditSessionState.ActiveClean : BistroBuilderEditSessionState.ActiveDirty;
            return false;
        }
        candidate = Draft.DeepClone();
        candidate.revision = BaselineRevision + 1;
        return true;
    }

    public bool FinalizePreparedCommit(BistroBuilderEditDocument candidate)
    {
        if (State != BistroBuilderEditSessionState.ReviewingCommit || candidate == null) return false;
        if (candidate.revision != BaselineRevision + 1) return false;
        if (!string.Equals(candidate.ComputeFingerprint(), BuildExpectedCommitFingerprint(), StringComparison.Ordinal)) return false;
        State = BistroBuilderEditSessionState.Committed;
        return true;
    }

    public void RejectPreparedCommit()
    {
        if (State == BistroBuilderEditSessionState.ReviewingCommit)
            State = undo.Count == 0 ? BistroBuilderEditSessionState.ActiveClean : BistroBuilderEditSessionState.ActiveDirty;
    }

    public bool TryCommit(out BistroBuilderEditDocument committed, out List<BistroBuilderEditDiagnostic> diagnostics)
    {
        if (!TryPrepareCommit(out committed, out diagnostics)) return false;
        if (FinalizePreparedCommit(committed)) return true;
        committed = null; RejectPreparedCommit(); return false;
    }

    private string BuildExpectedCommitFingerprint()
    {
        var expected = Draft.DeepClone(); expected.revision = BaselineRevision + 1; return expected.ComputeFingerprint();
    }
    public IReadOnlyList<BistroBuilderRoomProjection> RoomProjections => roomProjections;

    private void RebuildRooms()
    {
        var topology = new BistroBuilderWallTopologyBuilder(policy).Build(Draft.walls, DraftRevision);
        var faces = new BistroBuilderRoomFaceDetector(policy).Detect(topology);
        if (roomProjections.Count == 0 && Draft.rooms.Count > 0)
        {
            for (int i = 0; i < Draft.rooms.Count; i++)
            {
                var record = Draft.rooms[i];
                for (int f = 0; f < faces.Count; f++)
                {
                    if (!BistroBuilderRoomIdentityReconciler.PointInPolygon(record.identityAnchor, faces[f].boundary)) continue;
                    var p = new BistroBuilderRoomProjection { room = record.DeepClone(), area = faces[f].area, centroid = faces[f].centroid };
                    p.boundary.AddRange(faces[f].boundary); roomProjections.Add(p); break;
                }
            }
        }
        var reconciled = new BistroBuilderRoomIdentityReconciler().Reconcile(roomProjections, faces, "default", DraftRevision);
        roomProjections = CloneRoomProjections(reconciled.projections);
        Draft.rooms.Clear(); for (int i = 0; i < roomProjections.Count; i++) Draft.rooms.Add(roomProjections[i].room.DeepClone());
    }

    private void RestoreRooms(List<BistroBuilderRoomProjection> source)
    {
        roomProjections = CloneRoomProjections(source); Draft.rooms.Clear();
        for (int i = 0; i < roomProjections.Count; i++) Draft.rooms.Add(roomProjections[i].room.DeepClone());
    }

    private static List<BistroBuilderRoomProjection> CloneRoomProjections(IReadOnlyList<BistroBuilderRoomProjection> source)
    {
        var copy = new List<BistroBuilderRoomProjection>(); if (source == null) return copy;
        for (int i = 0; i < source.Count; i++)
        {
            var p = source[i]; if (p == null) continue;
            var c = new BistroBuilderRoomProjection { room = p.room.DeepClone(), area = p.area, centroid = p.centroid };
            c.boundary.AddRange(p.boundary); copy.Add(c);
        }
        return copy;
    }
}
