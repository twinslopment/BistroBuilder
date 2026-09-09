using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Muestra certificada de un Route Corridor. Las holguras son observaciones
/// de Navigation sobre espacio ya validado por NavMesh/BBSIS; no crean validez.
/// </summary>
[Serializable]
public sealed class BistroBuilderNavigationCorridorSample
{
    public Vector3 center;
    public Vector3 tangent = Vector3.forward;
    public float cumulativeMeters;
    [Min(0f)] public float leftClearance;
    [Min(0f)] public float rightClearance;

    public BistroBuilderNavigationCorridorSample DeepClone()
    {
        return (BistroBuilderNavigationCorridorSample)MemberwiseClone();
    }
}

/// <summary>
/// Corredor continuo asociado a un plan estructural. Permite steering y recovery
/// locales sin convertir cada desviación válida en un nuevo pathfinding.
/// </summary>
[Serializable]
public sealed class BistroBuilderNavigationRouteCorridor
{
    public List<BistroBuilderNavigationCorridorSample> samples =
        new List<BistroBuilderNavigationCorridorSample>();
    [Min(0f)] public float lengthMeters;
    [Min(0.05f)] public float mobilityRadius = 0.28f;
    public int spatialRevision;
    public int navigationRevision;

    public bool IsUsable => samples != null && samples.Count >= 2;

    public BistroBuilderNavigationRouteCorridor DeepClone()
    {
        var clone = new BistroBuilderNavigationRouteCorridor
        {
            lengthMeters = lengthMeters,
            mobilityRadius = mobilityRadius,
            spatialRevision = spatialRevision,
            navigationRevision = navigationRevision
        };
        if (samples != null)
            for (int i = 0; i < samples.Count; i++)
                if (samples[i] != null)
                    clone.samples.Add(samples[i].DeepClone());
        return clone;
    }

    public bool Matches(BistroBuilderNavigationTopologySnapshot topology)
    {
        return spatialRevision == topology.spatialRevision &&
               navigationRevision == topology.navigationRevision;
    }

    public float AverageClearance
    {
        get
        {
            if (!IsUsable) return 0f;
            float total = 0f;
            int count = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                BistroBuilderNavigationCorridorSample sample = samples[i];
                if (sample == null) continue;
                total += sample.leftClearance + sample.rightClearance;
                count += 2;
            }
            return count > 0 ? total / count : 0f;
        }
    }
}

/// <summary>
/// Proyección temporal de una posición sobre el corredor.
/// </summary>
public struct BistroBuilderNavigationCorridorProjection
{
    public Vector3 center;
    public Vector3 tangent;
    public Vector3 right;
    public float signedLateral;
    public float leftClearance;
    public float rightClearance;
    public float progressMeters;
}
