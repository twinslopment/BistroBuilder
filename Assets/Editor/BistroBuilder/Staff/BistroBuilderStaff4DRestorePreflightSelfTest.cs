using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest puro del preflight de restauración 4D.
/// No guarda escenas ni crea una autoridad alternativa de Waiter.
/// </summary>
public static class BistroBuilderStaff4DRestorePreflightSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Autotest preflight restore",
        false,
        3234)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D restore preflight",
            passed + " OK / " + failed + " fallos",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("El autotest del preflight de restore 4D ha fallado.");
        }
    }

    public static bool Run(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var log = new StringBuilder();
        log.AppendLine("=== BISTRO BUILDER — 4D RESTORE PREFLIGHT ===");

        GameObject waiterObject = null;
        GameObject duplicateObject = null;
        try
        {
            waiterObject = new GameObject("4D_RestorePreflight_Waiter");
            Waiter waiter = waiterObject.AddComponent<Waiter>();

            BistroBuilderStaffSessionSnapshot active = BuildSnapshot(waiter.WaiterId);
            Check(
                BistroBuilderStaffSessionRestorePreflight.TryValidate(
                    active,
                    out string validError),
                "Acepta un binding cuyo WaiterId existe. " + validError,
                ref passed,
                ref failed,
                log);

            BistroBuilderStaffSessionSnapshot missing = BuildSnapshot(999999);
            Check(
                !BistroBuilderStaffSessionRestorePreflight.TryValidate(
                    missing,
                    out string missingError) &&
                !string.IsNullOrWhiteSpace(missingError),
                "Rechaza WaiterId inexistente antes de cualquier mutación.",
                ref passed,
                ref failed,
                log);

            BistroBuilderStaffSessionSnapshot inactive =
                BistroBuilderStaffSessionEngine.CreateInactiveSnapshot();
            Check(
                BistroBuilderStaffSessionRestorePreflight.TryValidate(
                    inactive,
                    out string inactiveError),
                "Una sesión inactiva no exige bindings. " + inactiveError,
                ref passed,
                ref failed,
                log);

            duplicateObject = new GameObject("4D_RestorePreflight_Duplicate");
            duplicateObject.AddComponent<Waiter>();
            Check(
                !BistroBuilderStaffSessionRestorePreflight.TryValidate(
                    active,
                    out string duplicateError) &&
                !string.IsNullOrWhiteSpace(duplicateError),
                "La escena con WaiterId duplicados se rechaza.",
                ref passed,
                ref failed,
                log);
        }
        catch (Exception exception)
        {
            failed++;
            log.AppendLine("[FALLO] Excepción inesperada: " + exception);
        }
        finally
        {
            if (duplicateObject != null)
            {
                UnityEngine.Object.DestroyImmediate(duplicateObject);
            }
            if (waiterObject != null)
            {
                UnityEngine.Object.DestroyImmediate(waiterObject);
            }
        }

        log.AppendLine("Resultado: " + passed + " OK / " + failed + " fallos");
        report = log.ToString();
        return failed == 0;
    }

    private static BistroBuilderStaffSessionSnapshot BuildSnapshot(int waiterId)
    {
        return new BistroBuilderStaffSessionSnapshot
        {
            schemaId = BistroBuilderStaffSessionSnapshot.CurrentSchemaId,
            schemaVersion = BistroBuilderStaffSessionSnapshot.CurrentSchemaVersion,
            revision = 1L,
            active = true,
            sessionId = BistroBuilderStaffSessionIdUtility.CreateNew(),
            dayIndex = 1,
            bindings = new List<BistroBuilderStaffSessionBindingRecord>
            {
                new BistroBuilderStaffSessionBindingRecord
                {
                    employeeId = BistroBuilderEmployeeIdUtility.CreateNew(),
                    waiterId = waiterId,
                    handledTableIds = new List<int>()
                }
            }
        };
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
            return;
        }

        failed++;
        log.AppendLine("[FALLO] " + text);
    }
}
