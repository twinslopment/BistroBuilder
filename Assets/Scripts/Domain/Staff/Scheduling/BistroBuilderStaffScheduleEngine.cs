using System;
using System.Collections.Generic;

/// <summary>
/// Motor puro de horarios/turnos. No conoce escena, SaveGame ni agentes.
/// Todas las mutaciones producen snapshots nuevos y validan identidad EmployeeId.
/// </summary>
public static class BistroBuilderStaffScheduleEngine
{
    public static BistroBuilderStaffScheduleSnapshot CreateEmptySnapshot()
    {
        return new BistroBuilderStaffScheduleSnapshot
        {
            schemaId = BistroBuilderStaffScheduleSnapshot.CurrentSchemaId,
            schemaVersion = BistroBuilderStaffScheduleSnapshot.CurrentSchemaVersion,
            revision = 0L,
            shifts = new List<BistroBuilderStaffShiftRecord>()
        };
    }

    /// <summary>
    /// Valida únicamente la autosuficiencia estructural de staff.schedule.
    /// Se usa durante la prevalidación universal de Load, cuando staff.state
    /// objetivo todavía no ha sido aplicado al mundo.
    /// </summary>
    public static bool TryValidateStructure(
        BistroBuilderStaffScheduleSnapshot snapshot,
        out string error)
    {
        if (snapshot == null ||
            !string.Equals(
                snapshot.schemaId,
                BistroBuilderStaffScheduleSnapshot.CurrentSchemaId,
                StringComparison.Ordinal) ||
            snapshot.schemaVersion != BistroBuilderStaffScheduleSnapshot.CurrentSchemaVersion ||
            snapshot.revision < 0L || snapshot.shifts == null)
        {
            error = "staff.schedule no tiene estructura V1 válida.";
            return false;
        }

        var unique = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < snapshot.shifts.Count; index++)
        {
            BistroBuilderStaffShiftRecord shift = snapshot.shifts[index];
            if (shift == null ||
                !BistroBuilderEmployeeIdUtility.IsValid(shift.employeeId) ||
                shift.dayIndex < 1 ||
                shift.mealService == BistroBuilderMealServiceAvailability.None ||
                !Enum.IsDefined(typeof(BistroBuilderMealServiceAvailability), shift.mealService) ||
                shift.startMinute < 0 || shift.startMinute >= 1440 ||
                shift.endMinute <= shift.startMinute || shift.endMinute > 1440)
            {
                error = "staff.schedule contiene un turno inválido.";
                return false;
            }

            string key = BuildUniquenessKey(
                shift.employeeId,
                shift.dayIndex,
                shift.mealService);
            if (!unique.Add(key))
            {
                error = "Un empleado no puede tener dos turnos para el mismo servicio.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateSnapshot(
        BistroBuilderStaffScheduleSnapshot snapshot,
        BistroBuilderStaffSnapshot staff,
        out string error)
    {
        error = string.Empty;
        if (staff == null || !TryValidateStructure(snapshot, out error))
        {
            if (staff == null && string.IsNullOrWhiteSpace(error))
                error = "staff.schedule no puede cruzarse sin staff.state.";
            return false;
        }

        for (int index = 0; index < snapshot.shifts.Count; index++)
        {
            BistroBuilderStaffShiftRecord shift = snapshot.shifts[index];
            if (!BistroBuilderStaffEngine.TryFindEmployee(
                    staff,
                    shift.employeeId,
                    out BistroBuilderEmployeeRecord employee) ||
                employee == null ||
                employee.employmentStatus != BistroBuilderEmploymentStatus.Active)
            {
                error = "Un turno referencia un EmployeeId inexistente o inactivo.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool TrySetShift(
        BistroBuilderStaffScheduleSnapshot current,
        BistroBuilderStaffSnapshot staff,
        BistroBuilderStaffScheduleProfile profile,
        string employeeId,
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        bool scheduled,
        out BistroBuilderStaffScheduleSnapshot result,
        out string error)
    {
        result = null;
        error = string.Empty;
        if (profile == null || !profile.TryValidate(out error) ||
            !TryValidateSnapshot(current, staff, out error))
        {
            return false;
        }

        string normalized = BistroBuilderEmployeeIdUtility.Normalize(employeeId);
        if (!BistroBuilderEmployeeIdUtility.IsValid(normalized) || dayIndex < 1 ||
            !BistroBuilderStaffEngine.TryFindEmployee(staff, normalized, out BistroBuilderEmployeeRecord employee) ||
            employee == null || employee.employmentStatus != BistroBuilderEmploymentStatus.Active)
        {
            error = "El turno requiere un empleado activo válido.";
            return false;
        }

        if (!profile.TryGetDefaultWindow(mealService, out int startMinute, out int endMinute))
        {
            error = "El servicio no tiene una ventana horaria V1 configurable.";
            return false;
        }

        BistroBuilderStaffScheduleSnapshot candidate = current.DeepClone();
        int existing = FindShiftIndex(candidate, normalized, dayIndex, mealService);
        if (scheduled)
        {
            if (existing >= 0)
            {
                result = candidate;
                error = string.Empty;
                return true;
            }

            candidate.shifts.Add(new BistroBuilderStaffShiftRecord
            {
                employeeId = normalized,
                dayIndex = dayIndex,
                mealService = mealService,
                startMinute = startMinute,
                endMinute = endMinute
            });
        }
        else
        {
            if (existing < 0)
            {
                result = candidate;
                error = string.Empty;
                return true;
            }
            candidate.shifts.RemoveAt(existing);
        }

        try
        {
            candidate.revision = checked(candidate.revision + 1L);
        }
        catch (OverflowException)
        {
            error = "La revisión de staff.schedule no puede incrementarse.";
            return false;
        }

        SortShifts(candidate.shifts);
        if (!TryValidateSnapshot(candidate, staff, out error))
        {
            return false;
        }

        result = candidate;
        error = string.Empty;
        return true;
    }

    public static bool IsScheduled(
        BistroBuilderStaffScheduleSnapshot snapshot,
        string employeeId,
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService)
    {
        return snapshot != null && snapshot.shifts != null &&
               FindShiftIndex(
                   snapshot,
                   BistroBuilderEmployeeIdUtility.Normalize(employeeId),
                   dayIndex,
                   mealService) >= 0;
    }

    public static void CopyScheduledEmployeeIds(
        BistroBuilderStaffScheduleSnapshot snapshot,
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        List<string> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }
        destination.Clear();
        if (snapshot == null || snapshot.shifts == null)
        {
            return;
        }

        for (int index = 0; index < snapshot.shifts.Count; index++)
        {
            BistroBuilderStaffShiftRecord shift = snapshot.shifts[index];
            if (shift != null && shift.dayIndex == dayIndex &&
                shift.mealService == mealService)
            {
                destination.Add(shift.employeeId);
            }
        }
        destination.Sort(StringComparer.Ordinal);
    }

    private static int FindShiftIndex(
        BistroBuilderStaffScheduleSnapshot snapshot,
        string employeeId,
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService)
    {
        if (snapshot == null || snapshot.shifts == null)
        {
            return -1;
        }
        for (int index = 0; index < snapshot.shifts.Count; index++)
        {
            BistroBuilderStaffShiftRecord shift = snapshot.shifts[index];
            if (shift != null && shift.dayIndex == dayIndex &&
                shift.mealService == mealService &&
                string.Equals(shift.employeeId, employeeId, StringComparison.Ordinal))
            {
                return index;
            }
        }
        return -1;
    }

    private static void SortShifts(List<BistroBuilderStaffShiftRecord> shifts)
    {
        shifts.Sort((left, right) =>
        {
            int byDay = left.dayIndex.CompareTo(right.dayIndex);
            if (byDay != 0) return byDay;
            int byService = ((int)left.mealService).CompareTo((int)right.mealService);
            if (byService != 0) return byService;
            return string.Compare(left.employeeId, right.employeeId, StringComparison.Ordinal);
        });
    }

    private static string BuildUniquenessKey(
        string employeeId,
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService)
    {
        return employeeId + "|" + dayIndex + "|" + (int)mealService;
    }
}
