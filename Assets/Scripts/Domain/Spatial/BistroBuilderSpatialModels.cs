using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modos de representación espacial de BBSIS.
/// La geometría espacial nunca depende de la geometría de render.
/// </summary>
public enum BistroBuilderAdaptiveSpatialProxyMode
{
    Simple = 0,
    Compound = 1,
    Layered = 2,
    Articulated = 3
}

public enum BistroBuilderSpatialProxyLayer
{
    Static = 0,
    Operational = 1,
    Dynamic = 2
}

public enum BistroBuilderSpatialShapeKind
{
    OrientedBox = 0,
    Circle = 1,
    Capsule = 2,
    ConvexHull = 3
}

public enum BistroBuilderSpatialPortKind
{
    Interaction = 0,
    Service = 1,
    Work = 2,
    SeatBay = 3,
    Transfer = 4
}

public enum BistroBuilderSpatialClaimKind
{
    Destination = 0,
    Interaction = 1,
    Service = 2,
    Work = 3,
    Seat = 4,
    Transfer = 5,
    DynamicSweep = 6,
    Mobility = 7,
    Carry = 8,
    TraversalGate = 9
}

public enum BistroBuilderSpatialConflictMode
{
    Block = 0,
    Reservable = 1,
    Degrade = 2,
    Compatible = 3
}

public enum BistroBuilderSpatialSemanticRole
{
    StaticBody = 0,
    OperationalClearance = 1,
    SeatBay = 2,
    Approach = 3,
    WorkZone = 4,
    TransferZone = 5,
    DynamicSweep = 6,
    TraversalGate = 7,
    MobilityEnvelope = 8,
    CarryEnvelope = 9
}

public enum BistroBuilderSpatialEpisodeState
{
    Active = 0,
    Completed = 1,
    Cancelled = 2,
    Failed = 3
}

public enum BistroBuilderSpatialLeaseFailure
{
    None = 0,
    InvalidRequest = 1,
    Conflict = 2,
    SubjectUnavailable = 3,
    ContractMismatch = 4,
    EpisodeUnavailable = 5,
    StaticGeometryConflict = 6
}

/// <summary>
/// Volumen 2D determinista utilizado por Claims y Spatial Leases.
/// Círculos y OBB cubren las reservas de runtime sin imponer colliders físicos.
/// </summary>
[Serializable]
public struct BistroBuilderSpatialVolume
{
    public BistroBuilderSpatialShapeKind shapeKind;
    public Vector3 center;
    public Vector3 rightAxis;
    public Vector3 forwardAxis;
    public Vector2 halfExtents;
    public float radius;

    public static BistroBuilderSpatialVolume Circle(Vector3 center, float radius)
    {
        return new BistroBuilderSpatialVolume
        {
            shapeKind = BistroBuilderSpatialShapeKind.Circle,
            center = center,
            rightAxis = Vector3.right,
            forwardAxis = Vector3.forward,
            halfExtents = Vector2.zero,
            radius = Mathf.Max(0.01f, radius)
        };
    }

    public static BistroBuilderSpatialVolume Box(
        Vector3 center,
        Vector3 rightAxis,
        Vector3 forwardAxis,
        Vector2 halfExtents)
    {
        return new BistroBuilderSpatialVolume
        {
            shapeKind = BistroBuilderSpatialShapeKind.OrientedBox,
            center = center,
            rightAxis = NormalizeHorizontal(rightAxis, Vector3.right),
            forwardAxis = NormalizeHorizontal(forwardAxis, Vector3.forward),
            halfExtents = new Vector2(
                Mathf.Max(0.01f, halfExtents.x),
                Mathf.Max(0.01f, halfExtents.y)),
            radius = 0f
        };
    }

    public bool ContainsPoint(Vector3 point, float expansion = 0f)
    {
        float extra = Mathf.Max(0f, expansion);
        if (shapeKind == BistroBuilderSpatialShapeKind.Circle)
        {
            Vector3 delta = point - center;
            delta.y = 0f;
            float limit = Mathf.Max(0.01f, radius) + extra;
            return delta.sqrMagnitude <= limit * limit;
        }

        Vector3 planar = point - center;
        planar.y = 0f;
        float x = Mathf.Abs(Vector3.Dot(planar, NormalizeHorizontal(rightAxis, Vector3.right)));
        float z = Mathf.Abs(Vector3.Dot(planar, NormalizeHorizontal(forwardAxis, Vector3.forward)));
        return x <= Mathf.Max(0.01f, halfExtents.x) + extra &&
               z <= Mathf.Max(0.01f, halfExtents.y) + extra;
    }

    public bool Overlaps(BistroBuilderSpatialVolume other)
    {
        if (shapeKind == BistroBuilderSpatialShapeKind.Circle &&
            other.shapeKind == BistroBuilderSpatialShapeKind.Circle)
        {
            Vector3 delta = other.center - center;
            delta.y = 0f;
            float limit = Mathf.Max(0.01f, radius) + Mathf.Max(0.01f, other.radius);
            return delta.sqrMagnitude < limit * limit;
        }
        return OverlapsAsObb(this, other);
    }

    private static bool OverlapsAsObb(
        BistroBuilderSpatialVolume first,
        BistroBuilderSpatialVolume second)
    {
        if (first.shapeKind == BistroBuilderSpatialShapeKind.Circle)
            return CircleBox(first, second);
        if (second.shapeKind == BistroBuilderSpatialShapeKind.Circle)
            return CircleBox(second, first);

        Vector3[] axes =
        {
            NormalizeHorizontal(first.rightAxis, Vector3.right),
            NormalizeHorizontal(first.forwardAxis, Vector3.forward),
            NormalizeHorizontal(second.rightAxis, Vector3.right),
            NormalizeHorizontal(second.forwardAxis, Vector3.forward)
        };
        Vector3 delta = second.center - first.center;
        delta.y = 0f;
        for (int i = 0; i < axes.Length; i++)
        {
            Vector3 axis = axes[i];
            float distance = Mathf.Abs(Vector3.Dot(delta, axis));
            float firstRadius = ProjectionRadius(first, axis);
            float secondRadius = ProjectionRadius(second, axis);
            if (distance >= firstRadius + secondRadius)
                return false;
        }
        return true;
    }

    private static bool CircleBox(
        BistroBuilderSpatialVolume circle,
        BistroBuilderSpatialVolume box)
    {
        Vector3 right = NormalizeHorizontal(box.rightAxis, Vector3.right);
        Vector3 forward = NormalizeHorizontal(box.forwardAxis, Vector3.forward);
        Vector3 delta = circle.center - box.center;
        delta.y = 0f;
        float localX = Vector3.Dot(delta, right);
        float localZ = Vector3.Dot(delta, forward);
        float closestX = Mathf.Clamp(localX, -box.halfExtents.x, box.halfExtents.x);
        float closestZ = Mathf.Clamp(localZ, -box.halfExtents.y, box.halfExtents.y);
        float dx = localX - closestX;
        float dz = localZ - closestZ;
        float radius = Mathf.Max(0.01f, circle.radius);
        return dx * dx + dz * dz < radius * radius;
    }

    private static float ProjectionRadius(
        BistroBuilderSpatialVolume volume,
        Vector3 axis)
    {
        Vector3 right = NormalizeHorizontal(volume.rightAxis, Vector3.right);
        Vector3 forward = NormalizeHorizontal(volume.forwardAxis, Vector3.forward);
        return Mathf.Abs(Vector3.Dot(right, axis)) * Mathf.Max(0.01f, volume.halfExtents.x) +
               Mathf.Abs(Vector3.Dot(forward, axis)) * Mathf.Max(0.01f, volume.halfExtents.y);
    }

    private static Vector3 NormalizeHorizontal(Vector3 axis, Vector3 fallback)
    {
        axis.y = 0f;
        return axis.sqrMagnitude <= 0.000001f ? fallback : axis.normalized;
    }
}

[Serializable]
public sealed class BistroBuilderSpatialClaimRequest
{
    public string ownerId = string.Empty;
    public string subjectId = string.Empty;
    public string portId = string.Empty;
    public string episodeId = string.Empty;
    public BistroBuilderSpatialClaimKind kind = BistroBuilderSpatialClaimKind.Interaction;
    public BistroBuilderSpatialConflictMode conflictMode = BistroBuilderSpatialConflictMode.Reservable;
    public BistroBuilderSpatialVolume volume;
    public int priority;
    public float durationSeconds = 1f;
    public bool validateAgainstStaticGeometry;
    public string relatedSubjectId = string.Empty;
}

[Serializable]
public sealed class BistroBuilderSpatialLease
{
    public string leaseId = string.Empty;
    public string ownerId = string.Empty;
    public string subjectId = string.Empty;
    public string portId = string.Empty;
    public string episodeId = string.Empty;
    public BistroBuilderSpatialClaimKind kind;
    public BistroBuilderSpatialConflictMode conflictMode;
    public BistroBuilderSpatialVolume volume;
    public int priority;
    public long sequence;
    public float expiresAtUnscaled;

    public bool IsTransient => expiresAtUnscaled > 0f;
}

[Serializable]
public sealed class BistroBuilderSpatialEpisode
{
    public string episodeId = string.Empty;
    public string ownerId = string.Empty;
    public string purposeId = string.Empty;
    public string subjectId = string.Empty;
    public BistroBuilderSpatialEpisodeState state = BistroBuilderSpatialEpisodeState.Active;
    public long sequence;
}

[Serializable]
public sealed class BistroBuilderSpatialLeaseDecision
{
    public bool granted;
    public BistroBuilderSpatialLeaseFailure failure;
    public string blockingLeaseId = string.Empty;
    public string blockingSubjectId = string.Empty;
    public string message = string.Empty;
}

/// <summary>
/// Persistencia BBSIS: Claims, Leases, esperas y caches son transitorios.
/// Tras Load se reconstruyen desde el estado funcional, nunca se serializan vivos.
/// </summary>
[Serializable]
public sealed class BistroBuilderSpatialRuntimeSnapshot
{
    public const string CurrentSchemaId = "spatial.runtime";
    public const int CurrentSchemaVersion = 1;
    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public int topologyRevision;
}

[Serializable]
public sealed class BistroBuilderSpatialDiagnostic
{
    public string diagnosticId = string.Empty;
    public string subjectId = string.Empty;
    public string evidence = string.Empty;
    public bool blocking;
}

[Serializable]
public sealed class BistroBuilderSpatialQualityResult
{
    public bool viable = true;
    [Range(0f, 1f)] public float quality = 1f;
    public List<BistroBuilderSpatialDiagnostic> diagnostics =
        new List<BistroBuilderSpatialDiagnostic>();
}
