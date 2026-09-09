using UnityEngine;

/// <summary>
/// Política blanda de adelantamiento y cohesión de grupos.
/// Nunca invalida BBSIS ni fuerza una formación en corredores estrechos.
/// </summary>
public sealed class BistroBuilderNavigationSocialMotionPolicy
{
    private const float MinimumSpeedAdvantage = 0.35f;
    private const float MinimumSideClearance = 0.42f;
    private const float GroupReleaseClearance = 0.5f;

    public bool CanOvertake(BistroBuilderNavigationRouteCorridor corridor,
        Vector3 position, float ownSpeed, float leadSpeed,
        bool conflictAhead, out int preferredSide)
    {
        preferredSide = 0;
        if (corridor == null || !corridor.IsUsable || conflictAhead ||
            ownSpeed < leadSpeed + MinimumSpeedAdvantage) return false;
        var builder = new BistroBuilderNavigationRouteCorridorBuilder();
        if (!builder.TryProject(corridor, position, out var projection)) return false;
        float right = Mathf.Max(0f, projection.rightClearance);
        float left = Mathf.Max(0f, projection.leftClearance);
        float needed = Mathf.Max(MinimumSideClearance, corridor.mobilityRadius * 1.35f);
        if (right < needed && left < needed) return false;
        preferredSide = right >= left ? 1 : -1;
        return true;
    }

    public Vector3 ComputeLooseGroupBias(BistroBuilderNavigationRouteCorridor corridor,
        Vector3 selfPosition, Vector3 groupCentroid, Vector3 desiredVelocity,
        float strength = 1f)
    {
        if (corridor == null || !corridor.IsUsable) return Vector3.zero;
        var builder = new BistroBuilderNavigationRouteCorridorBuilder();
        if (!builder.TryProject(corridor, selfPosition, out var projection)) return Vector3.zero;
        if (projection.leftClearance + projection.rightClearance < GroupReleaseClearance)
            return Vector3.zero;
        Vector3 delta = groupCentroid - selfPosition;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance <= 0.65f || distance <= 0.0001f) return Vector3.zero;

        Vector3 desired = desiredVelocity;
        desired.y = 0f;
        Vector3 forward = desired.sqrMagnitude > 0.0001f ? desired.normalized : projection.tangent;
        Vector3 lateral = delta - forward * Vector3.Dot(delta, forward);
        Vector3 longitudinal = forward * Mathf.Max(0f, Vector3.Dot(delta, forward) - 0.8f);
        Vector3 bias = lateral * 0.42f + longitudinal * 0.18f;
        float maxBias = Mathf.Lerp(0.12f, 0.48f, Mathf.Clamp01(strength));
        return Vector3.ClampMagnitude(bias, maxBias);
    }
}
