using System;
using System.Collections.Generic;

/// <summary>
/// Aplica cambios de elegibilidad 4D como una operación de lote.
///
/// No conoce tareas, sesiones ni empleados. Solo coordina el gate runtime de
/// los Waiter existentes. Si un agente rechaza el cambio, restaura exactamente
/// los valores anteriores de los agentes ya procesados.
/// </summary>
public static class BistroBuilderStaffEligibilityBatch
{
    private readonly struct OriginalState
    {
        public readonly Waiter waiter;
        public readonly bool eligible;

        public OriginalState(Waiter waiter, bool eligible)
        {
            this.waiter = waiter;
            this.eligible = eligible;
        }
    }

    public static bool TryApply(
        IEnumerable<Waiter> waiters,
        bool eligible,
        out string error)
    {
        if (waiters == null)
        {
            error = "La colección de camareros es nula.";
            return false;
        }

        var originals = new List<OriginalState>();
        var seen = new HashSet<Waiter>();

        foreach (Waiter waiter in waiters)
        {
            if (waiter == null || !seen.Add(waiter))
            {
                continue;
            }

            originals.Add(
                new OriginalState(waiter, waiter.IsStaffServiceEligible));

            if (waiter.TrySetStaffServiceEligibility(eligible))
            {
                continue;
            }

            for (int index = originals.Count - 1; index >= 0; index--)
            {
                OriginalState original = originals[index];
                if (original.waiter != null)
                {
                    original.waiter.TrySetStaffServiceEligibility(
                        original.eligible);
                }
            }

            error =
                "WaiterId " + waiter.WaiterId +
                " rechazó el cambio de elegibilidad; el lote fue restaurado.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
