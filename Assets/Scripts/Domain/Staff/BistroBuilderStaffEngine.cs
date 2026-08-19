using System;
using System.Collections.Generic;

/// <summary>
/// Reglas puras de Personal. No conoce escenas, GameObjects, camareros ni
/// Finanzas. Toda mutación genera un nuevo snapshot validado.
/// </summary>
public static class BistroBuilderStaffEngine
{
    public static BistroBuilderStaffSnapshot CreateEmptySnapshot()
    {
        return new BistroBuilderStaffSnapshot
        {
            schemaId = BistroBuilderStaffSnapshot.CurrentSchemaId,
            schemaVersion = BistroBuilderStaffSnapshot.CurrentSchemaVersion,
            revision = 1L,
            employees = new List<BistroBuilderEmployeeRecord>()
        };
    }

    public static bool TryBuildEmployee(
        string employeeId,
        BistroBuilderEmployeeCreateRequest request,
        BistroBuilderStaffRoleCatalog roleCatalog,
        out BistroBuilderEmployeeRecord employee,
        out string error)
    {
        employee = null;
        if (request == null)
        {
            error = "La petición de creación de empleado es nula.";
            return false;
        }

        var built = new BistroBuilderEmployeeRecord
        {
            employeeId = BistroBuilderEmployeeIdUtility.Normalize(employeeId),
            firstName = NormalizeDisplayName(request.firstName),
            lastName = NormalizeDisplayName(request.lastName),
            roleId = BistroBuilderStaffStableIdUtility.Normalize(request.roleId),
            employmentStatus = BistroBuilderEmploymentStatus.Active,
            availability = request.availability,
            salaryCentsPerService = request.salaryCentsPerService,
            hiredDayIndex = request.hiredDayIndex,
            experiencePoints = request.initialExperiencePoints,
            skills = request.initialSkills != null
                ? request.initialSkills.DeepClone()
                : null,
            responsibilities = request.responsibilities != null
                ? request.responsibilities.DeepClone()
                : new BistroBuilderEmployeeResponsibilitySettings(),
            performance = new BistroBuilderEmployeePerformanceData(),
            revision = 1L
        };

        NormalizeEmployeeInPlace(built);
        if (!TryValidateEmployee(built, roleCatalog, true, out error))
        {
            return false;
        }

        employee = built;
        error = string.Empty;
        return true;
    }

    public static bool TryValidateSnapshot(
        BistroBuilderStaffSnapshot snapshot,
        BistroBuilderStaffRoleCatalog roleCatalog,
        out string error)
    {
        if (snapshot == null ||
            !string.Equals(
                snapshot.schemaId,
                BistroBuilderStaffSnapshot.CurrentSchemaId,
                StringComparison.Ordinal) ||
            snapshot.schemaVersion != BistroBuilderStaffSnapshot.CurrentSchemaVersion ||
            snapshot.revision < 1L ||
            snapshot.employees == null)
        {
            error = "staff.state contiene cabecera o colección inválida.";
            return false;
        }

        if (roleCatalog == null || !roleCatalog.TryValidate(out error))
        {
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < snapshot.employees.Count; index++)
        {
            BistroBuilderEmployeeRecord employee = snapshot.employees[index];
            if (employee == null ||
                !TryValidateEmployee(employee, roleCatalog, false, out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "staff.state contiene un empleado nulo o inválido.";
                }
                return false;
            }

            string employeeId =
                BistroBuilderEmployeeIdUtility.Normalize(employee.employeeId);
            if (!ids.Add(employeeId))
            {
                error = "staff.state repite EmployeeId: " + employeeId + ".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateEmployee(
        BistroBuilderEmployeeRecord employee,
        BistroBuilderStaffRoleCatalog roleCatalog,
        bool requireActiveRole,
        out string error)
    {
        if (employee == null)
        {
            error = "El empleado es nulo.";
            return false;
        }

        if (!BistroBuilderEmployeeIdUtility.IsValid(employee.employeeId))
        {
            error = "EmployeeId no es estable o válido.";
            return false;
        }

        string firstName = NormalizeDisplayName(employee.firstName);
        string lastName = NormalizeDisplayName(employee.lastName);
        if (firstName.Length < 1 || firstName.Length > 48 || lastName.Length > 64)
        {
            error = "El nombre del empleado no es válido.";
            return false;
        }

        if (roleCatalog == null ||
            !roleCatalog.TryGetRole(employee.roleId, out BistroBuilderStaffRoleDefinition role) ||
            role == null ||
            (requireActiveRole && !role.active))
        {
            error = "El empleado referencia un rol inexistente o no disponible.";
            return false;
        }

        if (!Enum.IsDefined(typeof(BistroBuilderEmploymentStatus), employee.employmentStatus) ||
            !Enum.IsDefined(typeof(BistroBuilderEmployeeAvailability), employee.availability) ||
            employee.salaryCentsPerService < 0L ||
            employee.hiredDayIndex < 1 ||
            employee.experiencePoints < 0L ||
            employee.revision < 1L)
        {
            error = "El contrato, estado o progreso del empleado no es válido.";
            return false;
        }

        if (employee.employmentStatus != BistroBuilderEmploymentStatus.Active &&
            employee.availability != BistroBuilderEmployeeAvailability.Unavailable)
        {
            error = "Un empleado inactivo o despedido no puede figurar disponible.";
            return false;
        }

        if (!TryValidateSkills(employee.skills, out error) ||
            !TryValidateResponsibilities(employee.responsibilities, out error) ||
            !TryValidatePerformance(employee.performance, out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryAppendEmployee(
        BistroBuilderStaffSnapshot snapshot,
        BistroBuilderEmployeeRecord employee,
        BistroBuilderStaffRoleCatalog roleCatalog,
        out BistroBuilderStaffSnapshot result,
        out string error)
    {
        result = null;
        if (!TryValidateSnapshot(snapshot, roleCatalog, out error) ||
            !TryValidateEmployee(employee, roleCatalog, true, out error))
        {
            return false;
        }

        string id = BistroBuilderEmployeeIdUtility.Normalize(employee.employeeId);
        for (int index = 0; index < snapshot.employees.Count; index++)
        {
            if (string.Equals(
                    BistroBuilderEmployeeIdUtility.Normalize(
                        snapshot.employees[index].employeeId),
                    id,
                    StringComparison.Ordinal))
            {
                error = "Ya existe un empleado con EmployeeId " + id + ".";
                return false;
            }
        }

        try
        {
            result = snapshot.DeepClone();
            result.employees.Add(employee.DeepClone());
            result.revision = checked(result.revision + 1L);
        }
        catch (OverflowException)
        {
            result = null;
            error = "La revisión de staff.state desbordó el rango soportado.";
            return false;
        }

        return TryValidateSnapshot(result, roleCatalog, out error);
    }

    public static bool TrySetAvailability(
        BistroBuilderStaffSnapshot snapshot,
        string employeeId,
        BistroBuilderEmployeeAvailability availability,
        BistroBuilderStaffRoleCatalog roleCatalog,
        out BistroBuilderStaffSnapshot result,
        out BistroBuilderEmployeeRecord updatedEmployee,
        out bool changed,
        out string error)
    {
        result = null;
        updatedEmployee = null;
        changed = false;

        if (!TryValidateSnapshot(snapshot, roleCatalog, out error) ||
            !Enum.IsDefined(typeof(BistroBuilderEmployeeAvailability), availability))
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "La disponibilidad solicitada no existe.";
            }
            return false;
        }

        if (!TryFindEmployeeIndex(snapshot, employeeId, out int employeeIndex))
        {
            error = "No existe el EmployeeId solicitado.";
            return false;
        }

        BistroBuilderEmployeeRecord current = snapshot.employees[employeeIndex];
        if (current.employmentStatus != BistroBuilderEmploymentStatus.Active &&
            availability == BistroBuilderEmployeeAvailability.Available)
        {
            error = "Solo un empleado activo puede quedar disponible.";
            return false;
        }

        if (current.availability == availability)
        {
            result = snapshot.DeepClone();
            updatedEmployee = current.DeepClone();
            error = string.Empty;
            return true;
        }

        try
        {
            result = snapshot.DeepClone();
            BistroBuilderEmployeeRecord updated = result.employees[employeeIndex];
            updated.availability = availability;
            updated.revision = checked(updated.revision + 1L);
            result.revision = checked(result.revision + 1L);
            updatedEmployee = updated.DeepClone();
            changed = true;
        }
        catch (OverflowException)
        {
            result = null;
            updatedEmployee = null;
            error = "La revisión de Personal desbordó el rango soportado.";
            return false;
        }

        return TryValidateSnapshot(result, roleCatalog, out error);
    }

    public static bool TryFindEmployee(
        BistroBuilderStaffSnapshot snapshot,
        string employeeId,
        out BistroBuilderEmployeeRecord employee)
    {
        employee = null;
        if (snapshot == null || snapshot.employees == null ||
            !TryFindEmployeeIndex(snapshot, employeeId, out int index))
        {
            return false;
        }

        employee = snapshot.employees[index].DeepClone();
        return true;
    }

    private static bool TryFindEmployeeIndex(
        BistroBuilderStaffSnapshot snapshot,
        string employeeId,
        out int index)
    {
        index = -1;
        if (snapshot == null || snapshot.employees == null)
        {
            return false;
        }

        string normalized = BistroBuilderEmployeeIdUtility.Normalize(employeeId);
        if (!BistroBuilderEmployeeIdUtility.IsValid(normalized))
        {
            return false;
        }

        for (int current = 0; current < snapshot.employees.Count; current++)
        {
            BistroBuilderEmployeeRecord employee = snapshot.employees[current];
            if (employee != null &&
                string.Equals(
                    BistroBuilderEmployeeIdUtility.Normalize(employee.employeeId),
                    normalized,
                    StringComparison.Ordinal))
            {
                index = current;
                return true;
            }
        }
        return false;
    }

    private static bool TryValidateSkills(
        BistroBuilderEmployeeSkillSet skills,
        out string error)
    {
        if (skills == null ||
            !IsSkill(skills.speed) ||
            !IsSkill(skills.attentiveness) ||
            !IsSkill(skills.organization) ||
            !IsSkill(skills.hospitality))
        {
            error = "Las habilidades del empleado deben estar entre 0 y 100.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateResponsibilities(
        BistroBuilderEmployeeResponsibilitySettings settings,
        out string error)
    {
        if (settings == null ||
            !BistroBuilderStaffStableIdUtility.IsValidOptional(
                settings.primaryResponsibilityId) ||
            !BistroBuilderStaffStableIdUtility.IsValidOptional(
                settings.primaryZoneId))
        {
            error = "La configuración de responsabilidad/zona no es válida.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidatePerformance(
        BistroBuilderEmployeePerformanceData performance,
        out string error)
    {
        if (performance == null ||
            performance.completedServices < 0 ||
            performance.completedTasks < 0 ||
            performance.failedTasks < 0 ||
            performance.tablesHandled < 0 ||
            performance.totalTaskDurationMilliseconds < 0L)
        {
            error = "Los contadores históricos de rendimiento no son válidos.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsSkill(int value)
    {
        return value >= 0 && value <= 100;
    }

    private static string NormalizeDisplayName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static void NormalizeEmployeeInPlace(BistroBuilderEmployeeRecord employee)
    {
        employee.employeeId =
            BistroBuilderEmployeeIdUtility.Normalize(employee.employeeId);
        employee.firstName = NormalizeDisplayName(employee.firstName);
        employee.lastName = NormalizeDisplayName(employee.lastName);
        employee.roleId =
            BistroBuilderStaffStableIdUtility.Normalize(employee.roleId);
        if (employee.responsibilities != null)
        {
            employee.responsibilities.primaryResponsibilityId =
                BistroBuilderStaffStableIdUtility.Normalize(
                    employee.responsibilities.primaryResponsibilityId);
            employee.responsibilities.primaryZoneId =
                BistroBuilderStaffStableIdUtility.Normalize(
                    employee.responsibilities.primaryZoneId);
        }
    }
}
