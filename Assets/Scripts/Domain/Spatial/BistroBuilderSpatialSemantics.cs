using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Volumen semántico derivado de un Spatial Subject.
/// No sustituye al proxy: añade significado operacional a su geometría.
/// </summary>
[Serializable]
public sealed class BistroBuilderSpatialSemanticVolume
{
    public string subjectId = string.Empty;
    public string semanticId = string.Empty;
    public string relatedSubjectId = string.Empty;
    public BistroBuilderSpatialSemanticRole role =
        BistroBuilderSpatialSemanticRole.OperationalClearance;
    public BistroBuilderSpatialProxyLayer layer =
        BistroBuilderSpatialProxyLayer.Operational;
    public BistroBuilderSpatialConflictMode conflictMode =
        BistroBuilderSpatialConflictMode.Reservable;
    public BistroBuilderSpatialVolume volume;
    public bool critical;
}

/// <summary>
/// Contrato BBSIS para adaptadores que exponen semántica espacial runtime.
/// </summary>
public interface IBistroBuilderSpatialSemanticProvider
{
    string SpatialSubjectId { get; }
    int WriteSemanticVolumes(List<BistroBuilderSpatialSemanticVolume> results);
}

[Serializable]
public sealed class BistroBuilderSpatialBottleneckRecord
{
    public string bottleneckId = string.Empty;
    public string subjectId = string.Empty;
    public string otherSubjectId = string.Empty;
    public BistroBuilderSpatialSemanticRole role;
    [Range(0f, 1f)] public float severity;
    public bool blocking;
    public string evidence = string.Empty;
}

[Serializable]
public sealed class BistroBuilderSpatialBottleneckLedger
{
    public int topologyRevision;
    [Range(0f, 1f)] public float flowQuality = 1f;
    public List<BistroBuilderSpatialBottleneckRecord> records =
        new List<BistroBuilderSpatialBottleneckRecord>();
}
