using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5D — Gate de persistencia cruzada entre partidas.
/// Demuestra que la prevalidación de Load solo exige estructura y que el cruce
/// EmployeeId se realiza contra staff.state objetivo durante Apply.
/// </summary>
public static class BistroBuilderStaff5DCrossSaveValidationSelfTest
{
    [MenuItem("Tools/Bistro Builder/Personal/5D - Autotest Save cruzado horarios", false, 3278)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog("Bistro Builder — 5D Save cruzado",
            passed + " OK / " + failed + " fallos", "Aceptar");
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0; failed = 0;
        var log = new StringBuilder();
        log.AppendLine("=== BISTRO BUILDER — 5D / SAVE CRUZADO STAFF.SCHEDULE ===");
        try
        {
            string employeeA = BistroBuilderEmployeeIdUtility.CreateNew();
            string employeeB = BistroBuilderEmployeeIdUtility.CreateNew();
            BistroBuilderStaffSnapshot staffA = BuildStaff(employeeA);
            BistroBuilderStaffSnapshot staffB = BuildStaff(employeeB);
            var scheduleB = new BistroBuilderStaffScheduleSnapshot
            {
                schemaId = BistroBuilderStaffScheduleSnapshot.CurrentSchemaId,
                schemaVersion = BistroBuilderStaffScheduleSnapshot.CurrentSchemaVersion,
                revision = 4L,
                shifts = new List<BistroBuilderStaffShiftRecord>
                {
                    new BistroBuilderStaffShiftRecord
                    {
                        employeeId = employeeB, dayIndex = 12,
                        mealService = BistroBuilderMealServiceAvailability.Lunch,
                        startMinute = 660, endMinute = 900
                    }
                }
            };
            Check(BistroBuilderStaffScheduleEngine.TryValidateStructure(scheduleB, out string structureError),
                "Prevalidación estructural independiente de la partida abierta. " + structureError,
                ref passed, ref failed, log);
            Check(!BistroBuilderStaffScheduleEngine.TryValidateSnapshot(scheduleB, staffA, out _),
                "El horario B no se cruza contra staff.state A.", ref passed, ref failed, log);
            Check(BistroBuilderStaffScheduleEngine.TryValidateSnapshot(scheduleB, staffB, out string targetError),
                "El horario B cruza con staff.state B tras Apply. " + targetError,
                ref passed, ref failed, log);
            BistroBuilderStaffScheduleSnapshot duplicated = scheduleB.DeepClone();
            duplicated.shifts.Add(duplicated.shifts[0].DeepClone());
            Check(!BistroBuilderStaffScheduleEngine.TryValidateStructure(duplicated, out _),
                "Los duplicados estructurales siguen rechazándose.", ref passed, ref failed, log);
            BistroBuilderStaffScheduleSnapshot broken = scheduleB.DeepClone();
            broken.shifts[0].employeeId = string.Empty;
            Check(!BistroBuilderStaffScheduleEngine.TryValidateStructure(broken, out _),
                "EmployeeId estructuralmente inválido sigue rechazándose.", ref passed, ref failed, log);
        }
        catch (Exception exception)
        {
            failed++; log.AppendLine("[FALLO] Excepción inesperada: " + exception);
        }
        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static BistroBuilderStaffSnapshot BuildStaff(string employeeId)
    {
        BistroBuilderStaffSnapshot staff = BistroBuilderStaffEngine.CreateEmptySnapshot();
        staff.employees.Add(new BistroBuilderEmployeeRecord
        {
            employeeId = employeeId,
            roleId = "waiter",
            employmentStatus = BistroBuilderEmploymentStatus.Active,
            availability = BistroBuilderEmployeeAvailability.Available,
            salaryCentsPerService = 8000L
        });
        return staff;
    }

    private static void Check(bool condition, string text, ref int passed, ref int failed, StringBuilder log)
    {
        if (condition) { passed++; log.AppendLine("[OK] " + text); }
        else { failed++; log.AppendLine("[FALLO] " + text); }
    }
}
