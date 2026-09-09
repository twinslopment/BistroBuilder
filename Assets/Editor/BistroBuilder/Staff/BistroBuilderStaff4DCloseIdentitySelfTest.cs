using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4D — Regresión del preflight de cierre para identidad EmployeeId.
///
/// Demuestra que la consolidación de rendimiento rechaza EmployeeId inválidos
/// o duplicados antes de aplicar resultados de desarrollo.
/// </summary>
public static class BistroBuilderStaff4DCloseIdentitySelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Autotest identidad de cierre",
        false,
        3236)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        if (ok)
        {
            Debug.Log(report);
        }
        else
        {
            Debug.LogError(report);
        }

        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D identidad de cierre",
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
        log.AppendLine("=== BISTRO BUILDER — 4D CLOSE IDENTITY / AUTOTEST ===");

        GameObject firstObject = null;
        GameObject secondObject = null;
        try
        {
            firstObject = new GameObject("4D_CloseIdentity_Waiter_1");
            secondObject = new GameObject("4D_CloseIdentity_Waiter_2");
            Waiter first = firstObject.AddComponent<Waiter>();
            Waiter second = secondObject.AddComponent<Waiter>();

            var secondSerialized = new SerializedObject(second);
            secondSerialized.FindProperty("waiterId").intValue = 2;
            secondSerialized.ApplyModifiedPropertiesWithoutUndo();

            string employeeId = BistroBuilderEmployeeIdUtility.CreateNew();
            var session = new BistroBuilderStaffSessionSnapshot
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
                        employeeId = employeeId,
                        waiterId = first.WaiterId,
                        handledTableIds = new List<int>()
                    }
                }
            };

            var waiters = new Dictionary<int, Waiter>
            {
                { first.WaiterId, first },
                { second.WaiterId, second }
            };

            Check(
                BistroBuilderStaffSessionClosePreflight.TryValidate(
                    session,
                    waiters,
                    out string validError),
                "El preflight acepta un binding con EmployeeId válido y único. " +
                validError,
                ref passed,
                ref failed,
                log);

            BistroBuilderStaffSessionSnapshot invalidEmployee = session.DeepClone();
            invalidEmployee.bindings[0].employeeId = string.Empty;
            Check(
                !BistroBuilderStaffSessionClosePreflight.TryValidate(
                    invalidEmployee,
                    waiters,
                    out _),
                "El preflight rechaza EmployeeId inválido.",
                ref passed,
                ref failed,
                log);

            BistroBuilderStaffSessionSnapshot duplicateEmployee = session.DeepClone();
            duplicateEmployee.bindings.Add(
                new BistroBuilderStaffSessionBindingRecord
                {
                    employeeId = employeeId,
                    waiterId = second.WaiterId,
                    handledTableIds = new List<int>()
                });
            Check(
                !BistroBuilderStaffSessionClosePreflight.TryValidate(
                    duplicateEmployee,
                    waiters,
                    out _),
                "El preflight rechaza el mismo EmployeeId ligado a dos WaiterId.",
                ref passed,
                ref failed,
                log);

            BistroBuilderStaffSessionSnapshot distinctEmployees = session.DeepClone();
            distinctEmployees.bindings.Add(
                new BistroBuilderStaffSessionBindingRecord
                {
                    employeeId = BistroBuilderEmployeeIdUtility.CreateNew(),
                    waiterId = second.WaiterId,
                    handledTableIds = new List<int>()
                });
            Check(
                BistroBuilderStaffSessionClosePreflight.TryValidate(
                    distinctEmployees,
                    waiters,
                    out string distinctError),
                "El preflight acepta identidades EmployeeId/WaiterId distintas. " +
                distinctError,
                ref passed,
                ref failed,
                log);
        }
        finally
        {
            if (firstObject != null)
            {
                Object.DestroyImmediate(firstObject);
            }
            if (secondObject != null)
            {
                Object.DestroyImmediate(secondObject);
            }
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
            return;
        }

        failed++;
        log.AppendLine("[FALLO] " + text);
    }
}
