using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5D — Round-trip de staff.schedule con el serializador real unity-json-v1.
/// </summary>
public static class BistroBuilderStaff5DJsonRoundTripSelfTest
{
    [MenuItem("Tools/Bistro Builder/Personal/5D - Autotest JSON horarios", false, 3276)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog("Bistro Builder — 5D", passed + " OK / " + failed + " fallos", "Aceptar");
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder();
        log.AppendLine("=== BISTRO BUILDER — 5D STAFF.SCHEDULE / JSON ===");
        try
        {
            string employeeId = BistroBuilderEmployeeIdUtility.CreateNew();
            var staff = BistroBuilderStaffEngine.CreateEmptySnapshot();
            staff.employees.Add(new BistroBuilderEmployeeRecord
            {
                employeeId = employeeId,
                roleId = "waiter",
                employmentStatus = BistroBuilderEmploymentStatus.Active,
                availability = BistroBuilderEmployeeAvailability.Available,
                salaryCentsPerService = 9100L
            });

            var schedule = new BistroBuilderStaffScheduleSnapshot
            {
                schemaId = BistroBuilderStaffScheduleSnapshot.CurrentSchemaId,
                schemaVersion = BistroBuilderStaffScheduleSnapshot.CurrentSchemaVersion,
                revision = 7L,
                shifts = new List<BistroBuilderStaffShiftRecord>
                {
                    new BistroBuilderStaffShiftRecord
                    {
                        employeeId = employeeId,
                        dayIndex = 12,
                        mealService = BistroBuilderMealServiceAvailability.Dinner,
                        startMinute = 1140,
                        endMinute = 1380
                    }
                }
            };

            Check(BistroBuilderStaffScheduleEngine.TryValidateSnapshot(
                    schedule, staff, out string beforeError),
                "Snapshot válido antes de JSON. " + beforeError,
                ref passed, ref failed, log);

            var serializer = new BistroBuilderJsonSaveSerializer();
            Check(serializer.SerializerId == BistroBuilderJsonSaveSerializer.StableSerializerId,
                "Se usa unity-json-v1.", ref passed, ref failed, log);

            byte[] bytes = serializer.Serialize(schedule, false);
            var restored = (BistroBuilderStaffScheduleSnapshot)serializer.Deserialize(
                bytes, typeof(BistroBuilderStaffScheduleSnapshot));

            Check(BistroBuilderStaffScheduleEngine.TryValidateSnapshot(
                    restored, staff, out string restoredError),
                "Snapshot válido después de JSON. " + restoredError,
                ref passed, ref failed, log);
            Check(restored.revision == 7L && restored.shifts.Count == 1 &&
                  restored.shifts[0].employeeId == employeeId &&
                  restored.shifts[0].dayIndex == 12 &&
                  restored.shifts[0].mealService == BistroBuilderMealServiceAvailability.Dinner &&
                  restored.shifts[0].startMinute == 1140 &&
                  restored.shifts[0].endMinute == 1380,
                "JSON conserva EmployeeId, día, servicio y ventana horaria.",
                ref passed, ref failed, log);

            Check(typeof(IBistroBuilderSaveSectionProvider).IsAssignableFrom(
                    typeof(BistroBuilderStaffScheduleSaveSectionProvider)) &&
                  typeof(IBistroBuilderSaveSectionPhaseOrdering).IsAssignableFrom(
                    typeof(BistroBuilderStaffScheduleSaveSectionProvider)),
                "staff.schedule extiende SaveGame universal con orden explícito.",
                ref passed, ref failed, log);
        }
        catch (Exception exception)
        {
            failed++;
            log.AppendLine("[FALLO] Excepción inesperada: " + exception);
        }

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static void Check(bool condition, string text, ref int passed, ref int failed, StringBuilder log)
    {
        if (condition) { passed++; log.AppendLine("[OK] " + text); }
        else { failed++; log.AppendLine("[FALLO] " + text); }
    }
}
