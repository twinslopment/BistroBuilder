using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    public enum ArchitectureOperationKind
    {
        CreateWall = 0,
        MoveWall = 1,
        SplitWall = 2,
        DeleteWall = 3,
        MoveVertex = 4,
        Composite = 5
    }

    public enum ArchitectureProposalStatus
    {
        Ready = 0,
        Rejected = 1,
        Conflict = 2
    }

    [Serializable]
    public sealed class ArchitectureOperationDescriptor
    {
        public ArchitectureOperationId Id;
        public ArchitectureOperationKind Kind;
        public string Label;
    }

    public sealed class ArchitectureOperationProposal
    {
        public ArchitectureOperationDescriptor Operation;
        public string BaseFingerprint;
        public string ProposedFingerprint;
        public ArchitectureSnapshot ProposedSnapshot;
        public ArchitectureProposalStatus Status;
        public string DiagnosticCode;
        public string DiagnosticMessage;
        public ArchitectureValidationResult Validation;

        public bool IsReady => Status == ArchitectureProposalStatus.Ready && ProposedSnapshot != null;
    }

    public sealed class ArchitectureCommittedOperation
    {
        public ArchitectureOperationDescriptor Operation;
        public ArchitectureSnapshot BeforeSnapshot;
        public ArchitectureSnapshot AfterSnapshot;
        public string BeforeFingerprint;
        public string AfterFingerprint;
    }

    /// <summary>
    /// Motor transaccional LA3. Toda mutación se resuelve sobre un DeepClone; el snapshot base no se toca
    /// hasta el commit. El commit verifica concurrencia por fingerprint y registra A/B para Undo/Redo semántico.
    /// </summary>
    public static class ArchitectureTransactionEngine
    {
        public static ArchitectureOperationProposal Propose(
            ArchitectureSnapshot current,
            ArchitectureOperationKind kind,
            string label,
            Action<ArchitectureSnapshot> mutation)
        {
            var descriptor = new ArchitectureOperationDescriptor
            {
                Id = ArchitectureOperationId.New(),
                Kind = kind,
                Label = string.IsNullOrWhiteSpace(label) ? kind.ToString() : label.Trim()
            };

            if (current == null)
                return Reject(descriptor, null, "LA3_NULL_BASE", "No existe snapshot base para proponer la operación.");
            if (mutation == null)
                return Reject(descriptor, current.ComputeFingerprint(), "LA3_NULL_MUTATION", "La operación no contiene una mutación.");

            var baseFingerprint = current.ComputeFingerprint();
            var proposed = current.DeepClone();
            try
            {
                mutation(proposed);
            }
            catch (Exception ex)
            {
                return Reject(descriptor, baseFingerprint, "LA3_MUTATION_EXCEPTION", ex.Message);
            }

            var validation = ArchitectureValidator.Validate(proposed);
            if (!validation.IsValid)
            {
                var first = validation.Issues.FirstOrDefault(x => x.Severity == ArchitectureValidationSeverity.Error);
                return new ArchitectureOperationProposal
                {
                    Operation = descriptor,
                    BaseFingerprint = baseFingerprint,
                    ProposedSnapshot = null,
                    Status = ArchitectureProposalStatus.Rejected,
                    DiagnosticCode = first?.Code ?? "LA3_INVALID_PROPOSAL",
                    DiagnosticMessage = first?.Message ?? "La propuesta viola invariantes del kernel.",
                    Validation = validation
                };
            }

            try
            {
                foreach (var level in proposed.Building.Levels ?? new List<ArchitectureLevel>())
                {
                    if (level != null) ArchitectureRegionEngine.Build(level);
                }
            }
            catch (Exception ex)
            {
                return Reject(descriptor, baseFingerprint, "LA3_TOPOLOGY_REJECTED", ex.Message);
            }

            return new ArchitectureOperationProposal
            {
                Operation = descriptor,
                BaseFingerprint = baseFingerprint,
                ProposedFingerprint = proposed.ComputeFingerprint(),
                ProposedSnapshot = proposed,
                Status = ArchitectureProposalStatus.Ready,
                Validation = validation
            };
        }

        public static bool TryCommit(
            ref ArchitectureSnapshot current,
            ArchitectureOperationProposal proposal,
            out ArchitectureCommittedOperation committed,
            out string diagnosticCode)
        {
            committed = null;
            diagnosticCode = null;
            if (current == null || proposal == null || !proposal.IsReady)
            {
                diagnosticCode = "LA3_COMMIT_NOT_READY";
                return false;
            }

            var liveFingerprint = current.ComputeFingerprint();
            if (!string.Equals(liveFingerprint, proposal.BaseFingerprint, StringComparison.Ordinal))
            {
                proposal.Status = ArchitectureProposalStatus.Conflict;
                proposal.DiagnosticCode = "LA3_STALE_PROPOSAL";
                proposal.DiagnosticMessage = "El estado arquitectónico cambió desde que se calculó la propuesta.";
                diagnosticCode = proposal.DiagnosticCode;
                return false;
            }

            var finalValidation = ArchitectureValidator.Validate(proposal.ProposedSnapshot);
            if (!finalValidation.IsValid)
            {
                diagnosticCode = "LA3_COMMIT_INVALID";
                return false;
            }

            var before = current.DeepClone();
            var after = proposal.ProposedSnapshot.DeepClone();
            current = after;
            committed = new ArchitectureCommittedOperation
            {
                Operation = proposal.Operation,
                BeforeSnapshot = before,
                AfterSnapshot = after.DeepClone(),
                BeforeFingerprint = liveFingerprint,
                AfterFingerprint = after.ComputeFingerprint()
            };
            return true;
        }

        public static bool TryUndo(ref ArchitectureSnapshot current, ArchitectureCommittedOperation operation, out string diagnosticCode)
        {
            diagnosticCode = null;
            if (current == null || operation?.BeforeSnapshot == null || operation.AfterSnapshot == null)
            {
                diagnosticCode = "LA3_UNDO_INVALID_RECORD";
                return false;
            }
            if (!string.Equals(current.ComputeFingerprint(), operation.AfterFingerprint, StringComparison.Ordinal))
            {
                diagnosticCode = "LA3_UNDO_STATE_MISMATCH";
                return false;
            }
            current = operation.BeforeSnapshot.DeepClone();
            return true;
        }

        public static bool TryRedo(ref ArchitectureSnapshot current, ArchitectureCommittedOperation operation, out string diagnosticCode)
        {
            diagnosticCode = null;
            if (current == null || operation?.BeforeSnapshot == null || operation.AfterSnapshot == null)
            {
                diagnosticCode = "LA3_REDO_INVALID_RECORD";
                return false;
            }
            if (!string.Equals(current.ComputeFingerprint(), operation.BeforeFingerprint, StringComparison.Ordinal))
            {
                diagnosticCode = "LA3_REDO_STATE_MISMATCH";
                return false;
            }
            current = operation.AfterSnapshot.DeepClone();
            return true;
        }

        private static ArchitectureOperationProposal Reject(ArchitectureOperationDescriptor descriptor, string baseFingerprint, string code, string message)
        {
            return new ArchitectureOperationProposal
            {
                Operation = descriptor,
                BaseFingerprint = baseFingerprint,
                Status = ArchitectureProposalStatus.Rejected,
                DiagnosticCode = code,
                DiagnosticMessage = message
            };
        }
    }

    /// <summary>
    /// Catálogo de mutaciones primitivas LA3. No ejecutan commit: solo modifican el snapshot de propuesta.
    /// </summary>
    public static class ArchitectureMutations
    {
        public static void CreateWall(
            ArchitectureSnapshot snapshot,
            LevelId levelId,
            VertexId startVertexId,
            VertexId endVertexId,
            WallId wallId,
            double thickness,
            double height)
        {
            var level = RequireLevel(snapshot, levelId);
            RequireVertex(level, startVertexId);
            RequireVertex(level, endVertexId);
            if (level.Walls.Any(x => x != null && x.Id.Equals(wallId)))
                throw new InvalidOperationException("LA3_WALL_ID_EXISTS");

            level.Walls.Add(new ArchitectureWall
            {
                Id = wallId,
                StartVertexId = startVertexId,
                EndVertexId = endVertexId,
                Thickness = thickness,
                Height = height,
                Openings = new List<ArchitectureOpening>()
            });
        }

        public static void DeleteWall(ArchitectureSnapshot snapshot, WallId wallId)
        {
            var level = FindWallLevel(snapshot, wallId, out var wall);
            if (wall == null) throw new InvalidOperationException("LA3_WALL_NOT_FOUND");
            level.Walls.Remove(wall);
        }

        public static void MoveVertex(ArchitectureSnapshot snapshot, VertexId vertexId, ArchitecturePoint target)
        {
            var vertex = snapshot?.FindVertex(vertexId);
            if (vertex == null) throw new InvalidOperationException("LA3_VERTEX_NOT_FOUND");
            vertex.Position = target;
        }

        public static void MoveWall(ArchitectureSnapshot snapshot, WallId wallId, double deltaX, double deltaY)
        {
            var level = FindWallLevel(snapshot, wallId, out var wall);
            var start = RequireVertex(level, wall.StartVertexId);
            var end = RequireVertex(level, wall.EndVertexId);
            start.Position = new ArchitecturePoint(start.Position.X + deltaX, start.Position.Y + deltaY);
            end.Position = new ArchitecturePoint(end.Position.X + deltaX, end.Position.Y + deltaY);
        }

        /// <summary>
        /// Divide una pared conservando su WallId en el primer tramo y creando un segundo WallId.
        /// Las aperturas se remapean paramétricamente; si una apertura cruza el punto de corte, se rechaza.
        /// </summary>
        public static void SplitWall(
            ArchitectureSnapshot snapshot,
            WallId wallId,
            double splitT,
            VertexId splitVertexId,
            WallId secondWallId)
        {
            if (splitT <= ArchitectureGeometry.Epsilon || splitT >= 1d - ArchitectureGeometry.Epsilon)
                throw new InvalidOperationException("LA3_SPLIT_T_DOMAIN");

            var level = FindWallLevel(snapshot, wallId, out var wall);
            if (level.Walls.Any(x => x != null && x.Id.Equals(secondWallId)))
                throw new InvalidOperationException("LA3_SECOND_WALL_ID_EXISTS");
            if (level.Vertices.Any(x => x != null && x.Id.Equals(splitVertexId)))
                throw new InvalidOperationException("LA3_SPLIT_VERTEX_ID_EXISTS");

            var start = RequireVertex(level, wall.StartVertexId);
            var end = RequireVertex(level, wall.EndVertexId);
            var splitPoint = new ArchitecturePoint(
                start.Position.X + ((end.Position.X - start.Position.X) * splitT),
                start.Position.Y + ((end.Position.Y - start.Position.Y) * splitT));

            var firstOpenings = new List<ArchitectureOpening>();
            var secondOpenings = new List<ArchitectureOpening>();
            var length = start.Position.DistanceTo(end.Position);
            foreach (var opening in wall.Openings ?? new List<ArchitectureOpening>())
            {
                if (opening == null) continue;
                var halfT = (opening.Width / length) * 0.5d;
                if (opening.CenterT - halfT < splitT && opening.CenterT + halfT > splitT)
                    throw new InvalidOperationException("LA3_SPLIT_CROSSES_OPENING");

                if (opening.CenterT < splitT)
                {
                    var clone = opening.DeepClone();
                    clone.CenterT = opening.CenterT / splitT;
                    firstOpenings.Add(clone);
                }
                else
                {
                    var clone = opening.DeepClone();
                    clone.WallId = secondWallId;
                    clone.CenterT = (opening.CenterT - splitT) / (1d - splitT);
                    secondOpenings.Add(clone);
                }
            }

            level.Vertices.Add(new ArchitectureVertex { Id = splitVertexId, Position = splitPoint });
            var originalEnd = wall.EndVertexId;
            wall.EndVertexId = splitVertexId;
            wall.Openings = firstOpenings;
            level.Walls.Add(new ArchitectureWall
            {
                Id = secondWallId,
                StartVertexId = splitVertexId,
                EndVertexId = originalEnd,
                Thickness = wall.Thickness,
                Height = wall.Height,
                Openings = secondOpenings
            });
        }

        private static ArchitectureLevel RequireLevel(ArchitectureSnapshot snapshot, LevelId levelId)
        {
            var level = snapshot?.FindLevel(levelId);
            if (level == null) throw new InvalidOperationException("LA3_LEVEL_NOT_FOUND");
            return level;
        }

        private static ArchitectureVertex RequireVertex(ArchitectureLevel level, VertexId vertexId)
        {
            var vertex = level?.Vertices?.FirstOrDefault(x => x != null && x.Id.Equals(vertexId));
            if (vertex == null) throw new InvalidOperationException("LA3_VERTEX_NOT_FOUND");
            return vertex;
        }

        private static ArchitectureLevel FindWallLevel(ArchitectureSnapshot snapshot, WallId wallId, out ArchitectureWall wall)
        {
            wall = null;
            foreach (var level in snapshot?.Building?.Levels ?? new List<ArchitectureLevel>())
            {
                wall = level?.Walls?.FirstOrDefault(x => x != null && x.Id.Equals(wallId));
                if (wall != null) return level;
            }
            throw new InvalidOperationException("LA3_WALL_NOT_FOUND");
        }
    }
}
