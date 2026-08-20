using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Preflight read-only para restaurar staff.session.runtime.
///
/// Comprueba que todos los WaiterId persistidos siguen resolviendo contra la
/// escena operativa real antes de que 4D cambie ninguna elegibilidad. No crea
/// agentes, no modifica tareas y no sustituye a WaiterTaskCoordinator.
/// </summary>
public static class BistroBuilderStaffSessionRestorePreflight
{
    /// <summary>
    /// Valida únicamente la parte runtime que no puede comprobar el dominio:
    /// identidad y existencia de los Waiter operativos referenciados.
    /// </summary>
    public static bool TryValidate(
        BistroBuilderStaffSessionSnapshot snapshot,
        out string error)
    {
        if (snapshot == null)
        {
            error = "El snapshot de sesión de Personal es nulo.";
            return false;
        }

        Waiter[] sceneWaiters = Object.FindObjectsByType<Waiter>(
            FindObjectsSortMode.InstanceID);
        var waiterIds = new HashSet<int>();

        for (int index = 0; index < sceneWaiters.Length; index++)
        {
            Waiter waiter = sceneWaiters[index];
            if (waiter == null || waiter.WaiterId < 1 ||
                !waiterIds.Add(waiter.WaiterId))
            {
                error =
                    "La escena contiene WaiterId inválidos o duplicados; " +
                    "staff.session.runtime no puede restaurarse de forma segura.";
                return false;
            }
        }

        if (!snapshot.active)
        {
            error = string.Empty;
            return true;
        }

        if (snapshot.bindings == null || snapshot.bindings.Count == 0)
        {
            error =
                "Una sesión activa de Personal no contiene bindings restaurables.";
            return false;
        }

        for (int index = 0; index < snapshot.bindings.Count; index++)
        {
            BistroBuilderStaffSessionBindingRecord binding =
                snapshot.bindings[index];
            if (binding == null || binding.waiterId < 1 ||
                !waiterIds.Contains(binding.waiterId))
            {
                error =
                    "staff.session.runtime referencia WaiterId inexistente " +
                    (binding != null ? binding.waiterId : 0) +
                    "; se aborta antes de cambiar elegibilidad.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
