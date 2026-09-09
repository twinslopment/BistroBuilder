using System;
using System.Collections.Generic;

public interface IBistroBuilderEditDocumentStore
{
    BistroBuilderEditDocument GetCommittedSnapshot();
    bool TryPublish(long expectedBaselineRevision, BistroBuilderEditDocument candidate,
        string operationId, out string error);
}

public interface IBistroBuilderEditCommitJournalStore
{
    bool TryWrite(BistroBuilderEditCommitJournal journal, out string error);
    bool TryRead(string operationId, out BistroBuilderEditCommitJournal journal);
}

public sealed class BistroBuilderInMemoryEditDocumentStore : IBistroBuilderEditDocumentStore
{
    private BistroBuilderEditDocument committed;
    private readonly HashSet<string> publishedOperations = new HashSet<string>(StringComparer.Ordinal);
    public BistroBuilderInMemoryEditDocumentStore(BistroBuilderEditDocument initial)
    { committed = initial != null ? initial.DeepClone() : new BistroBuilderEditDocument(); }
    public BistroBuilderEditDocument GetCommittedSnapshot() => committed.DeepClone();
    public bool TryPublish(long expectedBaselineRevision, BistroBuilderEditDocument candidate,
        string operationId, out string error)
    {
        error = string.Empty;
        if (candidate == null || string.IsNullOrWhiteSpace(operationId)) { error = "Publicación inválida."; return false; }
        if (publishedOperations.Contains(operationId)) return true;
        if (committed.revision != expectedBaselineRevision) { error = "La baseline comprometida ha cambiado."; return false; }
        if (candidate.revision != expectedBaselineRevision + 1) { error = "La revisión candidata no es N+1."; return false; }
        committed = candidate.DeepClone(); publishedOperations.Add(operationId); return true;
    }
}
public sealed class BistroBuilderInMemoryCommitJournalStore : IBistroBuilderEditCommitJournalStore
{
    private readonly Dictionary<string, BistroBuilderEditCommitJournal> journals =
        new Dictionary<string, BistroBuilderEditCommitJournal>(StringComparer.Ordinal);
    public bool TryWrite(BistroBuilderEditCommitJournal journal, out string error)
    {
        error = string.Empty;
        if (journal == null || string.IsNullOrWhiteSpace(journal.operationId)) { error = "Journal inválido."; return false; }
        journals[journal.operationId] = Clone(journal); return true;
    }
    public bool TryRead(string operationId, out BistroBuilderEditCommitJournal journal)
    {
        journal = null;
        if (!journals.TryGetValue(operationId ?? string.Empty, out var stored)) return false;
        journal = Clone(stored); return true;
    }
    private static BistroBuilderEditCommitJournal Clone(BistroBuilderEditCommitJournal source) =>
        new BistroBuilderEditCommitJournal
        {
            operationId = source.operationId, sessionId = source.sessionId,
            baselineRevision = source.baselineRevision, draftRevision = source.draftRevision,
            draftFingerprint = source.draftFingerprint, economicAuthorizationId = source.economicAuthorizationId,
            state = source.state
        };
}

public static class BistroBuilderEditEconomicProposalBuilder
{
    public static BistroBuilderEditEconomicProposal Build(BistroBuilderEditSession session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        var proposal = new BistroBuilderEditEconomicProposal
        {
            sessionId = session.SessionId, baselineRevision = session.BaselineRevision,
            draftRevision = session.DraftRevision, draftFingerprint = session.Draft.ComputeFingerprint()
        };
        AddWallChanges(session.Baseline, session.Draft, proposal.lines);
        AddOpeningChanges(session.Baseline, session.Draft, proposal.lines);
        AddSurfaceChanges(session.Baseline, session.Draft, proposal.lines);
        AddZoneChanges(session.Baseline, session.Draft, proposal.lines);
        return proposal;
    }

    private static void AddWallChanges(BistroBuilderEditDocument before, BistroBuilderEditDocument after,
        List<BistroBuilderEditEconomicLine> lines)
    {
        var oldById = new Dictionary<string, BistroBuilderWallRecord>(StringComparer.Ordinal);
        for (int i = 0; i < before.walls.Count; i++) oldById[before.walls[i].wallId.Value] = before.walls[i];
        for (int i = 0; i < after.walls.Count; i++)
        {
            var wall = after.walls[i];
            if (!oldById.TryGetValue(wall.wallId.Value, out var old))
                lines.Add(WallLine(BistroBuilderEditEconomicChangeKind.Added, wall));
            else
            {
                if (!SameWall(old, wall)) lines.Add(WallLine(BistroBuilderEditEconomicChangeKind.Modified, wall));
                oldById.Remove(wall.wallId.Value);
            }
        }
        foreach (var pair in oldById) lines.Add(WallLine(BistroBuilderEditEconomicChangeKind.Removed, pair.Value));
    }

    private static BistroBuilderEditEconomicLine WallLine(BistroBuilderEditEconomicChangeKind kind, BistroBuilderWallRecord wall) =>
        new BistroBuilderEditEconomicLine
        { kind = kind, entityId = wall.wallId, definitionId = wall.wallDefinitionId, quantity = 1f, length = wall.Length };

    private static bool SameWall(BistroBuilderWallRecord a, BistroBuilderWallRecord b) =>
        a.axisStart == b.axisStart && a.axisEnd == b.axisEnd && a.baseElevation == b.baseElevation &&
        a.height == b.height && a.thickness == b.thickness && a.wallDefinitionId == b.wallDefinitionId &&
        a.sideAFinishId == b.sideAFinishId && a.sideBFinishId == b.sideBFinishId;
    private static void AddOpeningChanges(BistroBuilderEditDocument before, BistroBuilderEditDocument after,
        List<BistroBuilderEditEconomicLine> lines)
    {
        var oldById = new Dictionary<string, BistroBuilderOpeningRecord>(StringComparer.Ordinal);
        for (int i = 0; i < before.openings.Count; i++) oldById[before.openings[i].openingId.Value] = before.openings[i];
        for (int i = 0; i < after.openings.Count; i++)
        {
            var opening = after.openings[i];
            if (!oldById.TryGetValue(opening.openingId.Value, out var old))
                lines.Add(OpeningLine(BistroBuilderEditEconomicChangeKind.Added, opening));
            else
            {
                if (!SameOpening(old, opening)) lines.Add(OpeningLine(BistroBuilderEditEconomicChangeKind.Modified, opening));
                oldById.Remove(opening.openingId.Value);
            }
        }
        foreach (var pair in oldById) lines.Add(OpeningLine(BistroBuilderEditEconomicChangeKind.Removed, pair.Value));
    }

    private static BistroBuilderEditEconomicLine OpeningLine(BistroBuilderEditEconomicChangeKind kind, BistroBuilderOpeningRecord opening) =>
        new BistroBuilderEditEconomicLine
        {
            kind = kind, entityId = opening.openingId,
            definitionId = string.IsNullOrWhiteSpace(opening.fillDefinitionId) ? opening.openingType : opening.fillDefinitionId,
            quantity = 1f, length = opening.width
        };

    private static bool SameOpening(BistroBuilderOpeningRecord a, BistroBuilderOpeningRecord b) =>
        a.hostWallId == b.hostWallId && a.axisPosition01 == b.axisPosition01 && a.width == b.width &&
        a.bottomElevation == b.bottomElevation && a.height == b.height && a.openingType == b.openingType &&
        a.fillDefinitionId == b.fillDefinitionId && a.flipped == b.flipped;

    private static void AddSurfaceChanges(BistroBuilderEditDocument before, BistroBuilderEditDocument after,
        List<BistroBuilderEditEconomicLine> lines)
    {
        var oldById = new Dictionary<string, BistroBuilderSurfaceFinishPatchRecord>(StringComparer.Ordinal);
        for (int i = 0; i < before.surfaces.Count; i++) oldById[before.surfaces[i].surfacePatchId.Value] = before.surfaces[i];
        for (int i = 0; i < after.surfaces.Count; i++)
        {
            var surface = after.surfaces[i];
            if (!oldById.TryGetValue(surface.surfacePatchId.Value, out var old))
                lines.Add(SurfaceLine(BistroBuilderEditEconomicChangeKind.Added, surface));
            else
            {
                if (!SameSurface(old, surface)) lines.Add(SurfaceLine(BistroBuilderEditEconomicChangeKind.Modified, surface));
                oldById.Remove(surface.surfacePatchId.Value);
            }
        }
        foreach (var pair in oldById) lines.Add(SurfaceLine(BistroBuilderEditEconomicChangeKind.Removed, pair.Value));
    }

    private static BistroBuilderEditEconomicLine SurfaceLine(BistroBuilderEditEconomicChangeKind kind,
        BistroBuilderSurfaceFinishPatchRecord surface) => new BistroBuilderEditEconomicLine
        {
            kind = kind, entityId = surface.surfacePatchId, definitionId = surface.finishDefinitionId,
            quantity = 1f, area = PolygonArea(surface.fallbackBoundary)
        };

    private static bool SameSurface(BistroBuilderSurfaceFinishPatchRecord a, BistroBuilderSurfaceFinishPatchRecord b)
    {
        if (a.buildPlaneId != b.buildPlaneId || a.surfaceRole != b.surfaceRole ||
            a.finishDefinitionId != b.finishDefinitionId || a.fallbackBoundary.Count != b.fallbackBoundary.Count) return false;
        for (int i = 0; i < a.fallbackBoundary.Count; i++) if (a.fallbackBoundary[i] != b.fallbackBoundary[i]) return false;
        return true;
    }

    private static void AddZoneChanges(BistroBuilderEditDocument before, BistroBuilderEditDocument after,
        List<BistroBuilderEditEconomicLine> lines)
    {
        var oldById = new Dictionary<string, BistroBuilderFunctionalZoneRecord>(StringComparer.Ordinal);
        for (int i = 0; i < before.zones.Count; i++) oldById[before.zones[i].zoneId.Value] = before.zones[i];
        for (int i = 0; i < after.zones.Count; i++)
        {
            var zone = after.zones[i];
            if (!oldById.TryGetValue(zone.zoneId.Value, out var old))
                lines.Add(ZoneLine(BistroBuilderEditEconomicChangeKind.Added, zone));
            else
            {
                if (!SameZone(old, zone)) lines.Add(ZoneLine(BistroBuilderEditEconomicChangeKind.Modified, zone));
                oldById.Remove(zone.zoneId.Value);
            }
        }
        foreach (var pair in oldById) lines.Add(ZoneLine(BistroBuilderEditEconomicChangeKind.Removed, pair.Value));
    }

    private static BistroBuilderEditEconomicLine ZoneLine(BistroBuilderEditEconomicChangeKind kind,
        BistroBuilderFunctionalZoneRecord zone) => new BistroBuilderEditEconomicLine
        {
            kind = kind, entityId = zone.zoneId, definitionId = zone.zoneDefinitionId,
            quantity = 1f, area = PolygonArea(zone.explicitRegion)
        };

    private static bool SameZone(BistroBuilderFunctionalZoneRecord a, BistroBuilderFunctionalZoneRecord b)
    {
        if (a.zoneDefinitionId != b.zoneDefinitionId || a.roomIds.Count != b.roomIds.Count ||
            a.explicitRegion.Count != b.explicitRegion.Count) return false;
        for (int i = 0; i < a.roomIds.Count; i++) if (a.roomIds[i] != b.roomIds[i]) return false;
        for (int i = 0; i < a.explicitRegion.Count; i++) if (a.explicitRegion[i] != b.explicitRegion[i]) return false;
        return true;
    }

    private static float PolygonArea(IReadOnlyList<UnityEngine.Vector2> polygon)
    {
        if (polygon == null || polygon.Count < 3) return 0f;
        double sum = 0d;
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
            sum += (double)a.x * b.y - (double)b.x * a.y;
        }
        return (float)Math.Abs(sum * 0.5d);
    }
}
public sealed class BistroBuilderEditCommitCoordinator
{
    private readonly IBistroBuilderEditDocumentStore documentStore;
    private readonly IBistroBuilderEditCommitJournalStore journalStore;
    private readonly IBistroBuilderEditEconomicGateway economy;

    public BistroBuilderEditCommitCoordinator(IBistroBuilderEditDocumentStore documentStore,
        IBistroBuilderEditCommitJournalStore journalStore, IBistroBuilderEditEconomicGateway economy)
    {
        this.documentStore = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
        this.journalStore = journalStore ?? throw new ArgumentNullException(nameof(journalStore));
        this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
    }

    public bool TryCommit(BistroBuilderEditSession session, out BistroBuilderEditDocument committed,
        out BistroBuilderEditCommitJournal journal, out List<BistroBuilderEditDiagnostic> diagnostics, out string error)
    {
        committed = null; journal = null; error = string.Empty;
        if (session == null) { diagnostics = new List<BistroBuilderEditDiagnostic>(); error = "Sesión inexistente."; return false; }
        if (!session.TryPrepareCommit(out var candidate, out diagnostics)) { error = "La propuesta contiene bloqueantes."; return false; }
        var proposal = BistroBuilderEditEconomicProposalBuilder.Build(session);
        if (!economy.TryPrepareAuthorization(proposal, out var authorization, out error))
        { session.RejectPreparedCommit(); return false; }
        if (!authorization.IsValid || authorization.draftRevision != session.DraftRevision)
        {
            economy.TryAbortAuthorization(authorization, out _); session.RejectPreparedCommit();
            error = "La autorización económica no corresponde a la revisión del Draft."; return false;
        }
        string operationId = Guid.NewGuid().ToString("N");
        journal = new BistroBuilderEditCommitJournal
        {
            operationId = operationId, sessionId = session.SessionId,
            baselineRevision = session.BaselineRevision, draftRevision = session.DraftRevision,
            draftFingerprint = proposal.draftFingerprint, economicAuthorizationId = authorization.authorizationId,
            state = BistroBuilderEditCommitJournalState.Prepared
        };
        if (!journalStore.TryWrite(journal, out error))
        {
            economy.TryAbortAuthorization(authorization, out _); session.RejectPreparedCommit(); return false;
        }

        journal.state = BistroBuilderEditCommitJournalState.Publishing;
        if (!journalStore.TryWrite(journal, out error))
        { economy.TryAbortAuthorization(authorization, out _); session.RejectPreparedCommit(); return false; }
        if (!documentStore.TryPublish(session.BaselineRevision, candidate, operationId, out error))
        {
            journal.state = BistroBuilderEditCommitJournalState.Aborted; journalStore.TryWrite(journal, out _);
            economy.TryAbortAuthorization(authorization, out _); session.RejectPreparedCommit(); return false;
        }

        journal.state = BistroBuilderEditCommitJournalState.Published;
        if (!journalStore.TryWrite(journal, out error)) return false;
        if (!economy.TryFinalizeAuthorization(authorization, operationId, out error)) return false;
        journal.state = BistroBuilderEditCommitJournalState.Finalized;
        if (!journalStore.TryWrite(journal, out error)) return false;
        if (!session.FinalizePreparedCommit(candidate)) { error = "La sesión rechazó el candidato ya publicado."; return false; }
        committed = documentStore.GetCommittedSnapshot(); return true;
    }
}
