using System;
using System.Collections.Generic;
using UnityEngine;

public static class BistroBuilderClimateEngine
{
    public static BistroBuilderWeatherState GenerateWeather(
        BistroBuilderClimateSettings settings,
        int runtimeSeed,
        int dayIndex,
        int year,
        int month,
        int day,
        int hour,
        int minute,
        long revision = 1L)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        int safeDayIndex = Mathf.Max(1, dayIndex);
        int safeHour = Mathf.Clamp(hour, 0, 23);
        int safeMinute = Mathf.Clamp(minute, 0, 59);
        int stepHours = Mathf.Max(1, settings.WeatherStepHours);
        int stepsPerDay = Mathf.Max(1, 24 / stepHours);
        int segment = Mathf.Clamp(safeHour / stepHours, 0, stepsPerDay - 1);
        long token = (long)safeDayIndex * stepsPerDay + segment;

        float hourFraction = safeHour + safeMinute / 60f;
        float diurnal = Mathf.Sin((hourFraction - 9f) / 24f * Mathf.PI * 2f);
        float dailyOffset = (Hash01(runtimeSeed, safeDayIndex, 101) * 2f - 1f) *
                            settings.DailyVariationC;
        float temperature = settings.GetMonthlyMeanTemperature(month) +
                            dailyOffset +
                            diurnal * settings.DiurnalAmplitudeC;

        float cloudNoise = SmoothHash(runtimeSeed, token, 201);
        float precipitationNoise = SmoothHash(runtimeSeed, token, 301);
        float windNoise = SmoothHash(runtimeSeed, token, 401);

        bool precipitation =
            precipitationNoise < settings.GetMonthlyPrecipitationChance(month);
        bool snowing = precipitation && temperature <= settings.SnowThresholdC;
        bool raining = precipitation && !snowing;
        bool windy = windNoise < settings.GetMonthlyWindChance(month);
        bool cloudy =
            precipitation ||
            cloudNoise < settings.GetMonthlyCloudChance(month);

        return new BistroBuilderWeatherState
        {
            dayIndex = safeDayIndex,
            calendarYear = Mathf.Max(1, year),
            calendarMonth = Mathf.Clamp(month, 1, 12),
            calendarDay = Mathf.Max(1, day),
            hour = safeHour,
            minute = safeMinute,
            temperatureC = Mathf.Round(temperature * 10f) / 10f,
            cloudState = cloudy
                ? BistroBuilderCloudState.Cloudy
                : BistroBuilderCloudState.Clear,
            isRaining = raining,
            isSnowing = snowing,
            isWindy = windy,
            revision = Math.Max(1L, revision)
        };
    }

    public static List<BistroBuilderForecastDay> BuildForecast(
        BistroBuilderClimateSettings settings,
        int runtimeSeed,
        int dayIndex,
        int year,
        int month,
        int day)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        var result = new List<BistroBuilderForecastDay>(settings.ForecastDays);
        var start = new DateTime(
            Mathf.Clamp(year, 1, 9999),
            Mathf.Clamp(month, 1, 12),
            Mathf.Clamp(day, 1, DateTime.DaysInMonth(
                Mathf.Clamp(year, 1, 9999),
                Mathf.Clamp(month, 1, 12))));

        for (int offset = 0; offset < settings.ForecastDays; offset++)
        {
            DateTime date = start.AddDays(offset);
            int forecastDayIndex = Mathf.Max(1, dayIndex + offset);
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            int cloudySamples = 0;
            int samples = 0;
            bool rain = false;
            bool snow = false;
            bool wind = false;

            for (int hour = 0; hour < 24; hour += settings.WeatherStepHours)
            {
                BistroBuilderWeatherState state = GenerateWeather(
                    settings,
                    runtimeSeed,
                    forecastDayIndex,
                    date.Year,
                    date.Month,
                    date.Day,
                    hour,
                    0);

                min = Mathf.Min(min, state.temperatureC);
                max = Mathf.Max(max, state.temperatureC);
                cloudySamples += state.cloudState == BistroBuilderCloudState.Cloudy ? 1 : 0;
                rain |= state.isRaining;
                snow |= state.isSnowing;
                wind |= state.isWindy;
                samples++;
            }

            result.Add(new BistroBuilderForecastDay
            {
                dayIndex = forecastDayIndex,
                calendarYear = date.Year,
                calendarMonth = date.Month,
                calendarDay = date.Day,
                minimumTemperatureC = min,
                maximumTemperatureC = max,
                dominantCloudState = cloudySamples * 2 >= samples
                    ? BistroBuilderCloudState.Cloudy
                    : BistroBuilderCloudState.Clear,
                rainExpected = rain,
                snowExpected = snow,
                windExpected = wind
            });
        }

        return result;
    }

    public static BistroBuilderOutdoorClimateEvaluation EvaluateOutdoor(
        BistroBuilderWeatherState weather,
        bool isOutdoor,
        bool isCovered)
    {
        if (!isOutdoor)
        {
            return new BistroBuilderOutdoorClimateEvaluation
            {
                isOutdoor = false,
                isCovered = true,
                directPrecipitation = false,
                comfort01 = 1f,
                rating = BistroBuilderOutdoorClimateRating.Excellent
            };
        }

        if (weather == null)
            throw new ArgumentNullException(nameof(weather));

        bool directPrecipitation =
            (weather.isRaining || weather.isSnowing) && !isCovered;

        float comfort = TemperatureComfort(weather.temperatureC);

        if (weather.isWindy)
            comfort -= 0.15f;

        if (directPrecipitation)
            comfort = Mathf.Min(comfort - 0.35f, 0.15f);

        comfort = Mathf.Clamp01(comfort);

        return new BistroBuilderOutdoorClimateEvaluation
        {
            isOutdoor = true,
            isCovered = isCovered,
            directPrecipitation = directPrecipitation,
            comfort01 = comfort,
            rating = ToRating(comfort)
        };
    }

    public static int DeriveRuntimeSeed(
        BistroBuilderClimateSettings settings,
        string gameId)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        if (!settings.DeriveSeedFromGameId || string.IsNullOrWhiteSpace(gameId))
            return settings.BaseSeed;

        unchecked
        {
            uint hash = 2166136261u;
            string normalized = gameId.Trim().ToLowerInvariant();
            for (int i = 0; i < normalized.Length; i++)
            {
                hash ^= normalized[i];
                hash *= 16777619u;
            }

            hash ^= (uint)settings.BaseSeed;
            return (int)(hash == 0u ? 1u : hash);
        }
    }

    public static bool ValidateSnapshot(
        BistroBuilderClimateSnapshot snapshot,
        out string error)
    {
        if (snapshot == null)
        {
            error = "climate.runtime es nulo.";
            return false;
        }

        if (!string.Equals(
                snapshot.schemaId,
                BistroBuilderClimateSnapshot.CurrentSchemaId,
                StringComparison.Ordinal) ||
            snapshot.schemaVersion != BistroBuilderClimateSnapshot.CurrentSchemaVersion ||
            snapshot.revision < 1L)
        {
            error = "climate.runtime tiene esquema o revisión inválidos.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static float TemperatureComfort(float temperatureC)
    {
        if (temperatureC >= 18f && temperatureC <= 26f)
            return 1f;
        if (temperatureC >= 14f && temperatureC < 18f)
            return Mathf.Lerp(0.65f, 1f, (temperatureC - 14f) / 4f);
        if (temperatureC > 26f && temperatureC <= 30f)
            return Mathf.Lerp(1f, 0.65f, (temperatureC - 26f) / 4f);
        if (temperatureC >= 9f && temperatureC < 14f)
            return Mathf.Lerp(0.30f, 0.65f, (temperatureC - 9f) / 5f);
        if (temperatureC > 30f && temperatureC <= 34f)
            return Mathf.Lerp(0.65f, 0.30f, (temperatureC - 30f) / 4f);
        return 0.15f;
    }

    private static BistroBuilderOutdoorClimateRating ToRating(float comfort)
    {
        if (comfort >= 0.85f) return BistroBuilderOutdoorClimateRating.Excellent;
        if (comfort >= 0.65f) return BistroBuilderOutdoorClimateRating.Good;
        if (comfort >= 0.40f) return BistroBuilderOutdoorClimateRating.Acceptable;
        if (comfort >= 0.20f) return BistroBuilderOutdoorClimateRating.Poor;
        return BistroBuilderOutdoorClimateRating.Unusable;
    }

    private static float SmoothHash(int seed, long token, int channel)
    {
        return Hash01(seed, token - 1L, channel) * 0.25f +
               Hash01(seed, token, channel) * 0.50f +
               Hash01(seed, token + 1L, channel) * 0.25f;
    }

    private static float Hash01(int seed, long token, int channel)
    {
        unchecked
        {
            uint x = (uint)seed;
            x ^= (uint)token * 0x9E3779B9u;
            x ^= (uint)(token >> 32) * 0x85EBCA6Bu;
            x ^= (uint)channel * 0xC2B2AE35u;
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return (x & 0x00FFFFFFu) / 16777215f;
        }
    }
}
