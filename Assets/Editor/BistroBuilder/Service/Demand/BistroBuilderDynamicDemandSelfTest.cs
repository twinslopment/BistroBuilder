using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest puro del motor universal de demanda base dinámica.
/// </summary>
public static class BistroBuilderDynamicDemandSelfTest
{
    [MenuItem("Tools/Bistro Builder/Service/Demanda dinámica - Autotest", false, 8301)]
    private static void RunFromMenu()
    {
        if (!Run(out _, out _, out string report)) Debug.LogError(report);
        else Debug.Log(report);
    }

    public static void RunFromCommandLine()
    {
        if (!Run(out _, out _, out string report))
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0; failed = 0;
        var lines = new List<string>();
        var settings = new BistroBuilderDynamicDemandSettings();
        BistroBuilderDynamicDemandContext neutral = Context();

        Check(settings.TryValidate(out _),
            "La configuración de balance por defecto es válida.",
            ref passed, ref failed, lines);
        Check(BistroBuilderDynamicDemandEngine.TryEvaluate(
                settings, neutral, out BistroBuilderDynamicDemandProjection baseline, out _) &&
              baseline.baseWalkInGroups >= 1 &&
              baseline.arrivalDelaySeconds.Count == baseline.baseWalkInGroups,
            "El contexto neutral produce demanda y curva completas.",
            ref passed, ref failed, lines);

        var progressed = Context(); progressed.progressionLevel = 8;
        Check(Evaluate(settings, progressed).baseWalkInGroups > baseline.baseWalkInGroups,
            "La progresión aumenta el interés orgánico.", ref passed, ref failed, lines);

        var poorRep = Context(); poorRep.globalReputationBasisPoints = 1000;
        var greatRep = Context(); greatRep.globalReputationBasisPoints = 9000;
        Check(Evaluate(settings, greatRep).baseWalkInGroups >
              Evaluate(settings, poorRep).baseWalkInGroups,
            "La reputación modifica la demanda base.", ref passed, ref failed, lines);

        var poorSat = Context(); poorSat.recentSatisfactionBasisPoints = 1000;
        var greatSat = Context(); greatSat.recentSatisfactionBasisPoints = 9000;
        Check(Evaluate(settings, greatSat).baseWalkInGroups >
              Evaluate(settings, poorSat).baseWalkInGroups,
            "La satisfacción reciente modifica el interés futuro.",
            ref passed, ref failed, lines);

        var monday = Context(); monday.dayOfWeek = DayOfWeek.Monday;
        var saturday = Context(); saturday.dayOfWeek = DayOfWeek.Saturday;
        Check(Evaluate(settings, saturday).baseWalkInGroups >
              Evaluate(settings, monday).baseWalkInGroups,
            "El calendario diferencia días valle y días fuertes.",
            ref passed, ref failed, lines);

        var afternoon = Context(); afternoon.minuteOfDay = 17 * 60;
        var dinner = Context(); dinner.minuteOfDay = 20 * 60;
        Check(Evaluate(settings, dinner).baseWalkInGroups >
              Evaluate(settings, afternoon).baseWalkInGroups,
            "La franja horaria crea picos y valles de demanda.",
            ref passed, ref failed, lines);

        var poorMenu = Context(); poorMenu.availableDishCount = 1;
        var strongMenu = Context(); strongMenu.availableDishCount = 10;
        Check(Evaluate(settings, strongMenu).baseWalkInGroups >
              Evaluate(settings, poorMenu).baseWalkInGroups,
            "Una carta operativa más sólida mejora la demanda base.",
            ref passed, ref failed, lines);

        var small = Context(); small.tableSeatCapacity = 4; small.barSeatCapacity = 0;
        var large = Context(); large.tableSeatCapacity = 28; large.barSeatCapacity = 4;
        var smallP = Evaluate(settings, small); var largeP = Evaluate(settings, large);
        Check(largeP.baseWalkInGroups > smallP.baseWalkInGroups &&
              smallP.baseWalkInGroups <= smallP.capacityCeilingGroups,
            "La capacidad influye y limita sin convertirse en demanda infinita.",
            ref passed, ref failed, lines);

        var reserved = Context(); reserved.reservationGroupCount = 5; reserved.reservedPartySize = 12;
        var noReserved = Context();
        Check(Evaluate(settings, reserved).capacityCeilingGroups <
              Evaluate(settings, noReserved).capacityCeilingGroups,
            "Las reservas protegen capacidad frente a walk-ins.",
            ref passed, ref failed, lines);

        var extremeCapacity = Context(); extremeCapacity.tableSeatCapacity = 80;
        extremeCapacity.globalReputationBasisPoints = 500; extremeCapacity.recentSatisfactionBasisPoints = 500;
        extremeCapacity.availableDishCount = 1; extremeCapacity.dayOfWeek = DayOfWeek.Monday;
        extremeCapacity.minuteOfDay = 17 * 60;
        Check(Evaluate(settings, extremeCapacity).baseWalkInGroups < 20,
            "Tener muchas mesas no llena por sí solo un restaurante poco atractivo.",
            ref passed, ref failed, lines);

        BistroBuilderDynamicDemandProjection curve = Evaluate(settings, dinner);
        bool varied = false;
        for (int i = 2; i < curve.arrivalDelaySeconds.Count; i++)
            if (Math.Abs(curve.arrivalDelaySeconds[i] - curve.arrivalDelaySeconds[1]) > 0.05f)
            { varied = true; break; }
        Check(curve.arrivalDelaySeconds.Count == curve.baseWalkInGroups &&
              (curve.baseWalkInGroups < 3 || varied),
            "Las llegadas usan una curva variable, no un intervalo uniforme.",
            ref passed, ref failed, lines);

        var invalid = Context(); invalid.tableSeatCapacity = 0; invalid.barSeatCapacity = 0;
        Check(!BistroBuilderDynamicDemandEngine.TryEvaluate(
                settings, invalid, out _, out _),
            "Se rechaza un contexto sin capacidad operativa.",
            ref passed, ref failed, lines);

        var plan = new BistroBuilderCustomerDemandPlan
        {
            planId = "dynamic.test",
            walkInGroupCount = 2,
            profiles = new List<BistroBuilderCustomerAcquisitionProfile>
            {
                BistroBuilderCustomerAcquisitionProfile.CreateBaseline(),
                BistroBuilderCustomerAcquisitionProfile.CreateBaseline()
            },
            arrivalDelaySeconds = new List<float> { 1f, 3.5f }
        };
        Check(plan.TryValidate(out _) &&
              plan.DeepClone().arrivalDelaySeconds.Count == 2,
            "El plan persistible conserva una cadencia por llegada.",
            ref passed, ref failed, lines);

        var saveArrival = new BistroBuilderCustomerArrivalPlanSaveRecord
        {
            groupSize = 2,
            serviceMode = (int)BistroBuilderServiceMode.TableService,
            delayBeforeArrivalSeconds = 3.25f,
            acquisition = BistroBuilderCustomerAcquisitionProfile.CreateBaseline()
        };
        Check(saveArrival.TryValidate(out _),
            "service.runtime admite y valida la cadencia dinámica pendiente.",
            ref passed, ref failed, lines);

        report = "=== BISTRO BUILDER — DEMANDA BASE DINÁMICA AUTOTEST ===\n" +
                 string.Join("\n", lines) + "\nResultado: " + passed +
                 " OK / " + failed + " fallos.";
        return failed == 0;
    }

    private static BistroBuilderDynamicDemandProjection Evaluate(
        BistroBuilderDynamicDemandSettings settings,
        BistroBuilderDynamicDemandContext context)
    {
        if (!BistroBuilderDynamicDemandEngine.TryEvaluate(
                settings, context, out BistroBuilderDynamicDemandProjection value, out string error))
            throw new InvalidOperationException(error);
        return value;
    }

    private static BistroBuilderDynamicDemandContext Context() =>
        new BistroBuilderDynamicDemandContext
        {
            progressionLevel = 1,
            tableCount = 8,
            tableSeatCapacity = 20,
            barSeatCapacity = 4,
            globalReputationBasisPoints = 5000,
            recentSatisfactionBasisPoints = 5000,
            dayOfWeek = DayOfWeek.Thursday,
            minuteOfDay = 13 * 60,
            availableDishCount = 8,
            reservationGroupCount = 0,
            reservedPartySize = 0
        };

    private static void Check(bool condition, string message,
        ref int passed, ref int failed, List<string> lines)
    {
        if (condition) { passed++; lines.Add("[OK] " + message); }
        else { failed++; lines.Add("[FAIL] " + message); }
    }
}
