using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.ConstructionAuthoring
{
    public enum EntityKind { None, Wall, Room, Opening }
    public readonly struct ArchitectureHit
    {
        public readonly EntityKind Kind;
        public readonly BistroBuilderEditId Id;
        public readonly Vector2 Position;
        public readonly float Distance;
        public readonly float AlongHost;
        public bool IsValid => Kind != EntityKind.None && Id.IsValid;
        public ArchitectureHit(EntityKind kind, BistroBuilderEditId id, Vector2 position, float distance, float alongHost = 0f)
        { Kind = kind; Id = id; Position = position; Distance = distance; AlongHost = alongHost; }
    }

    /// <summary>Read-only, revision-scoped projection of the existing Draft and rooms.
    /// Refresh explicitly after a command/Undo/Redo, never from pointer motion.</summary>
    public sealed class ArchitectureQueryCache
    {
        internal readonly List<BistroBuilderWallRecord> Walls = new List<BistroBuilderWallRecord>();
        internal readonly List<BistroBuilderOpeningRecord> Openings = new List<BistroBuilderOpeningRecord>();
        internal readonly List<BistroBuilderRoomProjection> Rooms = new List<BistroBuilderRoomProjection>();
        private readonly Dictionary<BistroBuilderEditId, BistroBuilderWallRecord> wallById = new Dictionary<BistroBuilderEditId, BistroBuilderWallRecord>();
        internal BistroBuilderWallTopologyProjection Topology { get; private set; }
        public string SessionId { get; private set; }
        public long Revision { get; private set; } = -1;
        public int RebuildCount { get; private set; }

        public bool Refresh(BistroBuilderEditSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.State == BistroBuilderEditSessionState.Cancelled || session.State == BistroBuilderEditSessionState.Committed)
            { Walls.Clear(); Openings.Clear(); Rooms.Clear(); wallById.Clear(); Topology = null; SessionId = null; Revision = -1; return true; }
            if (SessionId == session.SessionId && Revision == session.DraftRevision) return false;
            Walls.Clear(); Openings.Clear(); Rooms.Clear(); wallById.Clear();
            foreach (var wall in session.Draft.walls) { var copy = wall.DeepClone(); Walls.Add(copy); wallById.Add(copy.wallId, copy); }
            foreach (var opening in session.Draft.openings) Openings.Add(opening.DeepClone());
            Walls.Sort((a,b) => a.wallId.CompareTo(b.wallId));
            Openings.Sort((a,b) => a.openingId.CompareTo(b.openingId));
            foreach (var room in session.RoomProjections)
            {
                var copy = new BistroBuilderRoomProjection { room = room.room.DeepClone(), area = room.area, centroid = room.centroid };
                copy.boundary.AddRange(room.boundary); Rooms.Add(copy);
            }
            Rooms.Sort((a,b) => a.room.roomId.CompareTo(b.room.roomId));
            Topology = new BistroBuilderWallTopologyBuilder().Build(Walls, session.DraftRevision);
            SessionId = session.SessionId; Revision = session.DraftRevision; RebuildCount++;
            return true;
        }

        public bool Matches(BistroBuilderEditSession session) => session != null && SessionId == session.SessionId && Revision == session.DraftRevision;
        internal BistroBuilderWallRecord Wall(BistroBuilderEditId id) => wallById.TryGetValue(id, out var wall) ? wall : null;
        public BistroBuilderWallRecord CaptureWall(BistroBuilderEditId id) => Wall(id)?.DeepClone();

        public ArchitectureHit PickWall(Vector2 point, float radius, string plane = "default")
        {
            ArchitectureHit best = default;
            foreach (var wall in Walls)
            {
                if (wall.buildPlaneId != plane) continue;
                var p = ConstructionGeometry.Project(point, wall.axisStart, wall.axisEnd, out float t);
                float distance = Vector2.Distance(point, p);
                if (distance <= radius && (!best.IsValid || distance < best.Distance))
                    best = new ArchitectureHit(EntityKind.Wall, wall.wallId, p, distance, t);
            }
            return best;
        }

        public ArchitectureHit PickOpening(Vector2 point, float radius, string plane = "default")
        {
            ArchitectureHit best = default;
            foreach (var opening in Openings)
            {
                var wall = Wall(opening.hostWallId);
                if (wall == null || wall.buildPlaneId != plane || wall.Length <= 0f) continue;
                float half = opening.width * 0.5f / wall.Length;
                var a = Vector2.Lerp(wall.axisStart, wall.axisEnd, opening.axisPosition01 - half);
                var b = Vector2.Lerp(wall.axisStart, wall.axisEnd, opening.axisPosition01 + half);
                var p = ConstructionGeometry.Project(point, a, b, out _);
                float distance = Vector2.Distance(point, p);
                if (distance <= radius && (!best.IsValid || distance < best.Distance))
                    best = new ArchitectureHit(EntityKind.Opening, opening.openingId, p, distance, opening.axisPosition01);
            }
            return best;
        }

        public ArchitectureHit PickRoom(Vector2 point, string plane = "default")
        {
            ArchitectureHit best = default;
            float area = float.MaxValue;
            foreach (var room in Rooms)
                if (room.room.buildPlaneId == plane && room.area < area &&
                    BistroBuilderRoomIdentityReconciler.PointInPolygon(point, room.boundary))
                { best = new ArchitectureHit(EntityKind.Room, room.room.roomId, point, 0f); area = room.area; }
            return best;
        }

        public ArchitectureHit Pick(Vector2 point, float radius, string plane = "default")
        {
            var hit = PickOpening(point, radius, plane);
            if (hit.IsValid) return hit;
            hit = PickWall(point, radius, plane);
            return hit.IsValid ? hit : PickRoom(point, plane);
        }

        public bool Contains(EntityKind kind, BistroBuilderEditId id)
        {
            if (kind == EntityKind.Wall) return Wall(id) != null;
            if (kind == EntityKind.Opening) return Openings.Exists(o => o.openingId == id);
            return kind == EntityKind.Room && Rooms.Exists(r => r.room.roomId == id);
        }
    }

    /// <summary>Selection only. Does not own or change architectural state.</summary>
    public sealed class ArchitectureSelection
    {
        public EntityKind Kind { get; private set; }
        public BistroBuilderEditId Id { get; private set; }
        private string sessionId;
        public bool Select(ArchitectureQueryCache queries, ArchitectureHit hit)
        {
            if (queries == null || !hit.IsValid || !queries.Contains(hit.Kind, hit.Id)) { Clear(); return false; }
            Kind = hit.Kind; Id = hit.Id; sessionId = queries.SessionId; return true;
        }
        public bool Reconcile(ArchitectureQueryCache queries)
        {
            if (queries == null || queries.SessionId != sessionId || !queries.Contains(Kind, Id)) { Clear(); return false; }
            return true;
        }
        public void Clear() { Kind = EntityKind.None; Id = default; sessionId = null; }
    }
}
