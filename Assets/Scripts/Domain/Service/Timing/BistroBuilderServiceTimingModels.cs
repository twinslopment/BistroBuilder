using System;
using UnityEngine;

/// <summary>
/// Fases de servicio que disponen de umbrales temporales canónicos.
/// Se amplía de forma incremental cuando una vertical real lo necesita.
/// </summary>
public enum BistroBuilderServiceTimingPhase
{
    BillDelivery = 0,
    TakeOrder = 1,
    FoodDelivery = 2
}

/// <summary>
/// Lectura semántica compartida de una espera de servicio.
/// Resolution se reserva para flujos que estén cerrando una incidencia;
/// el evaluador temporal puro no la produce por sí solo.
/// </summary>
public enum BistroBuilderServiceTimingState
{
    Normal = 0,
    Attention = 1,
    Delay = 2,
    Incident = 3,
    Critical = 4,
    Resolution = 5
}

[Serializable]
public sealed class BistroBuilderServiceTimingProfile
{
    [SerializeField] private BistroBuilderServiceTimingPhase phase;
    [SerializeField, Min(0f)] private float targetSeconds;
    [SerializeField, Min(0f)] private float attentionSeconds;
    [SerializeField, Min(0f)] private float delaySeconds;
    [SerializeField, Min(0f)] private float incidentSeconds;
    [SerializeField, Min(0f)] private float criticalSeconds;
    [SerializeField, Range(0, 10000)]
    private int explanationPenaltyMitigationBasisPoints;
    [SerializeField, Range(0, 10000)]
    private int apologyPenaltyMitigationBasisPoints;

    public BistroBuilderServiceTimingPhase Phase => phase;
    public float TargetSeconds => targetSeconds;
    public float AttentionSeconds => attentionSeconds;
    public float DelaySeconds => delaySeconds;
    public float IncidentSeconds => incidentSeconds;
    public float CriticalSeconds => criticalSeconds;
    public int ExplanationPenaltyMitigationBasisPoints =>
        explanationPenaltyMitigationBasisPoints;
    public int ApologyPenaltyMitigationBasisPoints =>
        apologyPenaltyMitigationBasisPoints;

    public bool Validate(out string error)
    {
        if (!IsFiniteNonNegative(targetSeconds) ||
            !IsFiniteNonNegative(attentionSeconds) ||
            !IsFiniteNonNegative(delaySeconds) ||
            !IsFiniteNonNegative(incidentSeconds) ||
            !IsFiniteNonNegative(criticalSeconds) ||
            explanationPenaltyMitigationBasisPoints < 0 ||
            explanationPenaltyMitigationBasisPoints > 10000 ||
            apologyPenaltyMitigationBasisPoints < 0 ||
            apologyPenaltyMitigationBasisPoints > 10000)
        {
            error = "Los umbrales deben ser finitos/no negativos y las mitigaciones deben estar entre 0 y 10000 pb.";
            return false;
        }

        if (targetSeconds > attentionSeconds ||
            attentionSeconds > delaySeconds ||
            delaySeconds > incidentSeconds ||
            incidentSeconds > criticalSeconds)
        {
            error =
                "Los umbrales deben mantener target <= attention <= delay <= incident <= critical.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public BistroBuilderServiceTimingState Evaluate(float elapsedSeconds)
    {
        return BistroBuilderServiceTimingEvaluator.Evaluate(this, elapsedSeconds);
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value) &&
               value >= 0f;
    }
}

[Serializable]
public sealed class BistroBuilderFoodTimingPolicy
{
    [SerializeField, Min(0.1f)] private float minimumExpectedSeconds = 4f;
    [SerializeField, Min(0f)] private float attentionMultiplier = 1.15f;
    [SerializeField, Min(0f)] private float attentionOffsetSeconds;
    [SerializeField, Min(0f)] private float delayMultiplier = 1.35f;
    [SerializeField, Min(0f)] private float delayOffsetSeconds = 4f;
    [SerializeField, Min(0f)] private float incidentMultiplier = 2f;
    [SerializeField, Min(0f)] private float incidentOffsetSeconds;
    [SerializeField, Min(0f)] private float criticalMultiplier = 3f;
    [SerializeField, Min(0f)] private float criticalOffsetSeconds = 30f;
    [SerializeField, Range(0, 10000)]
    private int explanationPenaltyMitigationBasisPoints = 1500;
    [SerializeField, Range(0, 10000)]
    private int apologyPenaltyMitigationBasisPoints = 2500;

    public float MinimumExpectedSeconds => minimumExpectedSeconds;
    public float AttentionMultiplier => attentionMultiplier;
    public float AttentionOffsetSeconds => attentionOffsetSeconds;
    public float DelayMultiplier => delayMultiplier;
    public float DelayOffsetSeconds => delayOffsetSeconds;
    public float IncidentMultiplier => incidentMultiplier;
    public float IncidentOffsetSeconds => incidentOffsetSeconds;
    public float CriticalMultiplier => criticalMultiplier;
    public float CriticalOffsetSeconds => criticalOffsetSeconds;
    public int ExplanationPenaltyMitigationBasisPoints =>
        explanationPenaltyMitigationBasisPoints;
    public int ApologyPenaltyMitigationBasisPoints =>
        apologyPenaltyMitigationBasisPoints;

    public bool Validate(out string error)
    {
        if (!FinitePositive(minimumExpectedSeconds) ||
            !FiniteNonNegative(attentionMultiplier) ||
            !FiniteNonNegative(attentionOffsetSeconds) ||
            !FiniteNonNegative(delayMultiplier) ||
            !FiniteNonNegative(delayOffsetSeconds) ||
            !FiniteNonNegative(incidentMultiplier) ||
            !FiniteNonNegative(incidentOffsetSeconds) ||
            !FiniteNonNegative(criticalMultiplier) ||
            !FiniteNonNegative(criticalOffsetSeconds) ||
            explanationPenaltyMitigationBasisPoints < 0 ||
            explanationPenaltyMitigationBasisPoints > 10000 ||
            apologyPenaltyMitigationBasisPoints < 0 ||
            apologyPenaltyMitigationBasisPoints > 10000)
        {
            error =
                "La política dinámica de comida contiene valores inválidos.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public BistroBuilderServiceTimingState Evaluate(
        float expectedSeconds,
        float elapsedSeconds)
    {
        GetThresholds(
            expectedSeconds,
            out _,
            out float attention,
            out float delay,
            out float incident,
            out float critical
        );

        float elapsed =
            float.IsNaN(elapsedSeconds) ||
            float.IsInfinity(elapsedSeconds)
                ? 0f
                : Mathf.Max(0f, elapsedSeconds);

        if (elapsed >= critical)
            return BistroBuilderServiceTimingState.Critical;
        if (elapsed >= incident)
            return BistroBuilderServiceTimingState.Incident;
        if (elapsed >= delay)
            return BistroBuilderServiceTimingState.Delay;
        if (elapsed >= attention)
            return BistroBuilderServiceTimingState.Attention;
        return BistroBuilderServiceTimingState.Normal;
    }

    public void GetThresholds(
        float expectedSeconds,
        out float target,
        out float attention,
        out float delay,
        out float incident,
        out float critical)
    {
        float expected =
            float.IsNaN(expectedSeconds) ||
            float.IsInfinity(expectedSeconds)
                ? minimumExpectedSeconds
                : Mathf.Max(minimumExpectedSeconds, expectedSeconds);

        target = expected;
        attention = Mathf.Max(
            target,
            expected * attentionMultiplier + attentionOffsetSeconds
        );
        delay = Mathf.Max(
            attention,
            expected * delayMultiplier + delayOffsetSeconds
        );
        incident = Mathf.Max(
            delay,
            expected * incidentMultiplier + incidentOffsetSeconds
        );
        critical = Mathf.Max(
            incident,
            expected * criticalMultiplier + criticalOffsetSeconds
        );
    }

    private static bool FinitePositive(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value) &&
               value > 0f;
    }

    private static bool FiniteNonNegative(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value) &&
               value >= 0f;
    }
}

public static class BistroBuilderServiceTimingEvaluator
{
    public static BistroBuilderServiceTimingState Evaluate(
        BistroBuilderServiceTimingProfile profile,
        float elapsedSeconds)
    {
        if (profile == null ||
            float.IsNaN(elapsedSeconds) ||
            float.IsInfinity(elapsedSeconds))
        {
            return BistroBuilderServiceTimingState.Normal;
        }

        float elapsed = Mathf.Max(0f, elapsedSeconds);

        if (elapsed >= profile.CriticalSeconds)
            return BistroBuilderServiceTimingState.Critical;
        if (elapsed >= profile.IncidentSeconds)
            return BistroBuilderServiceTimingState.Incident;
        if (elapsed >= profile.DelaySeconds)
            return BistroBuilderServiceTimingState.Delay;
        if (elapsed >= profile.AttentionSeconds)
            return BistroBuilderServiceTimingState.Attention;

        return BistroBuilderServiceTimingState.Normal;
    }

    public static bool IsDelayOrWorse(BistroBuilderServiceTimingState state)
    {
        return state == BistroBuilderServiceTimingState.Delay ||
               state == BistroBuilderServiceTimingState.Incident ||
               state == BistroBuilderServiceTimingState.Critical;
    }

    public static bool IsActionableWait(BistroBuilderServiceTimingState state)
    {
        return state == BistroBuilderServiceTimingState.Attention ||
               state == BistroBuilderServiceTimingState.Delay ||
               state == BistroBuilderServiceTimingState.Incident ||
               state == BistroBuilderServiceTimingState.Critical;
    }
}
