using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate complementario 4D: verifica que un cambio por lote nunca deje la
/// elegibilidad de los Waiter parcialmente aplicada cuando uno está ocupado.
/// </summary>
public static class BistroBuilderStaff4DEligibilityBatchSelfTest
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4D - Autotest elegibilidad transaccional",
        false,
        3233)]
    private static void RunMenu()
    {
        bool ok = Run(out string report);
        Debug.Log(report);
        if (!ok)
        {
            Debug.LogError(
                "El autotest de elegibilidad transaccional 4D ha fallado.");
        }
    }

    public static bool Run(out string report)
    {
        GameObject firstHost = null;
        GameObject secondHost = null;

        try
        {
            firstHost = new GameObject("BB_4D_BATCH_FIRST_TEMP");
            secondHost = new GameObject("BB_4D_BATCH_SECOND_TEMP");

            Waiter first = firstHost.AddComponent<Waiter>();
            Waiter second = secondHost.AddComponent<Waiter>();

            // Ambos comienzan elegibles. El segundo simula trabajo real.
            second.SetState(WaiterState.TakingOrder);

            bool changed = BistroBuilderStaffEligibilityBatch.TryApply(
                new List<Waiter> { first, second },
                false,
                out string error);

            if (changed)
            {
                report =
                    "[FALLO] El lote debía rechazarse con un Waiter ocupado.";
                return false;
            }

            if (!first.IsStaffServiceEligible ||
                !second.IsStaffServiceEligible)
            {
                report =
                    "[FALLO] Un rechazo dejó elegibilidad parcialmente aplicada. " +
                    error;
                return false;
            }

            second.SetState(WaiterState.Idle);
            if (!BistroBuilderStaffEligibilityBatch.TryApply(
                    new List<Waiter> { first, second },
                    false,
                    out error))
            {
                report =
                    "[FALLO] El lote libre debía poder desactivarse. " + error;
                return false;
            }

            if (first.IsStaffServiceEligible || second.IsStaffServiceEligible)
            {
                report =
                    "[FALLO] El lote libre no aplicó la desactivación completa.";
                return false;
            }

            if (!BistroBuilderStaffEligibilityBatch.TryApply(
                    new List<Waiter> { first, second },
                    true,
                    out error) ||
                !first.IsStaffServiceEligible ||
                !second.IsStaffServiceEligible)
            {
                report =
                    "[FALLO] La restauración de elegibilidad no fue completa. " +
                    error;
                return false;
            }

            report =
                "[OK] 4D elegibilidad transaccional: rechazo con rollback, " +
                "aplicación completa y restauración completa.";
            return true;
        }
        finally
        {
            if (secondHost != null)
            {
                Object.DestroyImmediate(secondHost);
            }
            if (firstHost != null)
            {
                Object.DestroyImmediate(firstHost);
            }
        }
    }
}
