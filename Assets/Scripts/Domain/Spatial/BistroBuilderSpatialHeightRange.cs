using System;
using UnityEngine;

/// <summary>
/// Optional world-space vertical interval. Missing or invalid data is unbounded:
/// it can never prove separation and therefore never releases legacy obstacles.
/// </summary>
[Serializable]
public struct BistroBuilderSpatialHeightRange
{
    public bool bounded;
    public float minimum;
    public float maximum;

    public bool IsBounded => bounded && Finite(minimum) && Finite(maximum) && maximum > minimum;

    public static BistroBuilderSpatialHeightRange Between(float minimum, float maximum) =>
        new BistroBuilderSpatialHeightRange { bounded = true, minimum = minimum, maximum = maximum };

    public bool Overlaps(BistroBuilderSpatialHeightRange other, float clearance = 0f)
    {
        if (!IsBounded || !other.IsBounded) return true;
        float gap = Mathf.Max(0f, clearance);
        return maximum + gap > other.minimum && other.maximum + gap > minimum;
    }

    public bool Contains(float height, float expansion = 0f) => !IsBounded ||
        (height >= minimum - Mathf.Max(0f, expansion) && height <= maximum + Mathf.Max(0f, expansion));

    public BistroBuilderSpatialHeightRange Shifted(float delta) => IsBounded && Finite(delta)
        ? Between(minimum + delta, maximum + delta) : default;

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
