using System;
using System.Collections.Generic;

/// <summary>
/// Operaciones de planificación compuestas sobre staff.schedule.
/// Trabaja siempre sobre clones y solo devuelve un snapshot completo válido.
/// </summary>
public static class BistroBuilderStaffSchedulePlanner
{
    public static bool TryReplaceServiceAssignments(
        BistroBuilderStaffScheduleSnapshot current,
        BistroBuilderStaffSnapshot staff,
        BistroBuilderStaffScheduleProfile profile,
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        IReadOnlyList<string> employeeIds,
        out BistroBuilderStaffScheduleSnapshot result,
        out string error)
    {
        result = null;
        error = string.Empty;
        if (employeeIds == null ||
            !BistroBuilderStaffScheduleEngine.TryValidateSnapshot(current, staff, out error) ||
            profile == null || !profile.TryValidate(out error) ||
            !profile.TryGetDefaultWindow(mealService, out _, out _))
        {
            if (string.IsNullOrWhiteSpace(error))
                error = "La sustitución de turnos contiene datos inválidos.";
            return false;
        }

        var normalizedIds = new List<string>(employeeIds.Count);
        var unique = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < employeeIds.Count; index++)
        {
            string id = BistroBuilderEmployeeIdUtility.Normalize(employeeIds[index]);
            if (!BistroBuilderEmployeeIdUtility.IsValid(id) || !unique.Add(id))
            {
                error = "La plantilla del servicio contiene EmployeeId inválido o duplicado.";
                return false;
            }
            normalizedIds.Add(id);
        }

        BistroBuilderStaffScheduleSnapshot candidate = current.DeepClone();
        for (int index = candidate.shifts.Count - 1; index >= 0; index--)
        {
            BistroBuilderStaffShiftRecord shift = candidate.shifts[index];
            if (shift != null && shift.dayIndex == dayIndex &&
                shift.mealService == mealService)
            {
                candidate.shifts.RemoveAt(index);
            }
        }

        for (int index = 0; index < normalizedIds.Count; index++)
        {
            if (!BistroBuilderStaffScheduleEngine.TrySetShift(
                    candidate,
                    staff,
                    profile,
                    normalizedIds[index],
                    dayIndex,
                    mealService,
                    true,
                    out candidate,
                    out error))
            {
                return false;
            }
        }

        // Si se quitó un plan pero no se añadió ningún turno, TrySetShift no
        // incrementó la revisión. Registrar una única revisión de la operación.
        if (normalizedIds.Count == 0)
        {
            try { candidate.revision = checked(current.revision + 1L); }
            catch (OverflowException)
            {
                error = "La revisión de staff.schedule no puede incrementarse.";
                return false;
            }
        }

        if (!BistroBuilderStaffScheduleEngine.TryValidateSnapshot(candidate, staff, out error))
            return false;

        result = candidate;
        error = string.Empty;
        return true;
    }

    public static bool TryCopyServicePlan(
        BistroBuilderStaffScheduleSnapshot current,
        BistroBuilderStaffSnapshot staff,
        BistroBuilderStaffScheduleProfile profile,
        int sourceDay,
        BistroBuilderMealServiceAvailability sourceService,
        int targetDay,
        BistroBuilderMealServiceAvailability targetService,
        out BistroBuilderStaffScheduleSnapshot result,
        out string error)
    {
        var ids = new List<string>();
        BistroBuilderStaffScheduleEngine.CopyScheduledEmployeeIds(
            current, sourceDay, sourceService, ids);
        return TryReplaceServiceAssignments(
            current,
            staff,
            profile,
            targetDay,
            targetService,
            ids,
            out result,
            out error);
    }

    public static bool TryBuildMinimumWaiterPlan(
        BistroBuilderStaffScheduleSnapshot current,
        BistroBuilderStaffSnapshot staff,
        BistroBuilderStaffRoleCatalog roles,
        BistroBuilderStaffScheduleProfile profile,
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        out BistroBuilderStaffScheduleSnapshot result,
        out string error)
    {
        result = null;
        error = string.Empty;
        if (staff == null || roles == null || profile == null ||
            !profile.TryValidate(out error))
        {
            if (string.IsNullOrWhiteSpace(error)) error = "Faltan datos para autocompletar turnos.";
            return false;
        }

        var candidates = new List<BistroBuilderEmployeeRecord>();
        for (int index = 0; index < staff.employees.Count; index++)
        {
            BistroBuilderEmployeeRecord employee = staff.employees[index];
            if (employee == null ||
                employee.employmentStatus != BistroBuilderEmploymentStatus.Active ||
                employee.availability != BistroBuilderEmployeeAvailability.Available ||
                !roles.TryGetRole(employee.roleId, out BistroBuilderStaffRoleDefinition role) ||
                role == null ||
                !string.Equals(
                    role.operationalAdapterId,
                    BistroBuilderStaffOperationalAdapterIds.WaiterAgent,
                    StringComparison.Ordinal))
            {
                continue;
            }
            candidates.Add(employee.DeepClone());
        }

        candidates.Sort((left, right) =>
        {
            int bySalary = left.salaryCentsPerService.CompareTo(right.salaryCentsPerService);
            if (bySalary != 0) return bySalary;
            return string.Compare(left.employeeId, right.employeeId, StringComparison.Ordinal);
        });

        int count = Math.Min(profile.MinimumRecommendedWaiters, candidates.Count);
        var ids = new List<string>(count);
        for (int index = 0; index < count; index++) ids.Add(candidates[index].employeeId);

        if (ids.Count == 0)
        {
            error = "No hay camareros activos y disponibles para autocompletar el turno.";
            return false;
        }

        return TryReplaceServiceAssignments(
            current, staff, profile, dayIndex, mealService, ids, out result, out error);
    }
}
