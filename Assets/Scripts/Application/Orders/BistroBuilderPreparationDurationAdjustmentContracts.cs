using System.Collections.Generic;

/// <summary>
/// Contexto neutral para ajustar la duración runtime de preparación de una línea.
/// La receta y el precio histórico permanecen inmutables.
/// </summary>
public sealed class BistroBuilderPreparationDurationAdjustmentContext
{
    public string canonicalOrderId = string.Empty;
    public string customerGroupReferenceId = string.Empty;
    public string acquisitionSegmentId = "general";
    public string dishId = string.Empty;
    public BistroBuilderServiceMode serviceMode = BistroBuilderServiceMode.TableService;
    public BistroBuilderMealServiceAvailability mealService =
        BistroBuilderMealServiceAvailability.Lunch;
    public float baseDurationSeconds;
    public float minimumDurationSeconds;
    public float maximumDurationSeconds;
}

/// <summary>
/// Puerto extensible de ajustes de carga operativa sobre cocina.
/// Ninguna implementación externa obtiene autoridad sobre estados o recetas.
/// </summary>
public interface IBistroBuilderPreparationDurationAdjustmentProvider
{
    string AdjustmentProviderId { get; }

    bool TryGetAdjustmentBasisPoints(
        BistroBuilderPreparationDurationAdjustmentContext context,
        out int adjustmentBasisPoints,
        out string error);
}

/// <summary>Política pura de aplicación del ajuste acumulado.</summary>
public static class BistroBuilderPreparationDurationAdjustmentPolicy
{
    public const int MinimumAdjustmentBasisPoints = -5000;
    public const int MaximumAdjustmentBasisPoints = 50000;

    public static bool TryApply(
        float baseDurationSeconds,
        float minimumDurationSeconds,
        float maximumDurationSeconds,
        int adjustmentBasisPoints,
        out float adjustedDurationSeconds,
        out string error)
    {
        adjustedDurationSeconds = 0f;
        if (baseDurationSeconds < 0f || minimumDurationSeconds <= 0f ||
            maximumDurationSeconds < minimumDurationSeconds)
        {
            error = "Los tiempos de preparación del ajuste son inválidos.";
            return false;
        }

        if (adjustmentBasisPoints < MinimumAdjustmentBasisPoints ||
            adjustmentBasisPoints > MaximumAdjustmentBasisPoints)
        {
            error = "El ajuste de duración queda fuera del rango seguro.";
            return false;
        }

        if (adjustmentBasisPoints == 0)
        {
            adjustedDurationSeconds = baseDurationSeconds;
            error = string.Empty;
            return true;
        }

        double multiplier = 1d + adjustmentBasisPoints / 10000d;
        double adjusted = baseDurationSeconds * multiplier;
        if (double.IsNaN(adjusted) || double.IsInfinity(adjusted))
        {
            error = "El ajuste de duración produjo un valor no finito.";
            return false;
        }

        adjustedDurationSeconds = UnityEngine.Mathf.Clamp(
            (float)adjusted,
            minimumDurationSeconds,
            maximumDurationSeconds);
        error = string.Empty;
        return true;
    }
}
