using System;
using System.Collections.Generic;

/// <summary>
/// Gate 4D previo al cierre definitivo de una sesión de Personal.
///
/// No aplica XP, no cambia estados y no toca WaiterTaskCoordinator. Su única
/// responsabilidad es demostrar que todos los WaiterId ligados existen y están
/// realmente libres antes de permitir que la capa de sesión publique resultados
/// de rendimiento o desactive agentes.
/// </summary>
public static class BistroBuilderStaffSessionClosePreflight
{
    public static bool TryValidate(
        BistroBuilderStaffSessionSnapshot session,
        IReadOnlyDictionary<int, Waiter> waitersById,
        out string error)
    {
        if (session == null || !session.active ||
            session.bindings == null || session.bindings.Count == 0)
        {
            error = "El preflight de cierre necesita una sesión activa con bindings.";
            return false;
        }

        if (waitersById == null)
        {
            error = "El índice runtime de camareros es nulo.";
            return false;
        }

        var seenWaiterIds = new HashSet<int>();
        for (int index = 0; index < session.bindings.Count; index++)
        {
            BistroBuilderStaffSessionBindingRecord binding = session.bindings[index];
            if (binding == null || binding.waiterId < 1 ||
                !seenWaiterIds.Add(binding.waiterId))
            {
                error = "La sesión contiene un binding de camarero inválido o duplicado.";
                return false;
            }

            if (!waitersById.TryGetValue(binding.waiterId, out Waiter waiter) ||
                waiter == null)
            {
                error =
                    "No existe el WaiterId " + binding.waiterId +
                    " requerido para cerrar la sesión.";
                return false;
            }

            if (!waiter.IsStaffServiceEligible)
            {
                error =
                    "WaiterId " + binding.waiterId +
                    " perdió su elegibilidad antes de finalizar la sesión.";
                return false;
            }

            if (!waiter.IsAvailable || waiter.CurrentState != WaiterState.Idle)
            {
                error =
                    "WaiterId " + binding.waiterId +
                    " todavía está ocupado; el rendimiento no puede consolidarse.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
