using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderClimateDebugWindow : EditorWindow
{
    private BistroBuilderWeatherDebugPreset preset =
        BistroBuilderWeatherDebugPreset.Clear;
    private bool overrideTemperature;
    private float temperatureC = 20f;
    private int seed = 17391;

    [MenuItem("Tools/Bistro Builder/Climatología/Climate Studio", false, 3410)]
    private static void Open()
    {
        GetWindow<BistroBuilderClimateDebugWindow>(
            "BB Climate Studio");
    }

    private void OnGUI()
    {
        BistroBuilderClimateService service =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderClimateService>();

        EditorGUILayout.LabelField(
            "BB Climate & Weather System",
            EditorStyles.boldLabel);

        if (service == null)
        {
            EditorGUILayout.HelpBox(
                "No hay BistroBuilderClimateService en la escena.",
                MessageType.Warning);
            return;
        }

        BistroBuilderWeatherState state = service.CurrentWeather;
        if (state != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Actual",
                state.temperatureC.ToString("0.0") + " °C · " +
                BuildDescription(state));
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            preset = (BistroBuilderWeatherDebugPreset)
                EditorGUILayout.EnumPopup("Forzar estado", preset);
            overrideTemperature =
                EditorGUILayout.Toggle("Forzar temperatura", overrideTemperature);

            if (overrideTemperature)
                temperatureC = EditorGUILayout.FloatField("Temperatura °C", temperatureC);

            if (GUILayout.Button("Aplicar estado forzado"))
            {
                service.SetDebugOverride(
                    preset,
                    overrideTemperature,
                    temperatureC);
            }

            if (GUILayout.Button("Volver al clima generado"))
                service.ClearDebugOverride();

            EditorGUILayout.Space();
            seed = EditorGUILayout.IntField("Seed", seed);
            if (GUILayout.Button("Regenerar con seed"))
                service.RegenerateWithSeed(seed);
        }

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Los controles de runtime se habilitan en Play Mode.",
                MessageType.Info);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Previsión", EditorStyles.boldLabel);

        var forecast = service.Forecast;
        for (int index = 0; index < forecast.Count; index++)
        {
            BistroBuilderForecastDay day = forecast[index];
            EditorGUILayout.LabelField(
                day.calendarDay.ToString("00") + "/" +
                day.calendarMonth.ToString("00"),
                day.minimumTemperatureC.ToString("0.0") + "–" +
                day.maximumTemperatureC.ToString("0.0") + " °C · " +
                BuildForecastDescription(day));
        }
    }

    private static string BuildDescription(BistroBuilderWeatherState state)
    {
        if (state.isSnowing) return "Nieve";
        if (state.isRaining) return "Lluvia";
        if (state.isWindy && state.cloudState == BistroBuilderCloudState.Cloudy)
            return "Nublado · Viento";
        if (state.isWindy) return "Despejado · Viento";
        return state.cloudState == BistroBuilderCloudState.Cloudy
            ? "Nublado"
            : "Despejado";
    }

    private static string BuildForecastDescription(BistroBuilderForecastDay day)
    {
        string result = day.dominantCloudState == BistroBuilderCloudState.Cloudy
            ? "Nublado"
            : "Despejado";

        if (day.rainExpected) result += " · Lluvia";
        if (day.snowExpected) result += " · Nieve";
        if (day.windExpected) result += " · Viento";
        return result;
    }
}
