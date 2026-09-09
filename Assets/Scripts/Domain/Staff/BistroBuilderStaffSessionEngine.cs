using System;
using System.Collections.Generic;

/// <summary>
/// Invariantes puras del binding 4D. No conoce GameObjects ni escenas.
/// </summary>
public static class BistroBuilderStaffSessionEngine
{
    public static BistroBuilderStaffSessionSnapshot CreateInactiveSnapshot()
    {
        return new BistroBuilderStaffSessionSnapshot
        {
            schemaId = BistroBuilderStaffSessionSnapshot.CurrentSchemaId,
            schemaVersion = BistroBuilderStaffSessionSnapshot.CurrentSchemaVersion,
            revision = 1L,
            active = false,
            sessionId = string.Empty,
            dayIndex = 0,
            bindings = new List<BistroBuilderStaffSessionBindingRecord>()
        };
    }

    public static bool TryValidateSnapshot(
        BistroBuilderStaffSessionSnapshot snapshot,
        BistroBuilderStaffSnapshot staffSnapshot,
        out string error)
    {
        if (snapshot == null ||
            !string.Equals(
                snapshot.schemaId,
                BistroBuilderStaffSessionSnapshot.CurrentSchemaId,
                StringComparison.Ordinal) ||
            snapshot.schemaVersion !=
                BistroBuilderStaffSessionSnapshot.CurrentSchemaVersion ||
            snapshot.revision < 1L ||
            snapshot.bindings == null)
        {
            error = "staff.session.runtime contiene cabecera o colección inválida.";
            return false;
        }

        if (staffSnapshot == null || staffSnapshot.employees == null)
        {
            error = "El binding de sesión necesita staff.state para validarse.";
            return false;
        }

        if (!snapshot.active)
        {
            if (!string.IsNullOrEmpty(snapshot.sessionId) ||
                snapshot.dayIndex != 0 || snapshot.bindings.Count != 0)
            {
                error = "Una sesión inactiva conserva identidad o bindings.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        if (!BistroBuilderStaffSessionIdUtility.IsValid(snapshot.sessionId) ||
            snapshot.dayIndex < 1 || snapshot.bindings.Count == 0)
        {
            error = "La sesión activa no contiene identidad, día o bindings válidos.";
            return false;
        }

        var employeesById = new Dictionary<string, BistroBuilderEmployeeRecord>(
            StringComparer.Ordinal);
        for (int index = 0; index < staffSnapshot.employees.Count; index++)
        {
            BistroBuilderEmployeeRecord employee = staffSnapshot.employees[index];
            if (employee == null ||
                !BistroBuilderEmployeeIdUtility.IsValid(employee.employeeId))
            {
                error = "staff.state contiene un empleado inválido para el binding.";
                return false;
            }

            string employeeId = BistroBuilderEmployeeIdUtility.Normalize(
                employee.employeeId);
            if (!employeesById.TryAdd(employeeId, employee))
            {
                error = "staff.state repite EmployeeId durante validación de sesión.";
                return false;
            }
        }

        var employeeIds = new HashSet<string>(StringComparer.Ordinal);
        var waiterIds = new HashSet<int>();
        for (int index = 0; index < snapshot.bindings.Count; index++)
        {
            BistroBuilderStaffSessionBindingRecord binding = snapshot.bindings[index];
            if (binding == null ||
                !BistroBuilderEmployeeIdUtility.IsValid(binding.employeeId) ||
                binding.waiterId < 1 || binding.completedTasks < 0 ||
                binding.failedTasks < 0 ||
                binding.totalTaskDurationMilliseconds < 0L ||
                binding.handledTableIds == null)
            {
                error = "La sesión contiene un binding con datos básicos inválidos.";
                return false;
            }

            string employeeId = BistroBuilderEmployeeIdUtility.Normalize(
                binding.employeeId);
            if (!employeeIds.Add(employeeId) || !waiterIds.Add(binding.waiterId))
            {
                error =
                    "Un EmployeeId o WaiterId aparece ligado más de una vez en la sesión.";
                return false;
            }

            if (!employeesById.TryGetValue(
                    employeeId,
                    out BistroBuilderEmployeeRecord employee) ||
                employee.employmentStatus != BistroBuilderEmploymentStatus.Active ||
                employee.availability != BistroBuilderEmployeeAvailability.Available)
            {
                error =
                    "Un binding referencia un empleado inexistente, inactivo o no disponible.";
                return false;
            }

            var tableIds = new HashSet<int>();
            for (int tableIndex = 0;
                 tableIndex < binding.handledTableIds.Count;
                 tableIndex++)
            {
                int tableId = binding.handledTableIds[tableIndex];
                if (tableId < 1 || !tableIds.Add(tableId))
                {
                    error =
                        "Un binding contiene TableId inválidos o repetidos en rendimiento.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    public static string BuildServiceResultOperationId(
        string sessionId,
        string employeeId)
    {
        string normalizedSession = BistroBuilderStaffSessionIdUtility.Normalize(
            sessionId);
        string normalizedEmployee = BistroBuilderEmployeeIdUtility.Normalize(
            employeeId);
        if (!BistroBuilderStaffSessionIdUtility.IsValid(normalizedSession) ||
            !BistroBuilderEmployeeIdUtility.IsValid(normalizedEmployee))
        {
            return string.Empty;
        }

        return "staff.service:" + normalizedSession + ":" + normalizedEmployee;
    }
}
