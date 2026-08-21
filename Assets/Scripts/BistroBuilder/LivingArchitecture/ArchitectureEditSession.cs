using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    public enum ArchitectureEditMode
    {
        Inspect = 0,
        DrawWall = 1,
        MoveWall = 2,
        MoveVertex = 3,
        NumericEdit = 4
    }

    /// <summary>
    /// Sesión jugable LA9. Mantiene selección, preview, confirmación/cancelación y Undo/Redo
    /// sobre snapshots canónicos. No conoce GameObjects, cámara ni input concreto.
    /// </summary>
    public sealed class ArchitectureEditSession
    {
        private ArchitectureSnapshot current;
        private readonly ArchitectureSnapService snapService;
        private readonly ArchitectureImpactService impactService;
        private readonly Stack<ArchitectureCommittedOperation> undo = new Stack<ArchitectureCommittedOperation>();
        private readonly Stack<ArchitectureCommittedOperation> redo = new Stack<ArchitectureCommittedOperation>();

        public ArchitectureEditMode Mode { get; private set; }
        public WallId SelectedWallId { get; private set; }
        public VertexId SelectedVertexId { get; private set; }
        public ArchitectureOperationProposal Preview { get; private set; }
        public ArchitectureImpactReport PreviewImpact { get; private set; }
        public IReadOnlyList<ArchitectureSnapCandidate> LastSnapCandidates { get; private set; } = Array.Empty<ArchitectureSnapCandidate>();
        public bool HasPreview => Preview != null;
        public bool CanConfirm => Preview != null && Preview.IsReady && (PreviewImpact == null || !PreviewImpact.HasBlockingIssues);
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;

        public ArchitectureEditSession(
            ArchitectureSnapshot initial,
            ArchitectureSnapService snapService = null,
            ArchitectureImpactService impactService = null)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            var validation = ArchitectureValidator.Validate(initial);
            if (!validation.IsValid) throw new ArgumentException("LA9_INVALID_INITIAL_SNAPSHOT", nameof(initial));
            current = initial.DeepClone();
            this.snapService = snapService ?? new ArchitectureSnapService();
            this.impactService = impactService ?? new ArchitectureImpactService();
            Mode = ArchitectureEditMode.Inspect;
        }

        public ArchitectureSnapshot CaptureCurrent() => current.DeepClone();
        public ArchitectureSnapshot CaptureVisible() => (Preview?.ProposedSnapshot ?? current).DeepClone();

        public void SetMode(ArchitectureEditMode mode)
        {
            if (Mode == mode) return;
            CancelPreview();
            Mode = mode;
        }

        public bool SelectWall(WallId wallId)
        {
            if (current.FindWall(wallId) == null) return false;
            CancelPreview();
            SelectedWallId = wallId;
            SelectedVertexId = default(VertexId);
            return true;
        }

        public bool SelectVertex(VertexId vertexId)
        {
            if (current.FindVertex(vertexId) == null) return false;
            CancelPreview();
            SelectedVertexId = vertexId;
            SelectedWallId = default(WallId);
            return true;
        }

        public void ClearSelection()
        {
            CancelPreview();
            SelectedWallId = default(WallId);
            SelectedVertexId = default(VertexId);
        }

        public IReadOnlyList<ArchitectureSnapCandidate> QuerySnap(
            LevelId levelId,
            ArchitecturePoint cursor,
            bool hasAnchor,
            ArchitecturePoint anchor,
            WallId excludedWallId,
            double maxDistance = 0.35d)
        {
            var level = current.FindLevel(levelId);
            if (level == null)
            {
                LastSnapCandidates = Array.Empty<ArchitectureSnapCandidate>();
                return LastSnapCandidates;
            }

            LastSnapCandidates = snapService.GenerateCandidates(new ArchitectureSnapRequest
            {
                Level = level.DeepClone(),
                Cursor = cursor,
                HasAnchor = hasAnchor,
                Anchor = anchor,
                ExcludedWallId = excludedWallId,
                MaxDistance = maxDistance
            });
            return LastSnapCandidates;
        }

        public ArchitectureOperationProposal PreviewCreateWall(
            LevelId levelId,
            ArchitecturePoint start,
            ArchitecturePoint end,
            double thickness = 0.15d,
            double height = 2.8d,
            bool useSnap = true,
            double snapDistance = 0.35d)
        {
            var level = current.FindLevel(levelId);
            if (level == null) return SetRejected("LA9_LEVEL_NOT_FOUND", "No existe el nivel activo.");

            var resolvedStart = ResolveSnap(level, start, false, default(ArchitecturePoint), default(WallId), useSnap, snapDistance);
            var resolvedEnd = ResolveSnap(level, end, true, resolvedStart.Point, default(WallId), useSnap, snapDistance);
            if (resolvedStart.Point.DistanceTo(resolvedEnd.Point) <= ArchitectureGeometry.Epsilon)
                return SetRejected("LA9_WALL_TOO_SHORT", "La pared necesita una longitud positiva.");

            var startId = resolvedStart.VertexId ?? VertexId.New();
            var endId = resolvedEnd.VertexId ?? VertexId.New();
            var wallId = WallId.New();

            Preview = ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.CreateWall, "Crear pared", snapshot =>
            {
                var targetLevel = snapshot.FindLevel(levelId);
                if (targetLevel == null) throw new InvalidOperationException("LA9_LEVEL_NOT_FOUND");
                if (!resolvedStart.VertexId.HasValue)
                    targetLevel.Vertices.Add(new ArchitectureVertex { Id = startId, Position = resolvedStart.Point });
                if (!resolvedEnd.VertexId.HasValue)
                    targetLevel.Vertices.Add(new ArchitectureVertex { Id = endId, Position = resolvedEnd.Point });
                ArchitectureMutations.CreateWall(snapshot, levelId, startId, endId, wallId, thickness, height);
            });
            SelectedWallId = wallId;
            SelectedVertexId = default(VertexId);
            Mode = ArchitectureEditMode.DrawWall;
            AnalyzePreview();
            return Preview;
        }

        public ArchitectureOperationProposal PreviewMoveWall(WallId wallId, double deltaX, double deltaY)
        {
            Preview = ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.MoveWall, "Mover pared", snapshot =>
                ArchitectureMutations.MoveWall(snapshot, wallId, deltaX, deltaY));
            if (Preview.IsReady) SelectedWallId = wallId;
            SelectedVertexId = default(VertexId);
            Mode = ArchitectureEditMode.MoveWall;
            AnalyzePreview();
            return Preview;
        }

        public ArchitectureOperationProposal PreviewMoveVertex(VertexId vertexId, ArchitecturePoint target, bool useSnap = true, double snapDistance = 0.35d)
        {
            var ownerLevel = FindVertexLevel(current, vertexId);
            if (ownerLevel == null) return SetRejected("LA9_VERTEX_NOT_FOUND", "No existe el vértice seleccionado.");
            var resolved = ResolveSnap(ownerLevel, target, false, default(ArchitecturePoint), default(WallId), useSnap, snapDistance, vertexId);
            Preview = ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.MoveVertex, "Mover vértice", snapshot =>
                ArchitectureMutations.MoveVertex(snapshot, vertexId, resolved.Point));
            if (Preview.IsReady) SelectedVertexId = vertexId;
            SelectedWallId = default(WallId);
            Mode = ArchitectureEditMode.MoveVertex;
            AnalyzePreview();
            return Preview;
        }

        public ArchitectureOperationProposal PreviewSetWallLength(WallId wallId, double targetLength, bool preserveStart = true)
        {
            if (targetLength <= ArchitectureGeometry.Epsilon)
                return SetRejected("LA9_INVALID_LENGTH", "La longitud debe ser positiva.");
            var wall = current.FindWall(wallId);
            if (wall == null) return SetRejected("LA9_WALL_NOT_FOUND", "No existe la pared seleccionada.");
            var level = FindWallLevel(current, wallId);
            var start = level.Vertices.First(x => x != null && x.Id.Equals(wall.StartVertexId));
            var end = level.Vertices.First(x => x != null && x.Id.Equals(wall.EndVertexId));
            var dx = end.Position.X - start.Position.X;
            var dy = end.Position.Y - start.Position.Y;
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length <= ArchitectureGeometry.Epsilon) return SetRejected("LA9_ZERO_SOURCE_LENGTH", "La pared origen no tiene dirección válida.");
            var ux = dx / length;
            var uy = dy / length;
            var movedVertex = preserveStart ? wall.EndVertexId : wall.StartVertexId;
            var target = preserveStart
                ? new ArchitecturePoint(start.Position.X + (ux * targetLength), start.Position.Y + (uy * targetLength))
                : new ArchitecturePoint(end.Position.X - (ux * targetLength), end.Position.Y - (uy * targetLength));

            Preview = ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.MoveVertex, "Editar longitud de pared", snapshot =>
                ArchitectureMutations.MoveVertex(snapshot, movedVertex, target));
            SelectedWallId = wallId;
            SelectedVertexId = default(VertexId);
            Mode = ArchitectureEditMode.NumericEdit;
            AnalyzePreview();
            return Preview;
        }

        public ArchitectureOperationProposal PreviewDeleteSelectedWall()
        {
            if (!ArchitectureId.IsValid(SelectedWallId.Value))
                return SetRejected("LA9_NO_WALL_SELECTED", "No hay una pared seleccionada.");
            var wallId = SelectedWallId;
            Preview = ArchitectureTransactionEngine.Propose(current, ArchitectureOperationKind.DeleteWall, "Eliminar pared", snapshot =>
                ArchitectureMutations.DeleteWall(snapshot, wallId));
            AnalyzePreview();
            return Preview;
        }

        public bool TryConfirm(out string diagnosticCode)
        {
            diagnosticCode = null;
            if (!CanConfirm)
            {
                diagnosticCode = PreviewImpact != null && PreviewImpact.HasBlockingIssues ? "LA9_IMPACT_BLOCKING" : "LA9_PREVIEW_NOT_READY";
                return false;
            }

            ArchitectureCommittedOperation committed;
            if (!ArchitectureTransactionEngine.TryCommit(ref current, Preview, out committed, out diagnosticCode)) return false;
            undo.Push(committed);
            redo.Clear();
            Preview = null;
            PreviewImpact = null;
            LastSnapCandidates = Array.Empty<ArchitectureSnapCandidate>();
            return true;
        }

        public void CancelPreview()
        {
            Preview = null;
            PreviewImpact = null;
            LastSnapCandidates = Array.Empty<ArchitectureSnapCandidate>();
        }

        public bool TryUndo(out string diagnosticCode)
        {
            diagnosticCode = null;
            CancelPreview();
            if (undo.Count == 0) { diagnosticCode = "LA9_UNDO_EMPTY"; return false; }
            var operation = undo.Pop();
            if (!ArchitectureTransactionEngine.TryUndo(ref current, operation, out diagnosticCode))
            {
                undo.Push(operation);
                return false;
            }
            redo.Push(operation);
            return true;
        }

        public bool TryRedo(out string diagnosticCode)
        {
            diagnosticCode = null;
            CancelPreview();
            if (redo.Count == 0) { diagnosticCode = "LA9_REDO_EMPTY"; return false; }
            var operation = redo.Pop();
            if (!ArchitectureTransactionEngine.TryRedo(ref current, operation, out diagnosticCode))
            {
                redo.Push(operation);
                return false;
            }
            undo.Push(operation);
            return true;
        }

        private void AnalyzePreview()
        {
            PreviewImpact = Preview != null && Preview.IsReady ? impactService.Analyze(current, Preview) : null;
        }

        private ArchitectureOperationProposal SetRejected(string code, string message)
        {
            Preview = new ArchitectureOperationProposal
            {
                Operation = new ArchitectureOperationDescriptor { Id = ArchitectureOperationId.New(), Kind = ArchitectureOperationKind.Composite, Label = "Edición LA9" },
                BaseFingerprint = current.ComputeFingerprint(),
                Status = ArchitectureProposalStatus.Rejected,
                DiagnosticCode = code,
                DiagnosticMessage = message
            };
            PreviewImpact = null;
            return Preview;
        }

        private SnapResolution ResolveSnap(
            ArchitectureLevel level,
            ArchitecturePoint cursor,
            bool hasAnchor,
            ArchitecturePoint anchor,
            WallId excludedWall,
            bool useSnap,
            double maxDistance,
            VertexId excludedVertex = default(VertexId))
        {
            if (!useSnap) return new SnapResolution(cursor, null);
            var candidates = snapService.GenerateCandidates(new ArchitectureSnapRequest
            {
                Level = level.DeepClone(),
                Cursor = cursor,
                HasAnchor = hasAnchor,
                Anchor = anchor,
                ExcludedWallId = excludedWall,
                MaxDistance = maxDistance
            }).Where(x => x != null && !string.Equals(x.SourceEntityId, excludedVertex.Value, StringComparison.Ordinal)).ToList();
            LastSnapCandidates = candidates;
            var best = candidates.FirstOrDefault();
            if (best == null) return new SnapResolution(cursor, null);
            VertexId? vertexId = null;
            if (best.Type == ArchitectureSnapType.Vertex && ArchitectureId.IsValid(best.SourceEntityId))
                vertexId = new VertexId(best.SourceEntityId);
            return new SnapResolution(best.SnappedPoint, vertexId);
        }

        private static ArchitectureLevel FindWallLevel(ArchitectureSnapshot snapshot, WallId wallId)
        {
            return snapshot.Building.Levels.FirstOrDefault(level => level != null && level.Walls.Any(w => w != null && w.Id.Equals(wallId)));
        }

        private static ArchitectureLevel FindVertexLevel(ArchitectureSnapshot snapshot, VertexId vertexId)
        {
            return snapshot.Building.Levels.FirstOrDefault(level => level != null && level.Vertices.Any(v => v != null && v.Id.Equals(vertexId)));
        }

        private readonly struct SnapResolution
        {
            public readonly ArchitecturePoint Point;
            public readonly VertexId? VertexId;
            public SnapResolution(ArchitecturePoint point, VertexId? vertexId) { Point = point; VertexId = vertexId; }
        }
    }
}
