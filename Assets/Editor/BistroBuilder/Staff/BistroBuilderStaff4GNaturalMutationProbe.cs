using System;
using UnityEngine;

/// <summary>
/// 4G — Detector puro de mutación observable entre un checkpoint activo y
/// su posterior Load.
///
/// No altera Staff, WaiterTaskCoordinator, SaveGame ni el servicio. Su única
/// responsabilidad es demostrar que el runtime persistible se separó del
/// estado guardado antes de permitir que el Queen Test intente restaurarlo.
/// </summary>
public static class BistroBuilderStaff4GNaturalMutationProbe
{
    /// <summary>
    /// Devuelve true cuando el estado de sesión actual ya no coincide con el
    /// snapshot capturado al guardar el checkpoint, o cuando el contador de
    /// tareas completadas del empleado objetivo ha avanzado.
    /// </summary>
    public static bool HasObservableMutation(
        string checkpointSessionJson,
        int checkpointCompletedTasks,
        BistroBuilderStaffSessionSnapshot currentSession,
        BistroBuilderEmployeeSessionAssignmentView currentAssignment,
        out string evidence)
    {
        evidence = string.Empty;

        if (currentSession == null)
        {
            return false;
        }

        string currentJson = JsonUtility.ToJson(currentSession);
        if (!string.Equals(
                checkpointSessionJson ?? string.Empty,
                currentJson,
                StringComparison.Ordinal))
        {
            evidence = "staff.session.runtime cambió respecto al checkpoint.";
            return true;
        }

        if (currentAssignment != null &&
            currentAssignment.completedTasks > checkpointCompletedTasks)
        {
            evidence =
                "El empleado objetivo completó tareas después del checkpoint.";
            return true;
        }

        return false;
    }
}
