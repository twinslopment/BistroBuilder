using System;

/// <summary>
/// Mutaciones laborales 4B sobre staff.state. Mantiene el registro histórico:
/// despedir no elimina el EmployeeId ni sus métricas.
/// </summary>
public static class BistroBuilderStaffEmploymentEngine
{
    public static bool TryDismissEmployee(
        BistroBuilderStaffSnapshot snapshot,
        string employeeId,
        BistroBuilderStaffRoleCatalog roleCatalog,
        out BistroBuilderStaffSnapshot result,
        out BistroBuilderEmployeeRecord dismissed,
        out bool availabilityChanged,
        out string error)
    {
        result = null;
        dismissed = null;
        availabilityChanged = false;

        if (!BistroBuilderStaffEngine.TryValidateSnapshot(
                snapshot,
                roleCatalog,
                out error))
        {
            return false;
        }

        if (!BistroBuilderEmployeeIdUtility.IsValid(employeeId))
        {
            error = "EmployeeId no es válido.";
            return false;
        }

        int index = -1;
        string normalized = BistroBuilderEmployeeIdUtility.Normalize(employeeId);
        for (int current = 0; current < snapshot.employees.Count; current++)
        {
            BistroBuilderEmployeeRecord employee = snapshot.employees[current];
            if (employee != null && string.Equals(
                    BistroBuilderEmployeeIdUtility.Normalize(employee.employeeId),
                    normalized,
                    StringComparison.Ordinal))
            {
                index = current;
                break;
            }
        }

        if (index < 0)
        {
            error = "No existe el EmployeeId solicitado.";
            return false;
        }

        BistroBuilderEmployeeRecord existing = snapshot.employees[index];
        if (existing.employmentStatus != BistroBuilderEmploymentStatus.Active)
        {
            error = "Solo puede despedirse un empleado activo.";
            return false;
        }

        try
        {
            result = snapshot.DeepClone();
            BistroBuilderEmployeeRecord updated = result.employees[index];
            availabilityChanged =
                updated.availability != BistroBuilderEmployeeAvailability.Unavailable;
            updated.employmentStatus = BistroBuilderEmploymentStatus.Dismissed;
            updated.availability = BistroBuilderEmployeeAvailability.Unavailable;
            updated.revision = checked(updated.revision + 1L);
            result.revision = checked(result.revision + 1L);
            dismissed = updated.DeepClone();
        }
        catch (OverflowException)
        {
            result = null;
            dismissed = null;
            availabilityChanged = false;
            error = "La revisión de Personal ha desbordado el rango soportado.";
            return false;
        }

        return BistroBuilderStaffEngine.TryValidateSnapshot(
            result,
            roleCatalog,
            out error);
    }
}
