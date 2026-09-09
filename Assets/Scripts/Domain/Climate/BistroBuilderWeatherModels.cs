using System;
using System.Collections.Generic;
using UnityEngine;

public enum BistroBuilderCloudState
{
    Clear = 0,
    Cloudy = 1
}

public enum BistroBuilderOutdoorClimateRating
{
    Excellent = 0,
    Good = 1,
    Acceptable = 2,
    Poor = 3,
    Unusable = 4
}

public enum BistroBuilderWeatherDebugPreset
{
    Clear = 0,
    Cloudy = 1,
    Rain = 2,
    Snow = 3,
    Wind = 4
}

[Serializable]
public sealed class BistroBuilderWeatherState
{
    public int dayIndex = 1;
    public int calendarYear = 1;
    public int calendarMonth = 1;
    public int calendarDay = 1;
    public int hour;
    public int minute;
    public float temperatureC;
    public BistroBuilderCloudState cloudState;
    public bool isRaining;
    public bool isSnowing;
    public bool isWindy;
    public long revision;

    public BistroBuilderWeatherState DeepClone()
    {
        return (BistroBuilderWeatherState)MemberwiseClone();
    }
}

[Serializable]
public sealed class BistroBuilderForecastDay
{
    public int dayIndex = 1;
    public int calendarYear = 1;
    public int calendarMonth = 1;
    public int calendarDay = 1;
    public float minimumTemperatureC;
    public float maximumTemperatureC;
    public BistroBuilderCloudState dominantCloudState;
    public bool rainExpected;
    public bool snowExpected;
    public bool windExpected;
}

[Serializable]
public sealed class BistroBuilderOutdoorClimateEvaluation
{
    public bool isOutdoor;
    public bool isCovered;
    public bool directPrecipitation;
    [Range(0f, 1f)] public float comfort01 = 1f;
    public BistroBuilderOutdoorClimateRating rating =
        BistroBuilderOutdoorClimateRating.Excellent;
}

[Serializable]
public sealed class BistroBuilderClimateSnapshot
{
    public const string CurrentSchemaId = "climate.runtime";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;
    public int runtimeSeed;
    public long revision = 1L;
}

public interface IBistroBuilderClimateReader
{
    BistroBuilderWeatherState CurrentWeather { get; }
    IReadOnlyList<BistroBuilderForecastDay> Forecast { get; }

    BistroBuilderOutdoorClimateEvaluation EvaluateOutdoor(
        bool isOutdoor,
        bool isCovered
    );
}
