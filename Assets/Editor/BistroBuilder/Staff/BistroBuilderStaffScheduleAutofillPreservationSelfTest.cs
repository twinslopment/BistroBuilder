using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Static regression fence for non-destructive Schedule V1 auto-coverage.
/// It is NOT a runtime substitute for the real 5F Save/Load Queen Test.
/// </summary>
public static class BistroBuilderStaffScheduleAutofillPreservationSelfTest
{
    [MenuItem("Tools/Bistro Builder/Personal/V1 - Verificar conservación turnos", false, 3258)]
    public static void Run()
    {
        const string plannerPath =
            "Assets/Scripts/Domain/Staff/Scheduling/BistroBuilderStaffSchedulePlanner.cs";
        const string uiPath =
            "Assets/Scripts/Presentation/Staff/Scheduling/BistroBuilderStaffSchedulePlayerFacade.cs";
        if (!File.Exists(plannerPath) || !File.Exists(uiPath))
        {
            Debug.LogError("[TURNOS V1] FAIL: falta Planner o Facade canónicos.");
            return;
        }

        string source = File.ReadAllText(plannerPath);
        string ui = File.ReadAllText(uiPath);
        int passed = 0, failed = 0;
        void Check(bool condition, string label)
        {
            if (condition) passed++;
            else { failed++; Debug.LogError("[TURNOS V1] FAIL: " + label); }
        }

        int fillStart = source.IndexOf(
            "public static bool TryBuildMinimumWaiterPlan(", StringComparison.Ordinal);
        string fill = fillStart >= 0 ? source.Substring(fillStart) : string.Empty;
        Check(fill.Contains("CopyScheduledEmployeeIds(") &&
              fill.Contains("current, dayIndex, mealService, planned"),
            "Cobertura mínima empieza por el turno existente, incluidos cocineros.");
        Check(fill.Contains("if (!scheduled.Add(id)) continue;") &&
              fill.Contains("BistroBuilderStaffScheduleEngine.TrySetShift(") &&
              fill.Contains("id, dayIndex, mealService, true,"),
            "Solo agrega camareros faltantes con TrySetShift, sin repetir EmployeeId.");
        Check(fill.Contains("waiterCount >= profile.MinimumRecommendedWaiters") &&
              fill.Contains("result = current.DeepClone();"),
            "No incrementa revisión ni sustituye turnos si Sala ya está cubierta.");
        Check(!fill.Contains("return TryReplaceServiceAssignments(") &&
              fill.Contains("updated = current.DeepClone();"),
            "Auto-fill conserva exactamente ventanas y asignaciones preexistentes.");
        Check(fill.Contains("if (candidates.Count == 0)"),
            "Cocineros solos no se computan como cobertura de Sala.");
        Check(ui.Contains("projectedCookSalaryCents += salary") &&
              ui.Contains("projectedTotalSalaryCents += salary") &&
              ui.Contains("scheduledCooks++"),
            "El resumen de Horarios incluye los sueldos y turnos de Cocina.");
        string status = "[TURNOS V1] STATIC " + passed + " PASS / " +
            failed + " FAIL; Play Mode/Save Load PENDING.";
        if (failed == 0) Debug.Log(status);
        else Debug.LogError(status);
    }
}
