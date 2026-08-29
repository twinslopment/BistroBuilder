using System;
using System.Collections.Generic;

/// <summary>
/// Motor puro de demanda orgánica. Convierte progresión, capacidad, reputación,
/// satisfacción, calendario, franja, carta y reservas en un número de walk-ins
/// y una curva de llegadas. No crea clientes ni conoce Marketing.
/// </summary>
public static class BistroBuilderDynamicDemandEngine
{
    public static bool TryEvaluate(
        BistroBuilderDynamicDemandSettings settings,
        BistroBuilderDynamicDemandContext context,
        out BistroBuilderDynamicDemandProjection projection,
        out string error)
    {
        projection = null;
        error = string.Empty;
        if (settings == null || !settings.TryValidate(out error)) return false;
        if (!TryValidateContext(context, out error)) return false;

        int progression = 10000 + Math.Min(
            settings.maximumProgressionBonusBasisPoints,
            Math.Max(0, context.progressionLevel - 1) *
            settings.progressionBasisPointsPerLevel);

        double capacityRatio = context.TotalSeatCapacity /
                               Math.Max(1.0, settings.referenceCapacitySeats);
        int capacity = ToBasisPoints(Math.Max(0.70,
            Math.Min(1.45, Math.Sqrt(capacityRatio))));

        int reputation = 7000 +
            Clamp(context.globalReputationBasisPoints, 0, 10000) * 6000 / 10000;
        int satisfaction = 8500 +
            Clamp(context.recentSatisfactionBasisPoints, 0, 10000) * 3000 / 10000;
        int calendar = ResolveCalendarMultiplier(context.dayOfWeek);
        string dayPart = ResolveDayPartId(context.minuteOfDay);
        int dayPartMultiplier = ResolveDayPartMultiplier(dayPart);
        int menu = ResolveMenuMultiplier(context.availableDishCount);

        double expected = settings.neutralBaseGroups;
        expected = Apply(expected, progression);
        expected = Apply(expected, capacity);
        expected = Apply(expected, reputation);
        expected = Apply(expected, satisfaction);
        expected = Apply(expected, calendar);
        expected = Apply(expected, dayPartMultiplier);
        expected = Apply(expected, menu);

        int unconstrained = Math.Max(1, (int)Math.Round(
            expected, MidpointRounding.AwayFromZero));
        int protectedSeats = (int)Math.Round(
            Math.Max(0, context.reservedPartySize) *
            settings.reservationCapacityProtection,
            MidpointRounding.AwayFromZero);
        int effectiveSeats = Math.Max(1, context.TotalSeatCapacity - protectedSeats);
        int capacityCeiling = Math.Max(1, (int)Math.Floor(
            effectiveSeats * settings.serviceSeatTurns /
            Math.Max(0.5, settings.averagePartySize)));

        int finalGroups = Math.Min(unconstrained, capacityCeiling);
        finalGroups = Math.Min(settings.maximumBaseGroups,
            Math.Max(1, finalGroups));
        if (capacityCeiling >= settings.minimumBaseGroups)
            finalGroups = Math.Max(settings.minimumBaseGroups, finalGroups);

        projection = new BistroBuilderDynamicDemandProjection
        {
            baseWalkInGroups = finalGroups,
            unconstrainedGroups = unconstrained,
            capacityCeilingGroups = capacityCeiling,
            effectiveAvailableSeats = effectiveSeats,
            progressionMultiplierBasisPoints = progression,
            capacityMultiplierBasisPoints = capacity,
            reputationMultiplierBasisPoints = reputation,
            satisfactionMultiplierBasisPoints = satisfaction,
            calendarMultiplierBasisPoints = calendar,
            dayPartMultiplierBasisPoints = dayPartMultiplier,
            menuMultiplierBasisPoints = menu,
            reservationGroupCount = Math.Max(0, context.reservationGroupCount),
            reservedPartySize = Math.Max(0, context.reservedPartySize),
            dayPartId = dayPart
        };
        BuildArrivalDelays(settings, context.minuteOfDay,
            finalGroups, projection.arrivalDelaySeconds);
        error = string.Empty;
        return true;
    }

    public static void BuildArrivalDelays(
        BistroBuilderDynamicDemandSettings settings,
        int minuteOfDay,
        int groupCount,
        List<float> destination)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        destination.Clear();
        if (settings == null || groupCount < 1) return;

        string part = ResolveDayPartId(minuteOfDay);
        ResolveCurve(part, out double peak, out double width, out double strength);
        for (int index = 0; index < groupCount; index++)
        {
            if (index == 0)
            {
                destination.Add(Math.Max(0.25f,
                    Math.Min(2f, settings.minimumArrivalSpacingSeconds)));
                continue;
            }

            double t = groupCount <= 1 ? 0.5 : index / (double)(groupCount - 1);
            double distance = (t - peak) / width;
            double intensity = 0.72 + strength * Math.Exp(-0.5 * distance * distance);
            double spacing = settings.baseArrivalSpacingSeconds / Math.Max(0.35, intensity);
            destination.Add((float)Math.Max(
                settings.minimumArrivalSpacingSeconds,
                Math.Min(settings.maximumArrivalSpacingSeconds, spacing)));
        }
    }

    public static string ResolveDayPartId(int minuteOfDay)
    {
        int minute = Clamp(minuteOfDay, 0, 1439);
        if (minute >= 360 && minute < 660) return "breakfast";
        if (minute >= 660 && minute < 960) return "lunch";
        if (minute >= 960 && minute < 1140) return "afternoon";
        if (minute >= 1140 && minute < 1380) return "dinner";
        return "late_night";
    }

    private static bool TryValidateContext(
        BistroBuilderDynamicDemandContext context,
        out string error)
    {
        if (context == null || context.progressionLevel < 1 ||
            context.tableCount < 0 || context.tableSeatCapacity < 0 ||
            context.barSeatCapacity < 0 || context.TotalSeatCapacity < 1 ||
            context.globalReputationBasisPoints < 0 ||
            context.globalReputationBasisPoints > 10000 ||
            context.recentSatisfactionBasisPoints < 0 ||
            context.recentSatisfactionBasisPoints > 10000 ||
            context.minuteOfDay < 0 || context.minuteOfDay > 1439 ||
            context.availableDishCount < 0 || context.reservationGroupCount < 0 ||
            context.reservedPartySize < 0)
        {
            error = "El contexto factual de demanda dinámica es inválido.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static int ResolveCalendarMultiplier(DayOfWeek day)
    {
        switch (day)
        {
            case DayOfWeek.Monday: return 8500;
            case DayOfWeek.Tuesday: return 9000;
            case DayOfWeek.Wednesday: return 9500;
            case DayOfWeek.Thursday: return 10000;
            case DayOfWeek.Friday: return 12000;
            case DayOfWeek.Saturday: return 13000;
            case DayOfWeek.Sunday: return 10500;
            default: return 10000;
        }
    }

    private static int ResolveDayPartMultiplier(string part)
    {
        switch (part)
        {
            case "breakfast": return 7800;
            case "lunch": return 11800;
            case "afternoon": return 7200;
            case "dinner": return 13200;
            default: return 6000;
        }
    }

    private static int ResolveMenuMultiplier(int dishes)
    {
        if (dishes <= 0) return 6500;
        if (dishes <= 3) return 8500;
        if (dishes <= 7) return 10000;
        if (dishes <= 12) return 11000;
        return 11500;
    }

    private static void ResolveCurve(
        string part,
        out double peak,
        out double width,
        out double strength)
    {
        peak = 0.5; width = 0.25; strength = 0.75;
        switch (part)
        {
            case "breakfast": peak = 0.38; width = 0.22; strength = 0.70; break;
            case "lunch": peak = 0.48; width = 0.20; strength = 1.05; break;
            case "afternoon": peak = 0.55; width = 0.32; strength = 0.45; break;
            case "dinner": peak = 0.58; width = 0.22; strength = 1.10; break;
            case "late_night": peak = 0.25; width = 0.28; strength = 0.40; break;
        }
    }

    private static double Apply(double value, int basisPoints) =>
        value * Math.Max(0, basisPoints) / 10000.0;

    private static int ToBasisPoints(double multiplier) =>
        (int)Math.Round(multiplier * 10000.0, MidpointRounding.AwayFromZero);

    private static int Clamp(int value, int minimum, int maximum) =>
        Math.Max(minimum, Math.Min(maximum, value));
}
