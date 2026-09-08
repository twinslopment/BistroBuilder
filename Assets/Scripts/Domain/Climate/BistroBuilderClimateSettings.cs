using UnityEngine;

[CreateAssetMenu(
    fileName = "BB_Climate_Settings",
    menuName = "Bistro Builder/Climate/Climate Settings"
)]
public sealed class BistroBuilderClimateSettings : ScriptableObject
{
    [Header("Generación")]
    [SerializeField] private int baseSeed = 17391;
    [SerializeField] private bool deriveSeedFromGameId = true;
    [SerializeField, Range(1, 8)] private int forecastDays = 5;
    [SerializeField, Range(2, 6)] private int weatherStepHours = 3;

    [Header("Temperatura")]
    [SerializeField, Range(0f, 10f)] private float diurnalAmplitudeC = 4.5f;
    [SerializeField, Range(0f, 8f)] private float dailyVariationC = 2.5f;
    [SerializeField, Range(-5f, 5f)] private float snowThresholdC = 1.5f;

    [SerializeField] private float[] monthlyMeanTemperatureC =
    {
        7f, 8f, 11f, 14f, 18f, 22f,
        25f, 25f, 21f, 16f, 11f, 8f
    };

    [Header("Probabilidades estándar")]
    [SerializeField] private float[] monthlyCloudChance =
    {
        0.58f, 0.55f, 0.50f, 0.46f, 0.40f, 0.34f,
        0.30f, 0.31f, 0.38f, 0.47f, 0.55f, 0.59f
    };

    [SerializeField] private float[] monthlyPrecipitationChance =
    {
        0.20f, 0.18f, 0.17f, 0.16f, 0.14f, 0.11f,
        0.08f, 0.09f, 0.13f, 0.17f, 0.20f, 0.21f
    };

    [SerializeField] private float[] monthlyWindChance =
    {
        0.20f, 0.20f, 0.18f, 0.17f, 0.15f, 0.13f,
        0.12f, 0.12f, 0.15f, 0.18f, 0.20f, 0.21f
    };

    public int BaseSeed => baseSeed;
    public bool DeriveSeedFromGameId => deriveSeedFromGameId;
    public int ForecastDays => forecastDays;
    public int WeatherStepHours => weatherStepHours;
    public float DiurnalAmplitudeC => diurnalAmplitudeC;
    public float DailyVariationC => dailyVariationC;
    public float SnowThresholdC => snowThresholdC;

    public float GetMonthlyMeanTemperature(int month) =>
        ReadMonth(monthlyMeanTemperatureC, month, 15f);

    public float GetMonthlyCloudChance(int month) =>
        Mathf.Clamp01(ReadMonth(monthlyCloudChance, month, 0.45f));

    public float GetMonthlyPrecipitationChance(int month) =>
        Mathf.Clamp01(ReadMonth(monthlyPrecipitationChance, month, 0.15f));

    public float GetMonthlyWindChance(int month) =>
        Mathf.Clamp01(ReadMonth(monthlyWindChance, month, 0.17f));

    public bool Validate(out string error)
    {
        if (forecastDays < 1 || weatherStepHours < 1)
        {
            error = "ForecastDays o WeatherStepHours inválidos.";
            return false;
        }

        if (!HasTwelve(monthlyMeanTemperatureC) ||
            !HasTwelve(monthlyCloudChance) ||
            !HasTwelve(monthlyPrecipitationChance) ||
            !HasTwelve(monthlyWindChance))
        {
            error = "Las curvas mensuales de climatología deben tener 12 valores.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool HasTwelve(float[] values) =>
        values != null && values.Length == 12;

    private static float ReadMonth(float[] values, int month, float fallback)
    {
        if (!HasTwelve(values))
        {
            return fallback;
        }

        return values[Mathf.Clamp(month, 1, 12) - 1];
    }
}
