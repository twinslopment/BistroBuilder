using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5B — Autotest puro de operaciones compuestas de planificación.
/// </summary>
public static class BistroBuilderStaff5BPlanningSelfTest
{
    [MenuItem("Tools/Bistro Builder/Personal/5B - Autotest planificación", false, 3273)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog("Bistro Builder — 5B", passed + " OK / " + failed + " fallos", "Aceptar");
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder();
        log.AppendLine("=== BISTRO BUILDER — 5B PLANIFICACIÓN / AUTOTEST ===");

        BistroBuilderStaffScheduleProfile profile = null;
        try
        {
            profile = ScriptableObject.CreateInstance<BistroBuilderStaffScheduleProfile>();
            string firstId = BistroBuilderEmployeeIdUtility.CreateNew();
            string secondId = BistroBuilderEmployeeIdUtility.CreateNew();
            var staff = BistroBuilderStaffEngine.CreateEmptySnapshot();
            staff.employees.Add(CreateEmployee(firstId, 7500L));
            staff.employees.Add(CreateEmployee(secondId, 8500L));
            BistroBuilderStaffScheduleSnapshot empty =
                BistroBuilderStaffScheduleEngine.CreateEmptySnapshot();

            var ids = new List<string> { firstId, secondId };
            Check(BistroBuilderStaffSchedulePlanner.TryReplaceServiceAssignments(
                    empty, staff, profile, 5,
                    BistroBuilderMealServiceAvailability.Lunch,
                    ids,
                    out BistroBuilderStaffScheduleSnapshot planned,
                    out string replaceError) && planned.shifts.Count == 2,
                "Sustitución atómica de plantilla del servicio. " + replaceError,
                ref passed, ref failed, log);

            Check(BistroBuilderStaffSchedulePlanner.TryCopyServicePlan(
                    planned, staff, profile, 5,
                    BistroBuilderMealServiceAvailability.Lunch,
                    6,
                    BistroBuilderMealServiceAvailability.Dinner,
                    out BistroBuilderStaffScheduleSnapshot copied,
                    out string copyError) && copied.shifts.Count == 4,
                "Copia de plan entre día/servicio. " + copyError,
                ref passed, ref failed, log);

            var duplicateIds = new List<string> { firstId, firstId };
            Check(!BistroBuilderStaffSchedulePlanner.TryReplaceServiceAssignments(
                    empty, staff, profile, 5,
                    BistroBuilderMealServiceAvailability.Lunch,
                    duplicateIds,
                    out _, out _),
                "Se rechazan EmployeeId duplicados en un reemplazo.",
                ref passed, ref failed, log);

            BistroBuilderStaffScheduleSnapshot beforeFailure = planned.DeepClone();
            var invalid = new List<string> { firstId, string.Empty };
            Check(!BistroBuilderStaffSchedulePlanner.TryReplaceServiceAssignments(
                    planned, staff, profile, 5,
                    BistroBuilderMealServiceAvailability.Lunch,
                    invalid,
                    out _, out _) &&
                  planned.shifts.Count == beforeFailure.shifts.Count &&
                  planned.revision == beforeFailure.revision,
                "Un EmployeeId vacío falla sin mutar el snapshot de entrada.",
                ref passed, ref failed, log);
        }
        catch (Exception exception)
        {
            failed++;
            log.AppendLine("[FALLO] Excepción inesperada: " + exception);
        }
        finally
        {
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
        }

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static BistroBuilderEmployeeRecord CreateEmployee(string id, long salary)
    {
        return new BistroBuilderEmployeeRecord
        {
            employeeId = id,
            roleId = "waiter",
            employmentStatus = BistroBuilderEmploymentStatus.Active,
            availability = BistroBuilderEmployeeAvailability.Available,
            salaryCentsPerService = salary
        };
    }

    private static void Check(bool condition, string text, ref int passed, ref int failed, StringBuilder log)
    {
        if (condition) { passed++; log.AppendLine("[OK] " + text); }
        else { failed++; log.AppendLine("[FALLO] " + text); }
    }
}
