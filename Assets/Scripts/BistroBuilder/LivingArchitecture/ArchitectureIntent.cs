using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    public enum ArchitectureConstraintKind
    {
        FixedVertex = 0,
        WallLength = 1,
        WallAngle = 2,
        OpeningCentered = 3,
        OpeningOffsetFromStart = 4,
        RegionArea = 5
    }

    public enum ArchitectureConstraintSeverity
    {
        Hard = 0,
        Advisory = 1
    }

    [Serializable]
    public sealed class ArchitectureConstraint
    {
        public ArchitectureConstraintKind Kind;
        public ArchitectureConstraintSeverity Severity = ArchitectureConstraintSeverity.Hard;
        public string EntityId;
        public string RegionId;
        public double TargetValue;
        public double Tolerance = ArchitectureGeometry.Epsilon;
        public ArchitecturePoint TargetPoint;
    }

    [Serializable]
    public sealed class ArchitectureIntent
    {
        public string Label;
        public readonly List<ArchitectureConstraint> Constraints = new List<ArchitectureConstraint>();
    }

    public sealed class ArchitectureConstraintEvaluation
    {
        public ArchitectureConstraint Constraint;
        public bool Satisfied;
        public string Code;
        public string Message;
        public double ActualValue;
        public double TargetValue;
    }

    public sealed class ArchitectureIntentResult
    {
        public ArchitectureOperationProposal Proposal;
        public readonly List<ArchitectureConstraintEvaluation> Evaluations = new List<ArchitectureConstraintEvaluation>();
        public bool IsReady => Proposal != null && Proposal.IsReady && Evaluations.All(x => x.Constraint == null || x.Constraint.Severity != ArchitectureConstraintSeverity.Hard || x.Satisfied);
    }

    /// <summary>
    /// LA4: capa de intención y restricciones sobre LA3. No muta A, no toca Unity y no adivina cambios grandes.
    /// Las correcciones soportadas son locales, cerradas y deterministas; lo no resoluble se rechaza explícitamente.
    /// </summary>
    public static class ArchitectureIntentEngine
    {
        public static ArchitectureIntentResult Propose(
            ArchitectureSnapshot current,
            ArchitectureOperationKind kind,
            ArchitectureIntent intent,
            Action<ArchitectureSnapshot> requestedMutation)
        {
            var result = new ArchitectureIntentResult();
            if (current == null)
            {
                result.Proposal = ArchitectureTransactionEngine.Propose(null, kind, intent?.Label, requestedMutation);
                return result;
            }

            var constraints = intent?.Constraints ?? new List<ArchitectureConstraint>();
            var baseSnapshot = current.DeepClone();
            result.Proposal = ArchitectureTransactionEngine.Propose(current, kind, intent?.Label, snapshot =>
            {
                requestedMutation?.Invoke(snapshot);
                ApplyDeterministicCorrections(baseSnapshot, snapshot, constraints);
            });

            if (result.Proposal?.ProposedSnapshot == null)
                return result;

            Evaluate(baseSnapshot, result.Proposal.ProposedSnapshot, constraints, result.Evaluations);
            var failedHard = result.Evaluations.FirstOrDefault(x => x.Constraint != null && x.Constraint.Severity == ArchitectureConstraintSeverity.Hard && !x.Satisfied);
            if (failedHard != null)
            {
                result.Proposal.Status = ArchitectureProposalStatus.Rejected;
                result.Proposal.DiagnosticCode = failedHard.Code;
                result.Proposal.DiagnosticMessage = failedHard.Message;
                result.Proposal.ProposedSnapshot = null;
                result.Proposal.ProposedFingerprint = null;
            }
            return result;
        }

        private static void ApplyDeterministicCorrections(ArchitectureSnapshot before, ArchitectureSnapshot proposed, IEnumerable<ArchitectureConstraint> constraints)
        {
            foreach (var constraint in constraints.Where(x => x != null && x.Severity == ArchitectureConstraintSeverity.Hard))
            {
                switch (constraint.Kind)
                {
                    case ArchitectureConstraintKind.FixedVertex:
                        ApplyFixedVertex(proposed, constraint);
                        break;
                    case ArchitectureConstraintKind.WallLength:
                        ApplyWallLength(proposed, constraint);
                        break;
                    case ArchitectureConstraintKind.WallAngle:
                        ApplyWallAngle(proposed, constraint);
                        break;
                    case ArchitectureConstraintKind.OpeningCentered:
                        ApplyOpeningCentered(proposed, constraint);
                        break;
                    case ArchitectureConstraintKind.OpeningOffsetFromStart:
                        ApplyOpeningOffset(proposed, constraint);
                        break;
                    case ArchitectureConstraintKind.RegionArea:
                        // El área se valida, pero LA4 V1 no ejecuta un solver global para corregirla.
                        break;
                }
            }
        }

        private static void ApplyFixedVertex(ArchitectureSnapshot snapshot, ArchitectureConstraint constraint)
        {
            var vertex = snapshot.FindVertex(new VertexId(constraint.EntityId));
            if (vertex == null) throw new InvalidOperationException("LA4_VERTEX_NOT_FOUND");
            vertex.Position = constraint.TargetPoint;
        }

        private static void ApplyWallLength(ArchitectureSnapshot snapshot, ArchitectureConstraint constraint)
        {
            if (constraint.TargetValue <= ArchitectureGeometry.Epsilon) throw new InvalidOperationException("LA4_INVALID_LENGTH");
            var wall = snapshot.FindWall(new WallId(constraint.EntityId));
            if (wall == null) throw new InvalidOperationException("LA4_WALL_NOT_FOUND");
            var start = snapshot.FindVertex(wall.StartVertexId);
            var end = snapshot.FindVertex(wall.EndVertexId);
            if (start == null || end == null) throw new InvalidOperationException("LA4_WALL_VERTEX_NOT_FOUND");
            var dx = end.Position.X - start.Position.X;
            var dy = end.Position.Y - start.Position.Y;
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length <= ArchitectureGeometry.Epsilon) throw new InvalidOperationException("LA4_DEGENERATE_DIRECTION");
            end.Position = new ArchitecturePoint(start.Position.X + (dx / length * constraint.TargetValue), start.Position.Y + (dy / length * constraint.TargetValue));
        }

        private static void ApplyWallAngle(ArchitectureSnapshot snapshot, ArchitectureConstraint constraint)
        {
            var wall = snapshot.FindWall(new WallId(constraint.EntityId));
            if (wall == null) throw new InvalidOperationException("LA4_WALL_NOT_FOUND");
            var start = snapshot.FindVertex(wall.StartVertexId);
            var end = snapshot.FindVertex(wall.EndVertexId);
            if (start == null || end == null) throw new InvalidOperationException("LA4_WALL_VERTEX_NOT_FOUND");
            var length = start.Position.DistanceTo(end.Position);
            if (length <= ArchitectureGeometry.Epsilon) throw new InvalidOperationException("LA4_DEGENERATE_DIRECTION");
            var radians = constraint.TargetValue * Math.PI / 180d;
            end.Position = new ArchitecturePoint(start.Position.X + Math.Cos(radians) * length, start.Position.Y + Math.Sin(radians) * length);
        }

        private static void ApplyOpeningCentered(ArchitectureSnapshot snapshot, ArchitectureConstraint constraint)
        {
            var opening = FindOpening(snapshot, constraint.EntityId);
            if (opening == null) throw new InvalidOperationException("LA4_OPENING_NOT_FOUND");
            opening.CenterT = 0.5d;
        }

        private static void ApplyOpeningOffset(ArchitectureSnapshot snapshot, ArchitectureConstraint constraint)
        {
            var opening = FindOpening(snapshot, constraint.EntityId);
            if (opening == null) throw new InvalidOperationException("LA4_OPENING_NOT_FOUND");
            var wall = snapshot.FindWall(opening.WallId);
            if (wall == null) throw new InvalidOperationException("LA4_WALL_NOT_FOUND");
            var start = snapshot.FindVertex(wall.StartVertexId);
            var end = snapshot.FindVertex(wall.EndVertexId);
            var length = start.Position.DistanceTo(end.Position);
            if (length <= ArchitectureGeometry.Epsilon) throw new InvalidOperationException("LA4_DEGENERATE_DIRECTION");
            opening.CenterT = constraint.TargetValue / length;
        }

        private static ArchitectureOpening FindOpening(ArchitectureSnapshot snapshot, string id)
        {
            if (snapshot?.Building?.Levels == null) return null;
            return snapshot.Building.Levels.Where(x => x != null).SelectMany(x => x.Walls ?? new List<ArchitectureWall>()).Where(x => x != null)
                .SelectMany(x => x.Openings ?? new List<ArchitectureOpening>()).FirstOrDefault(x => x != null && string.Equals(x.Id.Value, id, StringComparison.Ordinal));
        }

        private static void Evaluate(ArchitectureSnapshot before, ArchitectureSnapshot proposed, IEnumerable<ArchitectureConstraint> constraints, List<ArchitectureConstraintEvaluation> output)
        {
            foreach (var constraint in constraints.Where(x => x != null))
            {
                var evaluation = EvaluateOne(before, proposed, constraint);
                output.Add(evaluation);
            }
        }

        private static ArchitectureConstraintEvaluation EvaluateOne(ArchitectureSnapshot before, ArchitectureSnapshot proposed, ArchitectureConstraint constraint)
        {
            var tolerance = Math.Max(ArchitectureGeometry.Epsilon, Math.Abs(constraint.Tolerance));
            switch (constraint.Kind)
            {
                case ArchitectureConstraintKind.FixedVertex:
                {
                    var vertex = proposed.FindVertex(new VertexId(constraint.EntityId));
                    var ok = vertex != null && vertex.Position.DistanceTo(constraint.TargetPoint) <= tolerance;
                    return Make(constraint, ok, "LA4_FIXED_VERTEX_UNSATISFIED", ok ? 0d : double.NaN, 0d);
                }
                case ArchitectureConstraintKind.WallLength:
                {
                    var actual = WallLength(proposed, constraint.EntityId);
                    return Make(constraint, !double.IsNaN(actual) && Math.Abs(actual - constraint.TargetValue) <= tolerance, "LA4_LENGTH_UNSATISFIED", actual, constraint.TargetValue);
                }
                case ArchitectureConstraintKind.WallAngle:
                {
                    var actual = WallAngle(proposed, constraint.EntityId);
                    var delta = AngularDistance(actual, constraint.TargetValue);
                    return Make(constraint, !double.IsNaN(actual) && delta <= tolerance, "LA4_ANGLE_UNSATISFIED", actual, constraint.TargetValue);
                }
                case ArchitectureConstraintKind.OpeningCentered:
                {
                    var opening = FindOpening(proposed, constraint.EntityId);
                    var actual = opening?.CenterT ?? double.NaN;
                    return Make(constraint, opening != null && Math.Abs(actual - 0.5d) <= tolerance, "LA4_OPENING_CENTER_UNSATISFIED", actual, 0.5d);
                }
                case ArchitectureConstraintKind.OpeningOffsetFromStart:
                {
                    var opening = FindOpening(proposed, constraint.EntityId);
                    var actual = double.NaN;
                    if (opening != null)
                    {
                        var wallLength = WallLength(proposed, opening.WallId.Value);
                        actual = wallLength * opening.CenterT;
                    }
                    return Make(constraint, !double.IsNaN(actual) && Math.Abs(actual - constraint.TargetValue) <= tolerance, "LA4_OPENING_OFFSET_UNSATISFIED", actual, constraint.TargetValue);
                }
                case ArchitectureConstraintKind.RegionArea:
                {
                    var level = FindRegionLevel(proposed, constraint.RegionId, out var actual);
                    return Make(constraint, level != null && !double.IsNaN(actual) && Math.Abs(actual - constraint.TargetValue) <= tolerance, "LA4_REGION_AREA_UNSATISFIED", actual, constraint.TargetValue);
                }
                default:
                    return Make(constraint, false, "LA4_UNKNOWN_CONSTRAINT", double.NaN, constraint.TargetValue);
            }
        }

        private static ArchitectureConstraintEvaluation Make(ArchitectureConstraint c, bool satisfied, string code, double actual, double target)
        {
            return new ArchitectureConstraintEvaluation
            {
                Constraint = c,
                Satisfied = satisfied,
                Code = satisfied ? "LA4_OK" : code,
                Message = satisfied ? "Restricción satisfecha." : "La propuesta no puede preservar la restricción solicitada sin una corrección no autorizada.",
                ActualValue = actual,
                TargetValue = target
            };
        }

        private static double WallLength(ArchitectureSnapshot snapshot, string wallId)
        {
            var wall = snapshot.FindWall(new WallId(wallId));
            if (wall == null) return double.NaN;
            var a = snapshot.FindVertex(wall.StartVertexId);
            var b = snapshot.FindVertex(wall.EndVertexId);
            return a == null || b == null ? double.NaN : a.Position.DistanceTo(b.Position);
        }

        private static double WallAngle(ArchitectureSnapshot snapshot, string wallId)
        {
            var wall = snapshot.FindWall(new WallId(wallId));
            if (wall == null) return double.NaN;
            var a = snapshot.FindVertex(wall.StartVertexId);
            var b = snapshot.FindVertex(wall.EndVertexId);
            if (a == null || b == null) return double.NaN;
            var degrees = Math.Atan2(b.Position.Y - a.Position.Y, b.Position.X - a.Position.X) * 180d / Math.PI;
            return degrees < 0d ? degrees + 360d : degrees;
        }

        private static double AngularDistance(double a, double b)
        {
            if (double.IsNaN(a) || double.IsNaN(b)) return double.PositiveInfinity;
            var delta = Math.Abs((a - b) % 360d);
            return Math.Min(delta, 360d - delta);
        }

        private static ArchitectureLevel FindRegionLevel(ArchitectureSnapshot snapshot, string regionId, out double area)
        {
            area = double.NaN;
            if (snapshot?.Building?.Levels == null) return null;
            foreach (var level in snapshot.Building.Levels.Where(x => x != null))
            {
                var region = ArchitectureRegionEngine.Build(level).Regions.FirstOrDefault(x => x != null && string.Equals(x.Id.Value, regionId, StringComparison.Ordinal));
                if (region == null) continue;
                area = region.Area;
                return level;
            }
            return null;
        }
    }
}
