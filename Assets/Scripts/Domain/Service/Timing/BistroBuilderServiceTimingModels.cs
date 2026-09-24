using System;
using UnityEngine;

/// <summary>
/// Fases de servicio que disponen de umbrales temporales canónicos.
/// Se amplía de forma incremental cuando una vertical real lo necesita.
/// </summary>
public enum BistroBuilderServiceTimingPhase
{
    BillDelivery = 0
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

    public BistroBuilderServiceTimingPhase Phase => phase;
    public float TargetSeconds => targetSeconds;
    public float AttentionSeconds => attentionSeconds;
    public float DelaySeconds => delaySeconds;
    public float IncidentSeconds => incidentSeconds;
    public float CriticalSeconds => criticalSeconds;
    public int ExplanationPenaltyMitigationBasisPoints =>
        explanationPenaltyMitigationBasisPoints;

    public bool Validate(out string error)
    {
        if (!IsFiniteNonNegative(targetSeconds) ||
            !IsFiniteNonNegative(attentionSeconds) ||
            !IsFiniteNonNegative(delaySeconds) ||
            !IsFiniteNonNegative(incidentSeconds) ||
            !IsFiniteNonNegative(criticalSeconds) ||
            explanationPenaltyMitigationBasisPoints < 0 ||
            explanationPenaltyMitigationBasisPoints > 10000)
        {
            error = "Los umbrales deben ser finitos/no negativos y la mitigación debe estar entre 0 y 10000 pb.";
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
