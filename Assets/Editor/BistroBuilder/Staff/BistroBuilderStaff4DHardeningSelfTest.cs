using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest estático de los gates de endurecimiento 4D añadidos antes de 4E.
/// No necesita Play Mode ni modifica escenas guardadas.
/// </summary>
public static class BistroBuilderStaff4DHardeningSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Autotest endurecimiento",
        false,
        3233)]
    private static void RunMenu()
    {
        bool ok = Run(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4D endurecimiento",
            passed + " OK / " + failed + " fallos",
            "Aceptar");
        if (!ok)
        {
            Debug.LogError("El autotest de endurecimiento 4D ha fallado.");
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
        log.AppendLine("=== BISTRO BUILDER — 4D HARDENING / AUTOTEST ===");

        GameObject waiterObject = null;
        try
        {
            waiterObject = new GameObject("4D_Hardening_Test_Waiter");
            Waiter waiter = waiterObject.AddComponent<Waiter>();

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
                        employeeId = BistroBuilderEmployeeIdUtility.CreateNew(),
                        waiterId = waiter.WaiterId,
                        handledTableIds = new List<int>()
                    }
                }
            };

            var waiters = new Dictionary<int, Waiter>
            {
                { waiter.WaiterId, waiter }
            };

            Check(
                waiter.CurrentState == WaiterState.Idle && waiter.IsAvailable,
                "El agente de prueba parte libre y elegible.",
                ref passed, ref failed, log);

            Check(
                BistroBuilderStaffSessionClosePreflight.TryValidate(
                    session,
                    waiters,
                    out string idleError),
                "El preflight acepta una sesión cuyos agentes están libres. " +
                idleError,
                ref passed, ref failed, log);

            waiter.SetState(WaiterState.TakingOrder);
            Check(
                !BistroBuilderStaffSessionClosePreflight.TryValidate(
                    session,
                    waiters,
                    out _),
                "El preflight bloquea consolidación mientras un agente trabaja.",
                ref passed, ref failed, log);

            waiter.SetState(WaiterState.Idle);
            Check(
                waiter.TrySetStaffServiceEligibility(false) &&
                !BistroBuilderStaffSessionClosePreflight.TryValidate(
                    session,
                    waiters,
                    out _),
                "El preflight rechaza un binding cuya elegibilidad se perdió.",
                ref passed, ref failed, log);

            Check(
                waiter.TrySetStaffServiceEligibility(true),
                "La elegibilidad del agente de prueba puede restaurarse.",
                ref passed, ref failed, log);

            var missing = new Dictionary<int, Waiter>();
            Check(
                !BistroBuilderStaffSessionClosePreflight.TryValidate(
                    session,
                    missing,
                    out _),
                "El preflight rechaza WaiterId inexistentes.",
                ref passed, ref failed, log);

            var duplicateSession = session.DeepClone();
            duplicateSession.bindings.Add(
                duplicateSession.bindings[0].DeepClone());
            Check(
                !BistroBuilderStaffSessionClosePreflight.TryValidate(
                    duplicateSession,
                    waiters,
                    out _),
                "El preflight rechaza bindings duplicados.",
                ref passed, ref failed, log);

            Check(
                BistroBuilderStaffEligibilityBatch.TryApply(
                    new[] { waiter },
                    false,
                    out string batchDisableError) &&
                !waiter.IsStaffServiceEligible,
                "El lote transaccional puede desactivar agentes libres. " +
                batchDisableError,
                ref passed, ref failed, log);

            Check(
                BistroBuilderStaffEligibilityBatch.TryApply(
                    new[] { waiter },
                    true,
                    out string batchEnableError) &&
                waiter.IsStaffServiceEligible,
                "El lote transaccional restaura elegibilidad. " +
                batchEnableError,
                ref passed, ref failed, log);
        }
        catch (Exception exception)
        {
            failed++;
            log.AppendLine("[FALLO] Excepción inesperada: " + exception);
        }
        finally
        {
            if (waiterObject != null)
            {
                UnityEngine.Object.DestroyImmediate(waiterObject);
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
