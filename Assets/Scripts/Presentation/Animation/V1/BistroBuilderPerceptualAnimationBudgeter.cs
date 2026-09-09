using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BistroBuilderPerceptualAnimationBudgeter : MonoBehaviour
{
    [SerializeField] private BistroBuilderAnimationBudgetProfile profile;
    [SerializeField, Range(0f, 1f)] private float cpuPressure;
    [SerializeField, Range(0f, 1f)] private float crowdPressure;

    private readonly Dictionary<string, float> protectedUntil = new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderAnimationQualityTier> assignedTierByHandle = new Dictionary<string, BistroBuilderAnimationQualityTier>(StringComparer.Ordinal);

    public int ActiveAssignmentCount => assignedTierByHandle.Count;

#if UNITY_EDITOR
    public void ConfigureForEditor(BistroBuilderAnimationBudgetProfile configuredProfile)
    {
        profile = configuredProfile;
    }
#endif

    public void SetRuntimePressure(float cpu, float crowd)
    {
        cpuPressure = Mathf.Clamp01(cpu);
        crowdPressure = Mathf.Clamp01(crowd);
    }

    public void Protect(BistroBuilderAnimationExecutionHandle handle, float seconds)
    {
        if (!handle.IsValid) return;
        protectedUntil[handle.ToString()] = Time.unscaledTime + Mathf.Max(0.05f, seconds);
    }

    public void ReleaseProtection(BistroBuilderAnimationExecutionHandle handle)
    {
        if (handle.IsValid) protectedUntil.Remove(handle.ToString());
    }

    public void Assign(BistroBuilderAnimationExecutionHandle handle, BistroBuilderAnimationQualityTier tier)
    {
        if (!handle.IsValid) return;
        assignedTierByHandle[handle.ToString()] = tier;
    }

    public void Release(BistroBuilderAnimationExecutionHandle handle)
    {
        if (!handle.IsValid) return;
        string key = handle.ToString();
        protectedUntil.Remove(key);
        assignedTierByHandle.Remove(key);
    }

    public BistroBuilderAnimationQualityTier Evaluate(
        BistroBuilderAnimationExecutionHandle handle,
        float perceptualImportance01,
        bool explicitProtection)
    {
        float pressure = Mathf.Max(cpuPressure, crowdPressure);
        pressure = Mathf.Clamp01(pressure - Mathf.Clamp01(perceptualImportance01) * 0.25f);
        BistroBuilderAnimationBudgetProfile p = profile;

        float q0 = p != null ? p.q0MaxPressure : 0.20f;
        float q1 = p != null ? p.q1MaxPressure : 0.40f;
        float q2 = p != null ? p.q2MaxPressure : 0.65f;
        float q3 = p != null ? p.q3MaxPressure : 0.85f;

        BistroBuilderAnimationQualityTier tier;
        if (pressure <= q0) tier = BistroBuilderAnimationQualityTier.Q0;
        else if (pressure <= q1) tier = BistroBuilderAnimationQualityTier.Q1;
        else if (pressure <= q2) tier = BistroBuilderAnimationQualityTier.Q2;
        else if (pressure <= q3) tier = BistroBuilderAnimationQualityTier.Q3;
        else tier = BistroBuilderAnimationQualityTier.Q4;

        bool protectedNow = explicitProtection || IsProtected(handle);
        BistroBuilderAnimationQualityTier protectedTier = p != null
            ? p.protectedMinimumQuality
            : BistroBuilderAnimationQualityTier.Q2;
        if (protectedNow && (int)tier > (int)protectedTier)
            tier = protectedTier;

        if (!protectedNow)
            tier = ApplyCapacityCaps(tier, p);

        return tier;
    }

    private BistroBuilderAnimationQualityTier ApplyCapacityCaps(
        BistroBuilderAnimationQualityTier candidate,
        BistroBuilderAnimationBudgetProfile p)
    {
        int maxQ0 = p != null ? Mathf.Max(1, p.maximumQ0Actors) : 16;
        int maxQ1 = p != null ? Mathf.Max(maxQ0, p.maximumQ1Actors) : 40;
        int q0Count = 0;
        int q0OrQ1Count = 0;

        foreach (BistroBuilderAnimationQualityTier tier in assignedTierByHandle.Values)
        {
            if (tier == BistroBuilderAnimationQualityTier.Q0) q0Count++;
            if ((int)tier <= (int)BistroBuilderAnimationQualityTier.Q1) q0OrQ1Count++;
        }

        if (candidate == BistroBuilderAnimationQualityTier.Q0 && q0Count >= maxQ0)
            candidate = BistroBuilderAnimationQualityTier.Q1;

        if ((int)candidate <= (int)BistroBuilderAnimationQualityTier.Q1 && q0OrQ1Count >= maxQ1)
            candidate = BistroBuilderAnimationQualityTier.Q2;

        return candidate;
    }

    private bool IsProtected(BistroBuilderAnimationExecutionHandle handle)
    {
        if (!handle.IsValid) return false;
        string key = handle.ToString();
        if (!protectedUntil.TryGetValue(key, out float until)) return false;
        if (Time.unscaledTime <= until) return true;
        protectedUntil.Remove(key);
        return false;
    }
}
