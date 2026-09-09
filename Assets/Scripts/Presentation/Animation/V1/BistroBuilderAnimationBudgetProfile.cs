using UnityEngine;

[CreateAssetMenu(fileName = "BBAnimationBudgetProfile", menuName = "Bistro Builder/Animation V1/Budget Profile")]
public sealed class BistroBuilderAnimationBudgetProfile : ScriptableObject
{
    [Range(0f, 1f)] public float q0MaxPressure = 0.20f;
    [Range(0f, 1f)] public float q1MaxPressure = 0.40f;
    [Range(0f, 1f)] public float q2MaxPressure = 0.65f;
    [Range(0f, 1f)] public float q3MaxPressure = 0.85f;
    public BistroBuilderAnimationQualityTier protectedMinimumQuality = BistroBuilderAnimationQualityTier.Q2;
    [Min(1)] public int maximumQ0Actors = 16;
    [Min(1)] public int maximumQ1Actors = 40;

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (q0MaxPressure < 0f || q0MaxPressure > q1MaxPressure ||
            q1MaxPressure > q2MaxPressure || q2MaxPressure > q3MaxPressure || q3MaxPressure > 1f)
        {
            error = "Animation quality pressure thresholds must be ordered Q0 <= Q1 <= Q2 <= Q3 <= 1.";
            return false;
        }
        if (maximumQ0Actors < 1 || maximumQ1Actors < maximumQ0Actors)
        {
            error = "Animation actor caps must satisfy 1 <= maximumQ0Actors <= maximumQ1Actors.";
            return false;
        }
        return true;
    }
}
