using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5A — Autotest puro de la fundación de horarios y turnos.
/// </summary>
public static class BistroBuilderStaff5AFoundationSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/5A - Autotest fundación horarios",
        false,
        3270)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 5A Horarios",
            passed + " OK / " + failed + " fallos",
            "Aceptar");
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder();
        log.AppendLine("=== BISTRO BUILDER — 5A HORARIOS / AUTOTEST ===");

        BistroBuilderStaffScheduleProfile profile = null;
        try
        {
            profile = ScriptableObject.CreateInstance<BistroBuilderStaffScheduleProfile>();
            Check(profile.TryValidate(out string profileError),
                "Perfil V1 válido. " + profileError,
                ref passed, ref failed, log);

            string employeeId = BistroBuilderEmployeeIdUtility.CreateNew();
            var staff = BistroBuilderStaffEngine.CreateEmptySnapshot();
            staff.employees.Add(new BistroBuilderEmployeeRecord
            {
                employeeId = employeeId,
                roleId = "waiter",
                employmentStatus = BistroBuilderEmploymentStatus.Active,
                availability = BistroBuilderEmployeeAvailability.Available,
                salaryCentsPerService = 8000L
            });

            BistroBuilderStaffScheduleSnapshot empty =
                BistroBuilderStaffScheduleEngine.CreateEmptySnapshot();
            Check(BistroBuilderStaffScheduleEngine.TryValidateSnapshot(
                    empty,
                    staff,
                    out string emptyError),
                "Snapshot vacío válido. " + emptyError,
                ref passed, ref failed, log);

            Check(BistroBuilderStaffScheduleEngine.TrySetShift(
                    empty,
                    staff,
                    profile,
                    employeeId,
                    3,
                    BistroBuilderMealServiceAvailability.Lunch,
                    true,
                    out BistroBuilderStaffScheduleSnapshot lunch,
                    out string setError),
                "Asignación de turno transaccional. " + setError,
                ref passed, ref failed, log);

            Check(lunch != null && lunch.revision == 1L && lunch.shifts.Count == 1 &&
                  BistroBuilderStaffScheduleEngine.IsScheduled(
                      lunch,
                      employeeId,
                      3,
                      BistroBuilderMealServiceAvailability.Lunch),
                "El turno queda consultable por EmployeeId/día/servicio.",
                ref passed, ref failed, log);

            BistroBuilderStaffScheduleSnapshot clone = lunch.DeepClone();
            clone.shifts[0].dayIndex = 99;
            Check(lunch.shifts[0].dayIndex == 3,
                "DeepClone no comparte registros de turno.",
                ref passed, ref failed, log);

            BistroBuilderStaffScheduleSnapshot duplicate = lunch.DeepClone();
            duplicate.shifts.Add(lunch.shifts[0].DeepClone());
            Check(!BistroBuilderStaffScheduleEngine.TryValidateSnapshot(
                    duplicate,
                    staff,
                    out _),
                "Se rechaza doble turno del mismo empleado/servicio.",
                ref passed, ref failed, log);

            Check(BistroBuilderStaffScheduleEngine.TrySetShift(
                    lunch,
                    staff,
                    profile,
                    employeeId,
                    3,
                    BistroBuilderMealServiceAvailability.Lunch,
                    false,
                    out BistroBuilderStaffScheduleSnapshot removed,
                    out string removeError) &&
                  removed.shifts.Count == 0 && removed.revision == 2L,
                "Desasignación conserva revisión y consistencia. " + removeError,
                ref passed, ref failed, log);

            var ids = new List<string>();
            BistroBuilderStaffScheduleEngine.CopyScheduledEmployeeIds(
                lunch,
                3,
                BistroBuilderMealServiceAvailability.Lunch,
                ids);
            Check(ids.Count == 1 && string.Equals(ids[0], employeeId, StringComparison.Ordinal),
                "Consulta de empleados programados es determinista.",
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

    private static void Check(
        bool condition,
        string text,
        ref int passed,
        ref int failed,
        StringBuilder log)
    {
        if (condition)
        {
            passed++;
            log.AppendLine("[OK] " + text);
        }
        else
        {
            failed++;
            log.AppendLine("[FALLO] " + text);
        }
    }
}
