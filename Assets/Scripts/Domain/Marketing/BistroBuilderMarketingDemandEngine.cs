using System;
using System.Collections.Generic;

/// <summary>
/// Proyección jugable de demanda de 7B. Convierte porcentajes continuos de
/// Marketing en grupos discretos sin crear clientes ni reservas por sí misma.
/// </summary>
public sealed class BistroBuilderMarketingDemandProjection
{
    public int baselineWalkInGroups;
    public int adjustedWalkInGroups;
    public int effectiveWalkInBasisPoints;
    public int reservationLeadCount;
    public List<BistroBuilderMarketingCustomerSegment> walkInSegments =
        new List<BistroBuilderMarketingCustomerSegment>();
    public List<BistroBuilderMarketingCustomerSegment> reservationSegments =
        new List<BistroBuilderMarketingCustomerSegment>();

    public BistroBuilderMarketingDemandProjection DeepClone()
    {
        return new BistroBuilderMarketingDemandProjection
        {
            baselineWalkInGroups = baselineWalkInGroups,
            adjustedWalkInGroups = adjustedWalkInGroups,
            effectiveWalkInBasisPoints = effectiveWalkInBasisPoints,
            reservationLeadCount = reservationLeadCount,
            walkInSegments = new List<BistroBuilderMarketingCustomerSegment>(
                walkInSegments),
            reservationSegments =
                new List<BistroBuilderMarketingCustomerSegment>(
                    reservationSegments)
        };
    }
}

public static class BistroBuilderMarketingDemandEngine
{
    private const int ReservationOpportunityPoolPerSegment = 5;
    private const int MaximumMarketingReservationLeadsPerDay = 3;

    private static readonly BistroBuilderMarketingCustomerSegment[]
        ConcreteSegments =
        {
            BistroBuilderMarketingCustomerSegment.LocalResidents,
            BistroBuilderMarketingCustomerSegment.Workers,
            BistroBuilderMarketingCustomerSegment.YoungAdults,
            BistroBuilderMarketingCustomerSegment.Groups,
            BistroBuilderMarketingCustomerSegment.Couples,
            BistroBuilderMarketingCustomerSegment.Foodies,
            BistroBuilderMarketingCustomerSegment.Traditional,
            BistroBuilderMarketingCustomerSegment.PriceSensitive,
            BistroBuilderMarketingCustomerSegment.Planners,
            BistroBuilderMarketingCustomerSegment.HighValue
        };

    // Mezcla orgánica inicial. Son pesos de proyección, no identidades de NPC.
    private static readonly int[] BaseWeights =
        { 20, 15, 12, 10, 10, 8, 8, 7, 5, 5 };

    public static IReadOnlyList<BistroBuilderMarketingCustomerSegment>
        Segments => ConcreteSegments;

    public static bool TryBuildProjection(
        int baselineWalkInGroups,
        BistroBuilderMarketingEffectSnapshot globalEffects,
        IReadOnlyDictionary<BistroBuilderMarketingCustomerSegment,
            BistroBuilderMarketingEffectSnapshot> segmentEffects,
        out BistroBuilderMarketingDemandProjection projection,
        out string error)
    {
        projection = null;
        if (baselineWalkInGroups < 1 || baselineWalkInGroups > 100 ||
            globalEffects == null || segmentEffects == null)
        {
            error = "La proyección de demanda recibió datos básicos inválidos.";
            return false;
        }

        var weights = new long[ConcreteSegments.Length];
        long weightedSpecificBasisPoints = 0L;
        for (int index = 0; index < ConcreteSegments.Length; index++)
        {
            BistroBuilderMarketingCustomerSegment segment =
                ConcreteSegments[index];
            if (!segmentEffects.TryGetValue(segment, out var effects) ||
                effects == null)
            {
                error = "Falta la proyección del segmento " + segment + ".";
                return false;
            }

            int specificBasisPoints =
                effects.overallDemandBasisPoints +
                effects.walkInDemandBasisPoints -
                globalEffects.overallDemandBasisPoints -
                globalEffects.walkInDemandBasisPoints;
            weightedSpecificBasisPoints +=
                (long)BaseWeights[index] * specificBasisPoints;

            int relativeMultiplier = Math.Max(
                1000,
                10000 + specificBasisPoints);
            weights[index] =
                (long)BaseWeights[index] * relativeMultiplier;
        }

        int weightedSpecific = (int)Math.Round(
            weightedSpecificBasisPoints / 100.0,
            MidpointRounding.AwayFromZero);
        int effectiveBasisPoints =
            globalEffects.overallDemandBasisPoints +
            globalEffects.walkInDemandBasisPoints +
            weightedSpecific;
        effectiveBasisPoints = Math.Max(-9000, Math.Min(50000,
            effectiveBasisPoints));

        double expectedGroups = baselineWalkInGroups *
            (10000.0 + effectiveBasisPoints) / 10000.0;
        int adjustedGroups = (int)Math.Round(
            expectedGroups,
            MidpointRounding.AwayFromZero);
        adjustedGroups = Math.Max(
            1,
            Math.Min(baselineWalkInGroups * 3, adjustedGroups));

        projection = new BistroBuilderMarketingDemandProjection
        {
            baselineWalkInGroups = baselineWalkInGroups,
            adjustedWalkInGroups = adjustedGroups,
            effectiveWalkInBasisPoints = effectiveBasisPoints
        };

        FillSmoothWeightedSegments(
            weights,
            adjustedGroups,
            projection.walkInSegments);
        FillReservationLeads(
            globalEffects,
            segmentEffects,
            projection.reservationSegments);
        projection.reservationLeadCount =
            projection.reservationSegments.Count;

        error = string.Empty;
        return true;
    }

    public static BistroBuilderMarketingDayPart ResolveDayPart(
        int minuteOfDay)
    {
        int minute = Math.Max(0, Math.Min(1439, minuteOfDay));
        if (minute >= 360 && minute < 660)
            return BistroBuilderMarketingDayPart.Breakfast;
        if (minute >= 660 && minute < 960)
            return BistroBuilderMarketingDayPart.Lunch;
        if (minute >= 960 && minute < 1140)
            return BistroBuilderMarketingDayPart.Afternoon;
        if (minute >= 1140 && minute < 1380)
            return BistroBuilderMarketingDayPart.Dinner;
        return BistroBuilderMarketingDayPart.LateNight;
    }

    private static void FillSmoothWeightedSegments(
        IReadOnlyList<long> weights,
        int count,
        List<BistroBuilderMarketingCustomerSegment> destination)
    {
        destination.Clear();
        var current = new long[ConcreteSegments.Length];
        long totalWeight = 0L;
        for (int index = 0; index < weights.Count; index++)
            totalWeight += Math.Max(1L, weights[index]);

        for (int slot = 0; slot < count; slot++)
        {
            int selected = 0;
            long best = long.MinValue;
            for (int index = 0; index < ConcreteSegments.Length; index++)
            {
                current[index] += Math.Max(1L, weights[index]);
                if (current[index] > best)
                {
                    best = current[index];
                    selected = index;
                }
            }

            current[selected] -= totalWeight;
            destination.Add(ConcreteSegments[selected]);
        }
    }

    private static void FillReservationLeads(
        BistroBuilderMarketingEffectSnapshot globalEffects,
        IReadOnlyDictionary<BistroBuilderMarketingCustomerSegment,
            BistroBuilderMarketingEffectSnapshot> segmentEffects,
        List<BistroBuilderMarketingCustomerSegment> destination)
    {
        destination.Clear();
        int globalReservationBasisPoints = Math.Max(
            0,
            globalEffects.reservationDemandBasisPoints);
        int globalLeads = ConvertBasisPointsToLeadCount(
            globalReservationBasisPoints);
        BistroBuilderMarketingCustomerSegment strongest =
            FindStrongestReservationSegment(segmentEffects);

        for (int index = 0; index < globalLeads; index++)
            destination.Add(strongest);

        for (int index = 0;
             index < ConcreteSegments.Length &&
             destination.Count < MaximumMarketingReservationLeadsPerDay;
             index++)
        {
            BistroBuilderMarketingCustomerSegment segment =
                ConcreteSegments[index];
            BistroBuilderMarketingEffectSnapshot effects =
                segmentEffects[segment];
            int specificBasisPoints = Math.Max(
                0,
                effects.reservationDemandBasisPoints -
                globalReservationBasisPoints);
            int leads = ConvertBasisPointsToLeadCount(specificBasisPoints);
            for (int lead = 0;
                 lead < leads &&
                 destination.Count < MaximumMarketingReservationLeadsPerDay;
                 lead++)
                destination.Add(segment);
        }

        if (destination.Count > MaximumMarketingReservationLeadsPerDay)
            destination.RemoveRange(
                MaximumMarketingReservationLeadsPerDay,
                destination.Count - MaximumMarketingReservationLeadsPerDay);
    }

    private static int ConvertBasisPointsToLeadCount(int basisPoints)
    {
        if (basisPoints <= 0)
            return 0;

        double expected = ReservationOpportunityPoolPerSegment *
            basisPoints / 10000.0;
        return Math.Max(
            0,
            (int)Math.Round(expected, MidpointRounding.AwayFromZero));
    }

    private static BistroBuilderMarketingCustomerSegment
        FindStrongestReservationSegment(
            IReadOnlyDictionary<BistroBuilderMarketingCustomerSegment,
                BistroBuilderMarketingEffectSnapshot> segmentEffects)
    {
        BistroBuilderMarketingCustomerSegment selected =
            BistroBuilderMarketingCustomerSegment.Planners;
        int best = int.MinValue;
        for (int index = 0; index < ConcreteSegments.Length; index++)
        {
            BistroBuilderMarketingCustomerSegment segment =
                ConcreteSegments[index];
            int value = segmentEffects[segment].reservationDemandBasisPoints;
            if (value > best)
            {
                best = value;
                selected = segment;
            }
        }
        return selected;
    }
}
