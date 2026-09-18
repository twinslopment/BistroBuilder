using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.ConstructionAuthoring
{
    public enum ConstructionGestureKind { None, Wall, Rectangle, MoveWall, MoveJunction, Opening }
    public enum ConstructionGestureState { Idle, Previewing, Ready, Invalid, Confirmed, Cancelled }
    public readonly struct ConstructionFeedback
    {
        public readonly ConstructionGestureState State;
        public readonly string Code;
        public bool CanConfirm => State == ConstructionGestureState.Ready;
        // Lightweight preview is not a replacement for final intrinsic/external validation.
        public bool RequiresFinalValidation => CanConfirm;
        public ConstructionFeedback(ConstructionGestureState state, string code) { State = state; Code = code; }
    }

    /// <summary>Logical selections refer to published definitions, never translated room-purpose aliases.</summary>
    public sealed class ConstructionDefinitionCatalog
    {
        private readonly HashSet<string> zones = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> ordered = new List<string>();
        public IReadOnlyList<string> ZoneIds { get; }
        public ConstructionDefinitionCatalog(IEnumerable<BistroBuilderEditCatalogDefinition> definitions)
        {
            if (definitions != null)
                foreach (var definition in definitions)
                    if (definition != null && definition.semanticKind == "zone" &&
                        !string.IsNullOrWhiteSpace(definition.definitionId) && zones.Add(definition.definitionId))
                        ordered.Add(definition.definitionId);
            ordered.Sort(StringComparer.Ordinal); ZoneIds = ordered.AsReadOnly();
        }
        public bool ContainsZone(string definitionId) => definitionId != null && zones.Contains(definitionId);
    }

    public delegate bool ConstructionCommandExecutor(IBistroBuilderEditCommand command,
        out BistroBuilderEditChangeSet changeSet, out string error);

    /// <summary>Transient gesture data only. Confirm dispatches ONE atomic command to
    /// the caller's existing Block 18 coordinator/session; no independent history/store.
    /// In runtime pass coordinator.TryExecute, preserving availability and feedback gates.</summary>
    public sealed class ConstructionGesture
    {
        private readonly List<WallPose> walls = new List<WallPose>(16);
        public IReadOnlyList<WallPose> PreviewWalls { get; }
        private BistroBuilderEditSession session;
        private ArchitectureQueryCache queries;
        private long revision;
        private string fingerprint;
        private Vector2 anchor;
        private BistroBuilderEditId targetId;
        private BistroBuilderWallRecord wallTemplate;
        private BistroBuilderOpeningRecord openingTemplate;
        private ConstructionDefinitionCatalog definitions;
        private string zoneId, plane;
        private float minimumSide;
        public ConstructionGestureKind Kind { get; private set; }
        public ConstructionGestureState State { get; private set; }
        public ConstructionDimensions Dimensions { get; private set; }
        public Vector2 OpeningCenter { get; private set; }
        public float OpeningPosition01 { get; private set; }
        public string Diagnostic { get; private set; } = string.Empty;
        public ConstructionFeedback Feedback => new ConstructionFeedback(State, Diagnostic);
        public ConstructionGesture() { PreviewWalls = walls.AsReadOnly(); }

        public bool BeginWall(BistroBuilderEditSession session, ArchitectureQueryCache queries,
            Vector2 start, BistroBuilderWallRecord template, out string error)
        {
            if (!ValidTemplate(template)) { error = "INVALID_WALL_DEFINITION"; return false; }
            if (!Begin(session, queries, start, ConstructionGestureKind.Wall, template.buildPlaneId, out error)) return false;
            wallTemplate = template.DeepClone(); return true;
        }

        public bool BeginRectangle(BistroBuilderEditSession session, ArchitectureQueryCache queries,
            Vector2 start, BistroBuilderWallRecord template, ConstructionDefinitionCatalog definitions,
            string zoneDefinitionId, float minimumSide, out string error)
        {
            if (!ValidTemplate(template) || !ConstructionGeometry.Finite(minimumSide) || minimumSide < 0.05f)
            { error = "INVALID_RECTANGLE_DEFINITION"; return false; }
            if (definitions == null || !definitions.ContainsZone(zoneDefinitionId))
            { error = "ZONE_DEFINITION_NOT_PUBLISHED"; return false; }
            if (!Begin(session, queries, start, ConstructionGestureKind.Rectangle, template.buildPlaneId, out error)) return false;
            wallTemplate = template.DeepClone(); this.definitions = definitions; zoneId = zoneDefinitionId;
            this.minimumSide = minimumSide; return true;
        }

        public bool BeginMoveWall(BistroBuilderEditSession session, ArchitectureQueryCache queries,
            BistroBuilderEditId wallId, Vector2 pointerStart, out string error)
        {
            var wall = queries?.Wall(wallId);
            if (wall == null) { error = "WALL_NOT_FOUND"; return false; }
            if (!Begin(session, queries, pointerStart, ConstructionGestureKind.MoveWall, wall.buildPlaneId, out error)) return false;
            targetId = wallId; return true;
        }

        public bool BeginJunction(BistroBuilderEditSession session, ArchitectureQueryCache queries,
            Vector2 junction, string plane, out string error) =>
            Begin(session, queries, junction, ConstructionGestureKind.MoveJunction, plane, out error);

        public bool BeginOpening(BistroBuilderEditSession session, ArchitectureQueryCache queries,
            BistroBuilderEditId hostId, BistroBuilderOpeningRecord template, out string error)
        {
            var host = queries?.Wall(hostId);
            if (host == null || template == null || string.IsNullOrWhiteSpace(template.openingType))
            { error = "OPENING_DEFINITION_OR_HOST_MISSING"; return false; }
            if (!Begin(session, queries, host.axisStart, ConstructionGestureKind.Opening, host.buildPlaneId, out error)) return false;
            openingTemplate = template.DeepClone(); targetId = hostId; return true;
        }

        private bool Begin(BistroBuilderEditSession session, ArchitectureQueryCache queries, Vector2 start,
            ConstructionGestureKind kind, string plane, out string error)
        {
            error = string.Empty;
            if (State == ConstructionGestureState.Previewing || State == ConstructionGestureState.Ready || State == ConstructionGestureState.Invalid)
            { error = "GESTURE_ALREADY_ACTIVE"; return false; }
            if (session == null || queries == null || !queries.Matches(session) || !ConstructionGeometry.Finite(start) ||
                (session.State != BistroBuilderEditSessionState.ActiveClean && session.State != BistroBuilderEditSessionState.ActiveDirty))
            { error = "SESSION_OR_QUERY_STALE"; return false; }
            this.session = session; this.queries = queries; this.plane = plane;
            revision = session.DraftRevision; fingerprint = session.Draft.ComputeFingerprint();
            anchor = start; Kind = kind; State = ConstructionGestureState.Previewing;
            Diagnostic = string.Empty; walls.Clear(); Dimensions = default; OpeningCenter = default; OpeningPosition01 = 0f;
            wallTemplate = null; openingTemplate = null; definitions = null; zoneId = null; targetId = default;
            return true;
        }

        /// <summary>Accepts the resolved snap point. Reuses the wall-pose buffer.
        /// Never executes a command or rebuilds topology, geometry or runtime systems.</summary>
        public bool Update(Vector2 pointer)
        {
            if (State != ConstructionGestureState.Previewing && State != ConstructionGestureState.Ready && State != ConstructionGestureState.Invalid) return false;
            walls.Clear(); Diagnostic = string.Empty; Dimensions = default; OpeningCenter = default; OpeningPosition01 = 0f;
            if (!IsCurrent() || !ConstructionGeometry.Finite(pointer)) return Invalid("GESTURE_STALE_OR_NONFINITE");
            bool ok;
            string error;
            switch (Kind)
            {
                case ConstructionGestureKind.Wall:
                    ok = ConstructionGeometry.TryWall(anchor, pointer, out var wall, out error);
                    if (ok) walls.Add(wall);
                    Dimensions = new ConstructionDimensions(anchor, pointer, false);
                    break;
                case ConstructionGestureKind.Rectangle:
                    ok = ConstructionGeometry.TryRectangle(anchor, pointer, minimumSide, walls, out error);
                    Dimensions = new ConstructionDimensions(anchor, pointer, true);
                    break;
                case ConstructionGestureKind.MoveWall:
                    ok = ConstructionGeometry.TryMoveWall(queries, targetId, pointer-anchor, walls, out error);
                    if (ok && walls.Count > 0) Dimensions = new ConstructionDimensions(walls[0].Start, walls[0].End, false);
                    if (ok && !AnyWallChanged()) { ok = false; error = "NO_CHANGE"; }
                    break;
                case ConstructionGestureKind.MoveJunction:
                    ok = ConstructionGeometry.TryMoveJunction(queries, anchor, pointer, plane, walls, out error);
                    if (ok && !AnyWallChanged()) { ok = false; error = "NO_CHANGE"; }
                    break;
                case ConstructionGestureKind.Opening:
                    ok = ConstructionGeometry.TryOpening(queries.Wall(targetId), pointer, openingTemplate.width,
                        openingTemplate.bottomElevation, openingTemplate.height, out float t, out var center, out error);
                    OpeningPosition01 = t; OpeningCenter = center;
                    Dimensions = new ConstructionDimensions(Vector2.zero, new Vector2(openingTemplate.width, openingTemplate.height), true);
                    break;
                default: ok = false; error = "NO_GESTURE"; break;
            }
            State = ok ? ConstructionGestureState.Ready : ConstructionGestureState.Invalid;
            Diagnostic = error; return ok;
        }

        public bool Confirm(ConstructionCommandExecutor execute, out string error)
        {
            error = string.Empty;
            if (State != ConstructionGestureState.Ready || execute == null)
            { error = "GESTURE_NOT_READY"; return false; }
            if (!IsCurrent() || session.Draft.ComputeFingerprint() != fingerprint)
            { error = "GESTURE_STALE"; Invalid(error); return false; }
            var commands = new List<IBistroBuilderEditCommand>();
            if (Kind == ConstructionGestureKind.Wall || Kind == ConstructionGestureKind.Rectangle)
            {
                foreach (var pose in walls)
                {
                    var wall = wallTemplate.DeepClone(); wall.wallId = BistroBuilderEditId.NewId();
                    wall.axisStart = pose.Start; wall.axisEnd = pose.End;
                    ConstructionWallCoverage.AppendUncovered(queries, wall, commands);
                }
                if (Kind == ConstructionGestureKind.Rectangle)
                {
                    if (!definitions.ContainsZone(zoneId)) { error = "ZONE_DEFINITION_NOT_PUBLISHED"; return false; }
                    var zone = new BistroBuilderFunctionalZoneRecord { zoneId = BistroBuilderEditId.NewId(), zoneDefinitionId = zoneId };
                    foreach (var pose in walls) zone.explicitRegion.Add(pose.Start);
                    commands.Add(new BistroBuilderSetFunctionalZoneCommand(zone));
                }
            }
            else if (Kind == ConstructionGestureKind.Opening)
            {
                var opening = openingTemplate.DeepClone(); opening.openingId = BistroBuilderEditId.NewId();
                opening.hostWallId = targetId; opening.axisPosition01 = OpeningPosition01;
                commands.Add(new BistroBuilderCreateOpeningCommand(opening));
            }
            else
            {
                foreach (var pose in walls)
                {
                    var wall = queries.Wall(pose.WallId).DeepClone(); wall.axisStart = pose.Start; wall.axisEnd = pose.End;
                    commands.Add(new BistroBuilderUpdateWallCommand(wall));
                }
            }
            if (!execute(new BistroBuilderAtomicEditCommand("Construction " + Kind, commands.ToArray()), out _, out error))
            { Diagnostic = error; return false; }
            State = ConstructionGestureState.Confirmed; walls.Clear(); Diagnostic = string.Empty; return true;
        }

        public void Cancel()
        {
            if (State == ConstructionGestureState.Confirmed) return;
            walls.Clear(); session = null; queries = null; fingerprint = null;
            Dimensions = default; OpeningCenter = default; OpeningPosition01 = 0f;
            State = ConstructionGestureState.Cancelled; Diagnostic = string.Empty;
        }

        private bool IsCurrent() => session != null && queries.Matches(session) && session.DraftRevision == revision &&
            (session.State == BistroBuilderEditSessionState.ActiveClean || session.State == BistroBuilderEditSessionState.ActiveDirty);
        private bool Invalid(string code) { State = ConstructionGestureState.Invalid; Diagnostic = code; return false; }
        private bool AnyWallChanged()
        {
            foreach (var pose in walls)
            {
                var original = queries.Wall(pose.WallId);
                if (original != null && (!ConstructionGeometry.Same(pose.Start, original.axisStart) ||
                    !ConstructionGeometry.Same(pose.End, original.axisEnd))) return true;
            }
            return false;
        }
        private static bool ValidTemplate(BistroBuilderWallRecord template) => template != null &&
            !string.IsNullOrWhiteSpace(template.wallDefinitionId) && !string.IsNullOrWhiteSpace(template.buildPlaneId) &&
            ConstructionGeometry.Finite(template.height) && template.height > 0f &&
            ConstructionGeometry.Finite(template.thickness) && template.thickness > 0f &&
            ConstructionGeometry.Finite(template.baseElevation);
    }
}
