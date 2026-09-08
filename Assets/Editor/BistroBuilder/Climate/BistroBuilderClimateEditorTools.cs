using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderClimateEditorTools
{
    public const string SettingsAssetPath =
        "Assets/Data/BistroBuilder/Climate/BB_Climate_Settings.asset";
    public const string PrototypeScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";

    [MenuItem("Tools/Bistro Builder/Climatología/V1 - Instalar + validar + autotest", false, 3400)]
    public static void InstallValidateAndTest()
    {
        try
        {
            InstallIntoScene(SceneManager.GetActiveScene());
            RunOrThrow();
            EditorUtility.DisplayDialog(
                "Bistro Builder — Climatología V1",
                "Instalación, validación y autotest correctos.",
                "Aceptar");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Bistro Builder — Climatología V1",
                exception.Message,
                "Aceptar");
        }
    }

    [MenuItem("Tools/Bistro Builder/Climatología/V1 - Validar", false, 3401)]
    public static void ValidateFromMenu()
    {
        bool ok = ValidateCurrentScene(out int passed, out int failed, out string report);
        Debug.Log(report);
        if (!ok) Debug.LogError("BB Climate V1 — validación fallida.");
        EditorUtility.DisplayDialog(
            "Bistro Builder — Climatología V1",
            passed + " OK / " + failed + " errores.",
            "Aceptar");
    }

    [MenuItem("Tools/Bistro Builder/Climatología/V1 - Autotest", false, 3402)]
    public static void SelfTestFromMenu()
    {
        bool ok = RunSelfTest(out int passed, out int failed, out string report);
        Debug.Log(report);
        if (!ok) Debug.LogError("BB Climate V1 — autotest fallido.");
        EditorUtility.DisplayDialog(
            "Bistro Builder — Climatología V1",
            passed + " OK / " + failed + " fallos.",
            "Aceptar");
    }

    public static void InstallPrototypeRestaurantBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(
            PrototypeScenePath,
            OpenSceneMode.Single);
        InstallIntoScene(scene);
        RunOrThrow();
    }

    private static void RunOrThrow()
    {
        bool validationOk = ValidateCurrentScene(
            out int validationPassed,
            out int validationFailed,
            out string validationReport);
        bool selfTestOk = RunSelfTest(
            out int testPassed,
            out int testFailed,
            out string testReport);

        Debug.Log(validationReport);
        Debug.Log(testReport);

        if (!validationOk || !selfTestOk)
        {
            throw new InvalidOperationException(
                "BB Climate V1 FAIL — validación " +
                validationPassed + "/" + validationFailed +
                ", autotest " + testPassed + "/" + testFailed + ".");
        }

        Debug.Log(
            "BB_CLIMATE_BATCH_PASS validation=" +
            validationPassed + " selftest=" + testPassed);
    }

    public static bool ValidateCurrentScene(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        StringBuilder builder = new StringBuilder();
        GameObject root = FindGameSystems(SceneManager.GetActiveScene());

        Check(root != null, "GameSystems existe.", ref passed, ref failed, builder);
        if (root != null)
        {
            Check(root.GetComponent<GameClock>() != null,
                "GameClock disponible.", ref passed, ref failed, builder);
            Check(root.GetComponent<BistroBuilderGeneralGameStateService>() != null,
                "Calendario global disponible.", ref passed, ref failed, builder);
            Check(root.GetComponent<BistroBuilderSaveGameService>() != null,
                "SaveGameService disponible.", ref passed, ref failed, builder);

            BistroBuilderClimateService climate =
                root.GetComponent<BistroBuilderClimateService>();
            BistroBuilderClimateSaveSectionProvider provider =
                root.GetComponent<BistroBuilderClimateSaveSectionProvider>();

            Check(climate != null, "ClimateService instalado.",
                ref passed, ref failed, builder);
            Check(provider != null, "climate.runtime instalado.",
                ref passed, ref failed, builder);
            Check(root.GetComponent<BistroBuilderWeatherVisualController>() != null,
                "WeatherVisualController instalado.",
                ref passed, ref failed, builder);

            if (climate != null)
                Check(climate.ValidateConfiguration(out _),
                    "ClimateService configura limpio.",
                    ref passed, ref failed, builder);
            if (provider != null)
                Check(provider.ValidateConfiguration(out _),
                    "Proveedor de guardado configura limpio.",
                    ref passed, ref failed, builder);
        }

        Check(
            AssetDatabase.LoadAssetAtPath<BistroBuilderClimateSettings>(
                SettingsAssetPath) != null,
            "Asset de configuración existe.",
            ref passed, ref failed, builder);

        builder.Insert(0,
            "BB CLIMATE V1 — VALIDACIÓN\nCorrectos: " +
            passed + "  Errores: " + failed + "\n\n");
        report = builder.ToString();
        return failed == 0;
    }

    public static bool RunSelfTest(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        StringBuilder builder = new StringBuilder();
        BistroBuilderClimateSettings settings =
            ScriptableObject.CreateInstance<BistroBuilderClimateSettings>();

        try
        {
            Check(settings.Validate(out _),
                "Settings estándar válidos.",
                ref passed, ref failed, builder);

            const int seed = 43821;
            BistroBuilderWeatherState a =
                BistroBuilderClimateEngine.GenerateWeather(
                    settings, seed, 40, 2026, 9, 8, 14, 30);
            BistroBuilderWeatherState b =
                BistroBuilderClimateEngine.GenerateWeather(
                    settings, seed, 40, 2026, 9, 8, 14, 30);

            Check(WeatherEquivalent(a, b),
                "Generación determinista.",
                ref passed, ref failed, builder);

            bool exclusive = true;
            for (int day = 1; day <= 120 && exclusive; day++)
            {
                for (int hour = 0; hour < 24; hour += 3)
                {
                    BistroBuilderWeatherState state =
                        BistroBuilderClimateEngine.GenerateWeather(
                            settings, seed, day, 2026,
                            ((day - 1) / 28) % 12 + 1,
                            (day - 1) % 28 + 1, hour, 0);
                    if (state.isRaining && state.isSnowing)
                    {
                        exclusive = false;
                        break;
                    }
                }
            }
            Check(exclusive, "Lluvia y nieve son excluyentes.",
                ref passed, ref failed, builder);

            BistroBuilderOutdoorClimateEvaluation interior =
                BistroBuilderClimateEngine.EvaluateOutdoor(a, false, false);
            Check(interior.comfort01 == 1f &&
                  interior.rating == BistroBuilderOutdoorClimateRating.Excellent,
                "Interior siempre confortable.",
                ref passed, ref failed, builder);

            var rain = new BistroBuilderWeatherState
            {
                temperatureC = 20f,
                cloudState = BistroBuilderCloudState.Cloudy,
                isRaining = true
            };
            BistroBuilderOutdoorClimateEvaluation uncovered =
                BistroBuilderClimateEngine.EvaluateOutdoor(rain, true, false);
            BistroBuilderOutdoorClimateEvaluation covered =
                BistroBuilderClimateEngine.EvaluateOutdoor(rain, true, true);

            Check(uncovered.directPrecipitation,
                "Exterior descubierto recibe precipitación.",
                ref passed, ref failed, builder);
            Check(!covered.directPrecipitation,
                "Cobertura neutraliza precipitación directa.",
                ref passed, ref failed, builder);
            Check(covered.comfort01 > uncovered.comfort01,
                "Pérgola y parasol comparten regla de cobertura.",
                ref passed, ref failed, builder);

            var forecastA = BistroBuilderClimateEngine.BuildForecast(
                settings, seed, 40, 2026, 9, 8);
            var forecastB = BistroBuilderClimateEngine.BuildForecast(
                settings, seed, 40, 2026, 9, 8);

            Check(forecastA.Count == settings.ForecastDays,
                "Previsión usa el horizonte configurado.",
                ref passed, ref failed, builder);
            Check(ForecastEquivalent(forecastA, forecastB),
                "Previsión determinista.",
                ref passed, ref failed, builder);

            var snapshot = new BistroBuilderClimateSnapshot
            {
                runtimeSeed = seed,
                revision = 3L
            };
            Check(BistroBuilderClimateEngine.ValidateSnapshot(snapshot, out _),
                "climate.runtime válido.",
                ref passed, ref failed, builder);
        }
        catch (Exception exception)
        {
            failed++;
            builder.AppendLine("[ERROR] " + exception);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }

        builder.Insert(0,
            "BB CLIMATE V1 — AUTOTEST\nCorrectos: " +
            passed + "  Fallos: " + failed + "\n\n");
        report = builder.ToString();
        return failed == 0;
    }

    private static void InstallIntoScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Escena no válida.");

        GameObject root = FindGameSystems(scene);
        if (root == null)
            throw new InvalidOperationException("No se encontró GameSystems.");
        if (root.GetComponent<GameClock>() == null ||
            root.GetComponent<BistroBuilderGeneralGameStateService>() == null ||
            root.GetComponent<BistroBuilderSaveGameService>() == null)
        {
            throw new InvalidOperationException(
                "GameSystems no contiene las dependencias base.");
        }

        BistroBuilderClimateSettings settings = GetOrCreateSettings();
        BistroBuilderClimateService climate = GetOrAdd<BistroBuilderClimateService>(root);
        GetOrAdd<BistroBuilderClimateSaveSectionProvider>(root);
        GetOrAdd<BistroBuilderWeatherVisualController>(root);

        SerializedObject serialized = new SerializedObject(climate);
        serialized.FindProperty("settings").objectReferenceValue = settings;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(climate);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("No se pudo guardar la escena.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static BistroBuilderClimateSettings GetOrCreateSettings()
    {
        BistroBuilderClimateSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderClimateSettings>(
                SettingsAssetPath);
        if (settings != null) return settings;

        settings = ScriptableObject.CreateInstance<BistroBuilderClimateSettings>();
        AssetDatabase.CreateAsset(settings, SettingsAssetPath);
        AssetDatabase.SaveAssets();
        return settings;
    }

    private static GameObject FindGameSystems(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root != null &&
                string.Equals(root.name, "GameSystems", StringComparison.Ordinal))
                return root;
        }
        return null;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(target);
    }

    private static bool WeatherEquivalent(
        BistroBuilderWeatherState a,
        BistroBuilderWeatherState b)
    {
        return a != null && b != null &&
               a.dayIndex == b.dayIndex &&
               a.hour == b.hour &&
               a.minute == b.minute &&
               Mathf.Approximately(a.temperatureC, b.temperatureC) &&
               a.cloudState == b.cloudState &&
               a.isRaining == b.isRaining &&
               a.isSnowing == b.isSnowing &&
               a.isWindy == b.isWindy;
    }

    private static bool ForecastEquivalent(
        System.Collections.Generic.IReadOnlyList<BistroBuilderForecastDay> a,
        System.Collections.Generic.IReadOnlyList<BistroBuilderForecastDay> b)
    {
        if (a == null || b == null || a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            BistroBuilderForecastDay x = a[i];
            BistroBuilderForecastDay y = b[i];
            if (x.dayIndex != y.dayIndex ||
                !Mathf.Approximately(x.minimumTemperatureC, y.minimumTemperatureC) ||
                !Mathf.Approximately(x.maximumTemperatureC, y.maximumTemperatureC) ||
                x.dominantCloudState != y.dominantCloudState ||
                x.rainExpected != y.rainExpected ||
                x.snowExpected != y.snowExpected ||
                x.windExpected != y.windExpected)
                return false;
        }
        return true;
    }

    private static void Check(
        bool condition,
        string label,
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        if (condition)
        {
            passed++;
            builder.AppendLine("[OK] " + label);
        }
        else
        {
            failed++;
            builder.AppendLine("[ERROR] " + label);
        }
    }
}
