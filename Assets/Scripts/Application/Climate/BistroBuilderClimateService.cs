using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Climate/Climate Service")]
public sealed class BistroBuilderClimateService :
    MonoBehaviour,
    IBistroBuilderClimateReader
{
    [Header("Configuración")]
    [SerializeField] private BistroBuilderClimateSettings settings;

    [Header("Dependencias")]
    [SerializeField] private GameClock gameClock;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameState;

    [Header("Depuración")]
    [SerializeField] private bool logWeatherChanges;

    private readonly List<BistroBuilderForecastDay> forecast =
        new List<BistroBuilderForecastDay>();
    private BistroBuilderWeatherState currentWeather;
    private int runtimeSeed;
    private long revision = 1L;
    private bool initialized;
    private bool hasDebugOverride;
    private BistroBuilderWeatherDebugPreset debugPreset;
    private bool overrideTemperature;
    private float overrideTemperatureC;

    public event Action<BistroBuilderWeatherState> WeatherChanged;
    public event Action ForecastChanged;

    public BistroBuilderWeatherState CurrentWeather =>
        currentWeather != null ? currentWeather.DeepClone() : null;

    public IReadOnlyList<BistroBuilderForecastDay> Forecast => forecast;
    public int RuntimeSeed => runtimeSeed;
    public bool IsInitialized => initialized;
    public bool HasDebugOverride => hasDebugOverride;

    private void Awake()
    {
        CacheDependencies();
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void Start()
    {
        TryInitializeForCurrentGame(out _);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();

        if (settings == null)
        {
            error = "Falta BistroBuilderClimateSettings.";
            return false;
        }

        if (!settings.Validate(out error))
            return false;

        if (gameClock == null)
        {
            error = "Falta GameClock.";
            return false;
        }

        if (generalGameState == null)
        {
            error = "Falta BistroBuilderGeneralGameStateService.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryInitializeForCurrentGame(out string error)
    {
        if (!ValidateConfiguration(out error))
            return false;

        if (!initialized)
        {
            runtimeSeed = BistroBuilderClimateEngine.DeriveRuntimeSeed(
                settings,
                generalGameState.GameId);
            revision = 1L;
            initialized = true;
        }

        RefreshAll(true);
        return true;
    }

    public BistroBuilderOutdoorClimateEvaluation EvaluateOutdoor(
        bool isOutdoor,
        bool isCovered)
    {
        EnsureInitialized();
        return BistroBuilderClimateEngine.EvaluateOutdoor(
            currentWeather,
            isOutdoor,
            isCovered);
    }

    public BistroBuilderClimateSnapshot CreateSnapshot()
    {
        EnsureInitialized();
        return new BistroBuilderClimateSnapshot
        {
            runtimeSeed = runtimeSeed,
            revision = revision
        };
    }

    public bool TryRestoreSnapshot(
        BistroBuilderClimateSnapshot snapshot,
        out string error)
    {
        if (!ValidateConfiguration(out error) ||
            !BistroBuilderClimateEngine.ValidateSnapshot(snapshot, out error))
        {
            return false;
        }

        runtimeSeed = snapshot.runtimeSeed;
        revision = Math.Max(1L, snapshot.revision);
        initialized = true;
        hasDebugOverride = false;
        RefreshAll(true);
        return true;
    }

    public void SetDebugOverride(
        BistroBuilderWeatherDebugPreset preset,
        bool useTemperatureOverride = false,
        float temperatureC = 20f)
    {
        EnsureInitialized();
        hasDebugOverride = true;
        debugPreset = preset;
        overrideTemperature = useTemperatureOverride;
        overrideTemperatureC = temperatureC;
        RefreshCurrent(true);
    }

    public void ClearDebugOverride()
    {
        if (!hasDebugOverride)
            return;

        hasDebugOverride = false;
        overrideTemperature = false;
        RefreshCurrent(true);
    }

    public void RegenerateWithSeed(int seed)
    {
        EnsureInitialized();
        runtimeSeed = seed == 0 ? 1 : seed;
        revision++;
        hasDebugOverride = false;
        RefreshAll(true);
    }

    private void HandleTimeChanged(int hour, int minute)
    {
        if (!initialized)
            return;

        RefreshCurrent(false);
    }

    private void HandleCalendarChanged()
    {
        if (!initialized)
            return;

        RefreshAll(true);
    }

    private void RefreshAll(bool forceEvent)
    {
        RefreshCurrent(forceEvent);
        RebuildForecast();
    }

    private void RefreshCurrent(bool forceEvent)
    {
        BistroBuilderWeatherState next =
            BistroBuilderClimateEngine.GenerateWeather(
                settings,
                runtimeSeed,
                generalGameState.DayIndex,
                generalGameState.CalendarYear,
                generalGameState.CalendarMonth,
                generalGameState.CalendarDay,
                gameClock.Hour,
                gameClock.Minute,
                revision);

        if (hasDebugOverride)
            ApplyDebugOverride(next);

        bool changed = forceEvent || !Equivalent(currentWeather, next);
        currentWeather = next;

        if (changed)
        {
            if (logWeatherChanges)
            {
                Debug.Log(
                    "BB Climate — " +
                    next.temperatureC.ToString("0.0") + " °C, " +
                    next.cloudState +
                    (next.isRaining ? ", lluvia" : string.Empty) +
                    (next.isSnowing ? ", nieve" : string.Empty) +
                    (next.isWindy ? ", viento" : string.Empty),
                    this);
            }

            WeatherChanged?.Invoke(CurrentWeather);
        }
    }

    private void RebuildForecast()
    {
        forecast.Clear();
        forecast.AddRange(BistroBuilderClimateEngine.BuildForecast(
            settings,
            runtimeSeed,
            generalGameState.DayIndex,
            generalGameState.CalendarYear,
            generalGameState.CalendarMonth,
            generalGameState.CalendarDay));
        ForecastChanged?.Invoke();
    }

    private void ApplyDebugOverride(BistroBuilderWeatherState state)
    {
        state.isRaining = false;
        state.isSnowing = false;
        state.isWindy = false;

        switch (debugPreset)
        {
            case BistroBuilderWeatherDebugPreset.Clear:
                state.cloudState = BistroBuilderCloudState.Clear;
                break;
            case BistroBuilderWeatherDebugPreset.Cloudy:
                state.cloudState = BistroBuilderCloudState.Cloudy;
                break;
            case BistroBuilderWeatherDebugPreset.Rain:
                state.cloudState = BistroBuilderCloudState.Cloudy;
                state.isRaining = true;
                break;
            case BistroBuilderWeatherDebugPreset.Snow:
                state.cloudState = BistroBuilderCloudState.Cloudy;
                state.isSnowing = true;
                break;
            case BistroBuilderWeatherDebugPreset.Wind:
                state.isWindy = true;
                break;
        }

        if (overrideTemperature)
            state.temperatureC = overrideTemperatureC;
    }

    private void EnsureInitialized()
    {
        if (!initialized && !TryInitializeForCurrentGame(out string error))
            throw new InvalidOperationException(error);
    }

    private void CacheDependencies()
    {
        if (gameClock == null)
            TryGetComponent(out gameClock);
        if (generalGameState == null)
            TryGetComponent(out generalGameState);
    }

    private void Subscribe()
    {
        if (gameClock != null)
        {
            gameClock.TimeChanged -= HandleTimeChanged;
            gameClock.TimeChanged += HandleTimeChanged;
        }

        if (generalGameState != null)
        {
            generalGameState.CalendarChanged -= HandleCalendarChanged;
            generalGameState.CalendarChanged += HandleCalendarChanged;
        }
    }

    private void Unsubscribe()
    {
        if (gameClock != null)
            gameClock.TimeChanged -= HandleTimeChanged;
        if (generalGameState != null)
            generalGameState.CalendarChanged -= HandleCalendarChanged;
    }

    private static bool Equivalent(
        BistroBuilderWeatherState a,
        BistroBuilderWeatherState b)
    {
        if (a == null || b == null)
            return false;

        return a.dayIndex == b.dayIndex &&
               a.hour == b.hour &&
               Mathf.Abs(a.temperatureC - b.temperatureC) < 0.09f &&
               a.cloudState == b.cloudState &&
               a.isRaining == b.isRaining &&
               a.isSnowing == b.isSnowing &&
               a.isWindy == b.isWindy;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependencies();
    }

    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
