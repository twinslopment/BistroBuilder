using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[Serializable]
public struct BistroBuilderEditId : IEquatable<BistroBuilderEditId>, IComparable<BistroBuilderEditId>
{
    [SerializeField] private string value;
    public string Value => value ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(value);
    public BistroBuilderEditId(string value) { this.value = value ?? string.Empty; }
    public static BistroBuilderEditId NewId() => new BistroBuilderEditId(Guid.NewGuid().ToString("N"));
    public bool Equals(BistroBuilderEditId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is BistroBuilderEditId other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
    public int CompareTo(BistroBuilderEditId other) => string.CompareOrdinal(Value, other.Value);
    public override string ToString() => Value;
    public static bool operator ==(BistroBuilderEditId a, BistroBuilderEditId b) => a.Equals(b);
    public static bool operator !=(BistroBuilderEditId a, BistroBuilderEditId b) => !a.Equals(b);
}

[Serializable]
public sealed class BistroBuilderArchitectureGeometryPolicy
{
    [Min(0.0001f)] public float pointTolerance = 0.005f;
    [Min(0.001f)] public float minimumWallLength = 0.05f;
    [Min(0.0001f)] public float minimumRoomArea = 0.01f;
    [Min(0.0001f)] public float openingEdgeMargin = 0.01f;
    public static BistroBuilderArchitectureGeometryPolicy Default => new BistroBuilderArchitectureGeometryPolicy();
    public bool SamePoint(Vector2 a, Vector2 b) => (a - b).sqrMagnitude <= pointTolerance * pointTolerance;
    public bool IsWallLengthValid(Vector2 a, Vector2 b) => Vector2.Distance(a, b) >= minimumWallLength;
}

[Serializable]
public sealed class BistroBuilderWallRecord
{
    public BistroBuilderEditId wallId;
    public string buildPlaneId = "default";
    public Vector2 axisStart;
    public Vector2 axisEnd;
    public float baseElevation;
    public float height = 2.8f;
    public float thickness = 0.12f;
    public string wallDefinitionId = "wall.default";
    public string sideAFinishId = string.Empty;
    public string sideBFinishId = string.Empty;

    public float Length => Vector2.Distance(axisStart, axisEnd);
    public BistroBuilderWallRecord DeepClone() => new BistroBuilderWallRecord
    {
        wallId = wallId, buildPlaneId = buildPlaneId, axisStart = axisStart, axisEnd = axisEnd,
        baseElevation = baseElevation, height = height, thickness = thickness,
        wallDefinitionId = wallDefinitionId, sideAFinishId = sideAFinishId, sideBFinishId = sideBFinishId
    };
}
[Serializable]
public sealed class BistroBuilderOpeningRecord
{
    public BistroBuilderEditId openingId;
    public BistroBuilderEditId hostWallId;
    [Range(0f, 1f)] public float axisPosition01 = 0.5f;
    public float width = 0.9f;
    public float bottomElevation;
    public float height = 2.1f;
    public string openingType = "door";
    public string fillDefinitionId = string.Empty;
    public bool flipped;

    public BistroBuilderOpeningRecord DeepClone() => new BistroBuilderOpeningRecord
    {
        openingId = openingId, hostWallId = hostWallId, axisPosition01 = axisPosition01,
        width = width, bottomElevation = bottomElevation, height = height,
        openingType = openingType, fillDefinitionId = fillDefinitionId, flipped = flipped
    };
}

[Serializable]
public sealed class BistroBuilderRoomRecord
{
    public BistroBuilderEditId roomId;
    public string buildPlaneId = "default";
    public Vector2 identityAnchor;
    public long createdRevision;
    public BistroBuilderRoomRecord DeepClone() => new BistroBuilderRoomRecord
    { roomId = roomId, buildPlaneId = buildPlaneId, identityAnchor = identityAnchor, createdRevision = createdRevision };
}
[Serializable]
public sealed class BistroBuilderSurfaceFinishPatchRecord
{
    public BistroBuilderEditId surfacePatchId;
    public string buildPlaneId = "default";
    public string surfaceRole = "floor";
    public string finishDefinitionId = string.Empty;
    public List<Vector2> fallbackBoundary = new List<Vector2>();
    public BistroBuilderSurfaceFinishPatchRecord DeepClone()
    {
        var copy = new BistroBuilderSurfaceFinishPatchRecord
        {
            surfacePatchId = surfacePatchId, buildPlaneId = buildPlaneId,
            surfaceRole = surfaceRole, finishDefinitionId = finishDefinitionId
        };
        copy.fallbackBoundary.AddRange(fallbackBoundary);
        return copy;
    }
}

[Serializable]
public sealed class BistroBuilderFunctionalZoneRecord
{
    public BistroBuilderEditId zoneId;
    public string zoneDefinitionId = string.Empty;
    public List<BistroBuilderEditId> roomIds = new List<BistroBuilderEditId>();
    public List<Vector2> explicitRegion = new List<Vector2>();
    public BistroBuilderFunctionalZoneRecord DeepClone()
    {
        var copy = new BistroBuilderFunctionalZoneRecord { zoneId = zoneId, zoneDefinitionId = zoneDefinitionId };
        copy.roomIds.AddRange(roomIds); copy.explicitRegion.AddRange(explicitRegion); return copy;
    }
}
[Flags]
public enum BistroBuilderEditCapability
{
    None = 0, Place = 1 << 0, Move = 1 << 1, Rotate = 1 << 2, Remove = 1 << 3,
    Duplicate = 1 << 4, Snap = 1 << 5, ChangeVariant = 1 << 6, ChangeFinish = 1 << 7,
    Resize = 1 << 8, EditEndpoints = 1 << 9, MoveAlongHost = 1 << 10, Flip = 1 << 11
}

[Serializable]
public sealed class BistroBuilderEditCatalogDefinition
{
    public string definitionId = string.Empty;
    public string semanticKind = string.Empty;
    public string categoryId = string.Empty;
    public string subcategoryId = string.Empty;
    public string familyId = string.Empty;
    public string variantGroupId = string.Empty;
    public List<string> tags = new List<string>();
    public BistroBuilderEditCapability capabilities;
    public string runtimeAssetKey = string.Empty;
}

[Serializable]
public sealed class BistroBuilderEditDocument
{
    public string documentId = Guid.NewGuid().ToString("N");
    public int schemaVersion = 1;
    public long revision;
    public List<BistroBuilderWallRecord> walls = new List<BistroBuilderWallRecord>();
    public List<BistroBuilderOpeningRecord> openings = new List<BistroBuilderOpeningRecord>();
    public List<BistroBuilderRoomRecord> rooms = new List<BistroBuilderRoomRecord>();
    public List<BistroBuilderSurfaceFinishPatchRecord> surfaces = new List<BistroBuilderSurfaceFinishPatchRecord>();
    public List<BistroBuilderFunctionalZoneRecord> zones = new List<BistroBuilderFunctionalZoneRecord>();
    public BistroBuilderEditDocument DeepClone()
    {
        var copy = new BistroBuilderEditDocument { documentId = documentId, schemaVersion = schemaVersion, revision = revision };
        for (int i = 0; i < walls.Count; i++) copy.walls.Add(walls[i].DeepClone());
        for (int i = 0; i < openings.Count; i++) copy.openings.Add(openings[i].DeepClone());
        for (int i = 0; i < rooms.Count; i++) copy.rooms.Add(rooms[i].DeepClone());
        for (int i = 0; i < surfaces.Count; i++) copy.surfaces.Add(surfaces[i].DeepClone());
        for (int i = 0; i < zones.Count; i++) copy.zones.Add(zones[i].DeepClone());
        return copy;
    }

    public BistroBuilderWallRecord FindWall(BistroBuilderEditId id)
    {
        for (int i = 0; i < walls.Count; i++) if (walls[i] != null && walls[i].wallId == id) return walls[i];
        return null;
    }

    public BistroBuilderOpeningRecord FindOpening(BistroBuilderEditId id)
    {
        for (int i = 0; i < openings.Count; i++) if (openings[i] != null && openings[i].openingId == id) return openings[i];
        return null;
    }

    public BistroBuilderRoomRecord FindRoom(BistroBuilderEditId id)
    {
        for (int i = 0; i < rooms.Count; i++) if (rooms[i] != null && rooms[i].roomId == id) return rooms[i];
        return null;
    }
    public string ComputeFingerprint()
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            void Mix(string text)
            {
                string s = text ?? string.Empty;
                for (int i = 0; i < s.Length; i++) { hash ^= s[i]; hash *= 1099511628211UL; }
                hash ^= 255; hash *= 1099511628211UL;
            }
            string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
            Mix(documentId); Mix(schemaVersion.ToString(CultureInfo.InvariantCulture)); Mix(revision.ToString(CultureInfo.InvariantCulture));

            var sortedWalls = new List<BistroBuilderWallRecord>(walls); sortedWalls.Sort((a,b) => a.wallId.CompareTo(b.wallId));
            for (int i = 0; i < sortedWalls.Count; i++)
            {
                var w = sortedWalls[i]; Mix(w.wallId.Value); Mix(w.buildPlaneId); Mix(F(w.axisStart.x)); Mix(F(w.axisStart.y));
                Mix(F(w.axisEnd.x)); Mix(F(w.axisEnd.y)); Mix(F(w.baseElevation)); Mix(F(w.height)); Mix(F(w.thickness));
                Mix(w.wallDefinitionId); Mix(w.sideAFinishId); Mix(w.sideBFinishId);
            }

            var sortedOpenings = new List<BistroBuilderOpeningRecord>(openings); sortedOpenings.Sort((a,b) => a.openingId.CompareTo(b.openingId));
            for (int i = 0; i < sortedOpenings.Count; i++)
            {
                var o = sortedOpenings[i]; Mix(o.openingId.Value); Mix(o.hostWallId.Value); Mix(F(o.axisPosition01)); Mix(F(o.width));
                Mix(F(o.bottomElevation)); Mix(F(o.height)); Mix(o.openingType); Mix(o.fillDefinitionId); Mix(o.flipped ? "1" : "0");
            }

            var sortedRooms = new List<BistroBuilderRoomRecord>(rooms); sortedRooms.Sort((a,b) => a.roomId.CompareTo(b.roomId));
            for (int i = 0; i < sortedRooms.Count; i++)
            {
                var r = sortedRooms[i]; Mix(r.roomId.Value); Mix(r.buildPlaneId); Mix(F(r.identityAnchor.x)); Mix(F(r.identityAnchor.y));
                Mix(r.createdRevision.ToString(CultureInfo.InvariantCulture));
            }

            var sortedSurfaces = new List<BistroBuilderSurfaceFinishPatchRecord>(surfaces); sortedSurfaces.Sort((a,b) => a.surfacePatchId.CompareTo(b.surfacePatchId));
            for (int i = 0; i < sortedSurfaces.Count; i++)
            {
                var s = sortedSurfaces[i]; Mix(s.surfacePatchId.Value); Mix(s.buildPlaneId); Mix(s.surfaceRole); Mix(s.finishDefinitionId);
                for (int p = 0; p < s.fallbackBoundary.Count; p++) { Mix(F(s.fallbackBoundary[p].x)); Mix(F(s.fallbackBoundary[p].y)); }
            }

            var sortedZones = new List<BistroBuilderFunctionalZoneRecord>(zones); sortedZones.Sort((a,b) => a.zoneId.CompareTo(b.zoneId));
            for (int i = 0; i < sortedZones.Count; i++)
            {
                var z = sortedZones[i]; Mix(z.zoneId.Value); Mix(z.zoneDefinitionId);
                var roomIds = new List<BistroBuilderEditId>(z.roomIds); roomIds.Sort();
                for (int r = 0; r < roomIds.Count; r++) Mix(roomIds[r].Value);
                for (int p = 0; p < z.explicitRegion.Count; p++) { Mix(F(z.explicitRegion[p].x)); Mix(F(z.explicitRegion[p].y)); }
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }}
