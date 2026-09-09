using System;
using System.Collections.Generic;

/// <summary>
/// Aplica cambios de elegibilidad 4D como una operación de lote.
///
/// No conoce tareas, sesiones ni empleados. Solo coordina el gate runtime de
/// los Waiter existentes. Si un agente rechaza el cambio, restaura exactamente
/// los valores anteriores de todos los agentes ya procesados.
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

    /// <summary>
    /// Aplica el mismo estado objetivo a todos los Waiter indicados.
    /// </summary>
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

        var targets = new List<KeyValuePair<Waiter, bool>>();
        foreach (Waiter waiter in waiters)
        {
            targets.Add(new KeyValuePair<Waiter, bool>(waiter, eligible));
        }

        return TryApply(targets, out error);
    }

    /// <summary>
    /// Aplica un plan mixto de elegibilidad como una única transacción.
    ///
    /// Cada Waiter puede tener su propio estado objetivo. Antes de mutar se
    /// valida que un mismo agente no aparezca con objetivos contradictorios.
    /// Si cualquier agente rechaza el cambio, se restauran exactamente los
    /// estados previos de todos los agentes procesados, incluido el que falló.
    /// </summary>
    public static bool TryApply(
        IEnumerable<KeyValuePair<Waiter, bool>> targets,
        out string error)
    {
        if (targets == null)
        {
            error = "El plan de elegibilidad es nulo.";
            return false;
        }

        var normalized = new List<KeyValuePair<Waiter, bool>>();
        var desiredByWaiter = new Dictionary<Waiter, bool>();

        foreach (KeyValuePair<Waiter, bool> target in targets)
        {
            Waiter waiter = target.Key;
            if (waiter == null)
            {
                continue;
            }

            if (desiredByWaiter.TryGetValue(waiter, out bool existing))
            {
                if (existing != target.Value)
                {
                    error =
                        "WaiterId " + waiter.WaiterId +
                        " aparece con objetivos de elegibilidad contradictorios.";
                    return false;
                }
                continue;
            }

            desiredByWaiter.Add(waiter, target.Value);
            normalized.Add(target);
        }

        var originals = new List<OriginalState>(normalized.Count);
        for (int index = 0; index < normalized.Count; index++)
        {
            Waiter waiter = normalized[index].Key;
            bool desired = normalized[index].Value;

            originals.Add(
                new OriginalState(waiter, waiter.IsStaffServiceEligible));

            if (waiter.TrySetStaffServiceEligibility(desired))
            {
                continue;
            }

            for (int rollbackIndex = originals.Count - 1;
                 rollbackIndex >= 0;
                 rollbackIndex--)
            {
                OriginalState original = originals[rollbackIndex];
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
