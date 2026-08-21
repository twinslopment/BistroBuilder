using System;
using System.Collections.Generic;

namespace BistroBuilder.LivingArchitecture.Domain
{
    public enum ArchitectureSnapType
    {
        Vertex,
        WallProjection,
        Parallel,
        Perpendicular,
        EqualLength,
        Continuity
    }

    public enum ArchitectureSnapConfidence
    {
        Low,
        Medium,
        High
    }

    [Serializable]
    public sealed class ArchitectureSnapRequest
    {
        public ArchitectureLevel Level;
        public ArchitecturePoint Cursor;
        public bool HasAnchor;
        public ArchitecturePoint Anchor;
        public WallId ExcludedWallId;
        public double MaxDistance = 0.35d;
        public double AngleToleranceDegrees = 5d;
        public double EqualLengthTolerance = 0.25d;
    }

    [Serializable]
    public sealed class ArchitectureSnapCandidate
    {
        public ArchitectureSnapType Type;
        public ArchitectureSnapConfidence Confidence;
        public ArchitecturePoint SnappedPoint;
        public string SourceEntityId;
        public string ReasonCode;
        public double Score;
        public double Distance;
        public double? TargetAngleDegrees;
        public double? TargetLength;
    }

    /// <summary>
    /// Generador puro de candidatos de snap. No muta arquitectura ni decide qué candidato aplicar.
    /// </summary>
    public sealed class ArchitectureSnapService
    {
        public IReadOnlyList<ArchitectureSnapCandidate> GenerateCandidates(ArchitectureSnapRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Level == null) throw new ArgumentException("Snap requires a level.", nameof(request));

            var candidates = new List<ArchitectureSnapCandidate>();
            AddVertexCandidates(request, candidates);
            AddWallProjectionCandidates(request, candidates);

            if (request.HasAnchor && request.Anchor.DistanceTo(request.Cursor) > ArchitectureGeometry.Epsilon)
            {
                AddDirectionalCandidates(request, candidates);
                AddEqualLengthCandidates(request, candidates);
                AddContinuityCandidates(request, candidates);
            }

            candidates.Sort(CompareCandidates);
            return candidates;
        }

        private static void AddVertexCandidates(ArchitectureSnapRequest request, List<ArchitectureSnapCandidate> candidates)
        {
            foreach (var vertex in request.Level.Vertices)
            {
                if (vertex == null) continue;
                var distance = request.Cursor.DistanceTo(vertex.Position);
                if (distance > request.MaxDistance) continue;

                candidates.Add(new ArchitectureSnapCandidate
                {
                    Type = ArchitectureSnapType.Vertex,
                    Confidence = ConfidenceForDistance(distance, request.MaxDistance),
                    SnappedPoint = vertex.Position,
                    SourceEntityId = vertex.Id.ToString(),
                    ReasonCode = "SNAP_VERTEX",
                    Distance = distance,
                    Score = ScoreFromDistance(distance, request.MaxDistance)
                });
            }
        }

        private static void AddWallProjectionCandidates(ArchitectureSnapRequest request, List<ArchitectureSnapCandidate> candidates)
        {
            foreach (var wall in request.Level.Walls)
            {
                if (wall == null || wall.Id.Equals(request.ExcludedWallId)) continue;
                ArchitecturePoint a;
                ArchitecturePoint b;
                if (!TryGetWallPoints(request.Level, wall, out a, out b)) continue;

                double t;
                var projected = ProjectPointToSegment(request.Cursor, a, b, out t);
                var distance = request.Cursor.DistanceTo(projected);
                if (distance > request.MaxDistance) continue;

                candidates.Add(new ArchitectureSnapCandidate
                {
                    Type = ArchitectureSnapType.WallProjection,
                    Confidence = ConfidenceForDistance(distance, request.MaxDistance),
                    SnappedPoint = projected,
                    SourceEntityId = wall.Id.ToString(),
                    ReasonCode = "SNAP_WALL_ALIGNMENT",
                    Distance = distance,
                    Score = 0.75d * ScoreFromDistance(distance, request.MaxDistance)
                });
            }
        }

        private static void AddDirectionalCandidates(ArchitectureSnapRequest request, List<ArchitectureSnapCandidate> candidates)
        {
            var draftDx = request.Cursor.X - request.Anchor.X;
            var draftDy = request.Cursor.Y - request.Anchor.Y;
            var draftLength = Math.Sqrt((draftDx * draftDx) + (draftDy * draftDy));
            var draftAngle = NormalizeAngle(Math.Atan2(draftDy, draftDx) * 180d / Math.PI);

            foreach (var wall in request.Level.Walls)
            {
                if (wall == null || wall.Id.Equals(request.ExcludedWallId)) continue;
                ArchitecturePoint a;
                ArchitecturePoint b;
                if (!TryGetWallPoints(request.Level, wall, out a, out b)) continue;

                var wallAngle = NormalizeAngle(Math.Atan2(b.Y - a.Y, b.X - a.X) * 180d / Math.PI);
                TryAddAngleCandidate(request, candidates, wall, draftLength, draftAngle, wallAngle, ArchitectureSnapType.Parallel, "SNAP_PARALLEL");
                TryAddAngleCandidate(request, candidates, wall, draftLength, draftAngle, NormalizeAngle(wallAngle + 90d), ArchitectureSnapType.Perpendicular, "SNAP_PERPENDICULAR");
            }
        }

        private static void TryAddAngleCandidate(
            ArchitectureSnapRequest request,
            List<ArchitectureSnapCandidate> candidates,
            ArchitectureWall sourceWall,
            double draftLength,
            double draftAngle,
            double targetAngle,
            ArchitectureSnapType type,
            string reasonCode)
        {
            var angleDelta = SmallestAxisAngleDelta(draftAngle, targetAngle);
            if (angleDelta > request.AngleToleranceDegrees) return;

            var radians = targetAngle * Math.PI / 180d;
            var snapped = new ArchitecturePoint(
                request.Anchor.X + (Math.Cos(radians) * draftLength),
                request.Anchor.Y + (Math.Sin(radians) * draftLength));

            candidates.Add(new ArchitectureSnapCandidate
            {
                Type = type,
                Confidence = ConfidenceForAngle(angleDelta, request.AngleToleranceDegrees),
                SnappedPoint = snapped,
                SourceEntityId = sourceWall.Id.ToString(),
                ReasonCode = reasonCode,
                Distance = request.Cursor.DistanceTo(snapped),
                Score = 0.85d * ScoreFromDistance(angleDelta, request.AngleToleranceDegrees),
                TargetAngleDegrees = NormalizeAxisAngle(targetAngle)
            });
        }

        private static void AddEqualLengthCandidates(ArchitectureSnapRequest request, List<ArchitectureSnapCandidate> candidates)
        {
            var dx = request.Cursor.X - request.Anchor.X;
            var dy = request.Cursor.Y - request.Anchor.Y;
            var draftLength = Math.Sqrt((dx * dx) + (dy * dy));
            if (draftLength <= ArchitectureGeometry.Epsilon) return;

            var unitX = dx / draftLength;
            var unitY = dy / draftLength;

            foreach (var wall in request.Level.Walls)
            {
                if (wall == null || wall.Id.Equals(request.ExcludedWallId)) continue;
                ArchitecturePoint a;
                ArchitecturePoint b;
                if (!TryGetWallPoints(request.Level, wall, out a, out b)) continue;

                var targetLength = a.DistanceTo(b);
                var delta = Math.Abs(targetLength - draftLength);
                if (delta > request.EqualLengthTolerance) continue;

                var snapped = new ArchitecturePoint(
                    request.Anchor.X + (unitX * targetLength),
                    request.Anchor.Y + (unitY * targetLength));

                candidates.Add(new ArchitectureSnapCandidate
                {
                    Type = ArchitectureSnapType.EqualLength,
                    Confidence = ConfidenceForDistance(delta, request.EqualLengthTolerance),
                    SnappedPoint = snapped,
                    SourceEntityId = wall.Id.ToString(),
                    ReasonCode = "SNAP_EQUAL_LENGTH",
                    Distance = request.Cursor.DistanceTo(snapped),
                    Score = 0.80d * ScoreFromDistance(delta, request.EqualLengthTolerance),
                    TargetLength = targetLength
                });
            }
        }

        private static void AddContinuityCandidates(ArchitectureSnapRequest request, List<ArchitectureSnapCandidate> candidates)
        {
            foreach (var wall in request.Level.Walls)
            {
                if (wall == null || wall.Id.Equals(request.ExcludedWallId)) continue;
                ArchitecturePoint a;
                ArchitecturePoint b;
                if (!TryGetWallPoints(request.Level, wall, out a, out b)) continue;

                ArchitecturePoint far;
                if (request.Anchor.DistanceTo(a) <= request.MaxDistance) far = b;
                else if (request.Anchor.DistanceTo(b) <= request.MaxDistance) far = a;
                else continue;

                var awayX = request.Anchor.X - far.X;
                var awayY = request.Anchor.Y - far.Y;
                var awayLength = Math.Sqrt((awayX * awayX) + (awayY * awayY));
                if (awayLength <= ArchitectureGeometry.Epsilon) continue;

                var draftLength = request.Anchor.DistanceTo(request.Cursor);
                var unitX = awayX / awayLength;
                var unitY = awayY / awayLength;
                var snapped = new ArchitecturePoint(
                    request.Anchor.X + (unitX * draftLength),
                    request.Anchor.Y + (unitY * draftLength));
                var miss = request.Cursor.DistanceTo(snapped);
                if (miss > request.MaxDistance) continue;

                candidates.Add(new ArchitectureSnapCandidate
                {
                    Type = ArchitectureSnapType.Continuity,
                    Confidence = ConfidenceForDistance(miss, request.MaxDistance),
                    SnappedPoint = snapped,
                    SourceEntityId = wall.Id.ToString(),
                    ReasonCode = "SNAP_CONTINUITY",
                    Distance = miss,
                    Score = 0.95d * ScoreFromDistance(miss, request.MaxDistance)
                });
            }
        }

        private static bool TryGetWallPoints(ArchitectureLevel level, ArchitectureWall wall, out ArchitecturePoint a, out ArchitecturePoint b)
        {
            a = default(ArchitecturePoint);
            b = default(ArchitecturePoint);
            ArchitectureVertex start = null;
            ArchitectureVertex end = null;

            foreach (var vertex in level.Vertices)
            {
                if (vertex == null) continue;
                if (vertex.Id.Equals(wall.StartVertexId)) start = vertex;
                if (vertex.Id.Equals(wall.EndVertexId)) end = vertex;
            }

            if (start == null || end == null) return false;
            a = start.Position;
            b = end.Position;
            return true;
        }

        private static ArchitecturePoint ProjectPointToSegment(ArchitecturePoint p, ArchitecturePoint a, ArchitecturePoint b, out double t)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var lengthSquared = (dx * dx) + (dy * dy);
            if (lengthSquared <= ArchitectureGeometry.Epsilon * ArchitectureGeometry.Epsilon)
            {
                t = 0d;
                return a;
            }

            t = (((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / lengthSquared;
            t = Math.Max(0d, Math.Min(1d, t));
            return new ArchitecturePoint(a.X + (dx * t), a.Y + (dy * t));
        }

        private static double NormalizeAngle(double angle)
        {
            angle %= 360d;
            if (angle < 0d) angle += 360d;
            return angle;
        }

        private static double NormalizeAxisAngle(double angle)
        {
            angle = NormalizeAngle(angle);
            if (angle >= 180d) angle -= 180d;
            return angle;
        }

        private static double SmallestAxisAngleDelta(double a, double b)
        {
            var aa = NormalizeAxisAngle(a);
            var bb = NormalizeAxisAngle(b);
            var delta = Math.Abs(aa - bb);
            return Math.Min(delta, 180d - delta);
        }

        private static ArchitectureSnapConfidence ConfidenceForDistance(double distance, double tolerance)
        {
            if (tolerance <= ArchitectureGeometry.Epsilon) return ArchitectureSnapConfidence.High;
            var ratio = distance / tolerance;
            if (ratio <= 0.25d) return ArchitectureSnapConfidence.High;
            if (ratio <= 0.60d) return ArchitectureSnapConfidence.Medium;
            return ArchitectureSnapConfidence.Low;
        }

        private static ArchitectureSnapConfidence ConfidenceForAngle(double delta, double tolerance)
        {
            return ConfidenceForDistance(delta, tolerance);
        }

        private static double ScoreFromDistance(double distance, double tolerance)
        {
            if (tolerance <= ArchitectureGeometry.Epsilon) return 1d;
            return Math.Max(0d, 1d - (distance / tolerance));
        }

        private static int CompareCandidates(ArchitectureSnapCandidate a, ArchitectureSnapCandidate b)
        {
            var scoreCompare = b.Score.CompareTo(a.Score);
            if (scoreCompare != 0) return scoreCompare;
            var typeCompare = a.Type.CompareTo(b.Type);
            if (typeCompare != 0) return typeCompare;
            var sourceCompare = string.CompareOrdinal(a.SourceEntityId, b.SourceEntityId);
            if (sourceCompare != 0) return sourceCompare;
            var xCompare = a.SnappedPoint.X.CompareTo(b.SnappedPoint.X);
            if (xCompare != 0) return xCompare;
            return a.SnappedPoint.Y.CompareTo(b.SnappedPoint.Y);
        }
    }
}
