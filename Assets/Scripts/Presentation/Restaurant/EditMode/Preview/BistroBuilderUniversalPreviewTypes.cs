using System.Collections.Generic;
using UnityEngine;

public enum BistroBuilderPreviewDomain
{
    None = 0,
    Furniture = 1,
    Wall = 2,
    Room = 3,
    Opening = 4,
    Surface = 5,
    Module = 6,
    Zone = 7
}

public enum BistroBuilderPreviewValidity
{
    Neutral = 0,
    Valid = 1,
    Invalid = 2
}

public enum BistroBuilderPreviewPhase
{
    Hidden = 0,
    Previewing = 1,
    Snapped = 2,
    Ready = 3,
    Confirmed = 4,
    Cancelled = 5
}

/// <summary>
/// Snapshot reutilizable de la preview universal.
/// Las listas son de solo lectura para consumidores; el servicio conserva la propiedad.
/// </summary>
public sealed class BistroBuilderUniversalPreviewState
{
    private readonly List<Vector3> candidateSegments = new List<Vector3>(32);
    private readonly List<Vector3> ghostSegments = new List<Vector3>(32);
    private readonly List<Vector3> conflictSegments = new List<Vector3>(16);

    public int Revision { get; internal set; }
    public string OwnerId { get; internal set; } = string.Empty;
    public BistroBuilderPreviewDomain Domain { get; internal set; }
    public BistroBuilderPreviewValidity Validity { get; internal set; }
    public BistroBuilderPreviewPhase Phase { get; internal set; }
    public string Message { get; internal set; } = string.Empty;
    public bool HasCandidatePose { get; internal set; }
    public Vector3 CandidatePosition { get; internal set; }
    public Quaternion CandidateRotation { get; internal set; } = Quaternion.identity;
    public bool HasSnapPoint { get; internal set; }
    public Vector3 SnapPoint { get; internal set; }
    public Object RelatedObject { get; internal set; }
    public Object ConflictObject { get; internal set; }

    public IReadOnlyList<Vector3> CandidateSegments => candidateSegments;
    public IReadOnlyList<Vector3> GhostSegments => ghostSegments;
    public IReadOnlyList<Vector3> ConflictSegments => conflictSegments;

    public bool IsVisible =>
        Phase != BistroBuilderPreviewPhase.Hidden &&
        (!string.IsNullOrEmpty(OwnerId) ||
         candidateSegments.Count > 0 ||
         HasCandidatePose);

    internal List<Vector3> MutableCandidateSegments => candidateSegments;
    internal List<Vector3> MutableGhostSegments => ghostSegments;
    internal List<Vector3> MutableConflictSegments => conflictSegments;

    internal void Reset()
    {
        OwnerId = string.Empty;
        Domain = BistroBuilderPreviewDomain.None;
        Validity = BistroBuilderPreviewValidity.Neutral;
        Phase = BistroBuilderPreviewPhase.Hidden;
        Message = string.Empty;
        HasCandidatePose = false;
        CandidatePosition = Vector3.zero;
        CandidateRotation = Quaternion.identity;
        HasSnapPoint = false;
        SnapPoint = Vector3.zero;
        RelatedObject = null;
        ConflictObject = null;
        candidateSegments.Clear();
        ghostSegments.Clear();
        conflictSegments.Clear();
    }
}
