using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate complementario 4D: verifica que un cambio por lote nunca deje la
/// elegibilidad de los Waiter parcialmente aplicada cuando uno está ocupado.
/// Cubre tanto lotes uniformes como planes mixtos usados por restore/rehidratación.
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
                    "[FALLO] El lote uniforme debía rechazarse con un Waiter ocupado.";
                return false;
            }

            if (!first.IsStaffServiceEligible ||
                !second.IsStaffServiceEligible)
            {
                report =
                    "[FALLO] Un rechazo uniforme dejó elegibilidad parcial. " +
                    error;
                return false;
            }

            second.SetState(WaiterState.Idle);
            if (!BistroBuilderStaffEligibilityBatch.TryApply(
                    new List<Waiter> { first },
                    false,
                    out error) || first.IsStaffServiceEligible)
            {
                report =
                    "[FALLO] No pudo prepararse el estado mixto inicial. " + error;
                return false;
            }

            // Estado previo mixto: first=false, second=true. El primer target
            // cambia a true y el segundo falla al intentar pasar a false ocupado.
            second.SetState(WaiterState.TakingOrder);
            var mixedFailure = new List<KeyValuePair<Waiter, bool>>
            {
                new KeyValuePair<Waiter, bool>(first, true),
                new KeyValuePair<Waiter, bool>(second, false)
            };

            if (BistroBuilderStaffEligibilityBatch.TryApply(
                    mixedFailure,
                    out error))
            {
                report =
                    "[FALLO] El plan mixto debía rechazarse con el segundo Waiter ocupado.";
                return false;
            }

            if (first.IsStaffServiceEligible ||
                !second.IsStaffServiceEligible)
            {
                report =
                    "[FALLO] El rollback mixto no restauró exactamente false/true. " +
                    error;
                return false;
            }

            second.SetState(WaiterState.Idle);
            if (!BistroBuilderStaffEligibilityBatch.TryApply(
                    mixedFailure,
                    out error) ||
                !first.IsStaffServiceEligible ||
                second.IsStaffServiceEligible)
            {
                report =
                    "[FALLO] El plan mixto libre no aplicó exactamente true/false. " +
                    error;
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
                    "[FALLO] La restauración uniforme final no fue completa. " +
                    error;
                return false;
            }

            report =
                "[OK] 4D elegibilidad transaccional: lotes uniformes y mixtos, " +
                "rechazo con rollback exacto y aplicación completa.";
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
