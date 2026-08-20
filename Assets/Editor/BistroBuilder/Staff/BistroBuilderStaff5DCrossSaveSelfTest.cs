using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 5D — Demuestra que un slot puede contener EmployeeId distintos de la
/// partida actualmente abierta: prevalidación estructural primero y cruce con
/// staff.state objetivo únicamente durante Apply.
/// </summary>
public static class BistroBuilderStaff5DCrossSaveSelfTest
{
    private const string ProviderPath =
        "Assets/Scripts/Application/Persistence/Staff/BistroBuilderStaffScheduleSaveSectionProvider.cs";

    [MenuItem("Tools/Bistro Builder/Personal/5D - Autotest carga cruzada horarios", false, 3277)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
        EditorUtility.DisplayDialog("Bistro Builder — 5D carga cruzada",
            passed + " OK / " + failed + " fallos", "Aceptar");
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder();
        log.AppendLine("=== BISTRO BUILDER — 5D CROSS-SAVE / AUTOTEST ===");

        string targetEmployeeId = BistroBuilderEmployeeIdUtility.CreateNew();
        var schedule = new BistroBuilderStaffScheduleSnapshot
        {
            revision = 1L,
            shifts = new List<BistroBuilderStaffShiftRecord>
            {
                new BistroBuilderStaffShiftRecord
                {
                    employeeId = targetEmployeeId,
                    dayIndex = 4,
                    mealService = BistroBuilderMealServiceAvailability.Lunch,
                    startMinute = 720,
                    endMinute = 960
                }
            }
        };
        BistroBuilderStaffSnapshot currentStaff = BistroBuilderStaffEngine.CreateEmptySnapshot();
        BistroBuilderStaffSnapshot targetStaff = BistroBuilderStaffEngine.CreateEmptySnapshot();
        targetStaff.employees.Add(new BistroBuilderEmployeeRecord
        {
            employeeId = targetEmployeeId,
            roleId = "waiter",
            employmentStatus = BistroBuilderEmploymentStatus.Active,
            availability = BistroBuilderEmployeeAvailability.Available,
            salaryCentsPerService = 8000L
        });

        Check(BistroBuilderStaffScheduleEngine.TryValidateStructure(schedule, out string structureError),
            "La prevalidación acepta estructura válida sin consultar la partida abierta. " + structureError,
            ref passed, ref failed, log);
        Check(!BistroBuilderStaffScheduleEngine.TryValidateSnapshot(schedule, currentStaff, out _),
            "El cruce rechazaría correctamente EmployeeId contra la plantilla actual distinta.",
            ref passed, ref failed, log);
        Check(BistroBuilderStaffScheduleEngine.TryValidateSnapshot(schedule, targetStaff, out string targetError),
            "El mismo horario es válido contra staff.state objetivo. " + targetError,
            ref passed, ref failed, log);

        string source = Read(ProviderPath);
        string validateState = Slice(source, "public bool ValidateState", "public IEnumerator PrepareForLoad");
        string apply = Slice(source, "public IEnumerator ApplyState", "public void FinalizeLoad");
        Check(validateState.Contains("TryValidateStructure") &&
              !validateState.Contains("staffService.CreateSnapshot()"),
            "ValidateState no cruza EmployeeId contra la partida abierta.",
            ref passed, ref failed, log);
        Check(apply.Contains("TryValidateSnapshot") &&
              apply.Contains("staffService.CreateSnapshot()") &&
              apply.Contains("TryRestoreSnapshot"),
            "Apply cruza EmployeeId después de staff.state y antes de restaurar ScheduleService.",
            ref passed, ref failed, log);

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static string Read(string path)
    {
        string full = Path.GetFullPath(path);
        return File.Exists(full) ? File.ReadAllText(full) : string.Empty;
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (end <= start) end = source.Length;
        return source.Substring(start, end - start);
    }

    private static void Check(bool condition, string text,
        ref int passed, ref int failed, StringBuilder log)
    {
        if (condition) { passed++; log.AppendLine("[OK] " + text); }
        else { failed++; log.AppendLine("[FALLO] " + text); }
    }
}
