using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad de planificación de horarios y turnos.
///
/// No modifica StaffService, no crea agentes y no abre/cierra servicios.
/// Conserva únicamente el plan de turnos y ofrece consultas de cobertura.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Staff/Staff Schedule Service")]
public sealed class BistroBuilderStaffScheduleService : MonoBehaviour
{
    [SerializeField] private BistroBuilderStaffService staffService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private RestaurantServiceStateService serviceStateService;
    [SerializeField] private BistroBuilderStaffScheduleProfile scheduleProfile;

    private BistroBuilderStaffScheduleSnapshot state;
    private readonly List<BistroBuilderEmployeeRecord> employeeBuffer =
        new List<BistroBuilderEmployeeRecord>();

    public event Action<long> ScheduleChanged;
    public event Action ScheduleRestored;

    public long Revision => state != null ? state.revision : 0L;
    public int ShiftCount => state != null && state.shifts != null
        ? state.shifts.Count
        : 0;
    public BistroBuilderStaffScheduleProfile ScheduleProfile => scheduleProfile;

    private void Awake()
    {
        if (state == null)
        {
            state = BistroBuilderStaffScheduleEngine.CreateEmptySnapshot();
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (staffService == null || generalGameStateService == null ||
            serviceStateService == null || scheduleProfile == null)
        {
            error = "Horarios necesita Staff, calendario, estado de servicio y perfil.";
            return false;
        }

        if (!staffService.ValidateConfiguration(out error) ||
            !scheduleProfile.TryValidate(out error) ||
            generalGameStateService.DayIndex < 1)
        {
            if (generalGameStateService.DayIndex < 1 && string.IsNullOrWhiteSpace(error))
            {
                error = "El calendario no expone un DayIndex válido.";
            }
            return false;
        }

        if (state != null &&
            !BistroBuilderStaffScheduleEngine.TryValidateSnapshot(
                state,
                staffService.CreateSnapshot(),
                out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool EnsureReady(out string error)
    {
        if (!ValidateConfiguration(out error))
        {
            return false;
        }
        if (state == null)
        {
            state = BistroBuilderStaffScheduleEngine.CreateEmptySnapshot();
        }
        error = string.Empty;
        return true;
    }

    public bool TrySetScheduled(
        string employeeId,
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        bool scheduled,
        out string error)
    {
        if (!EnsureReady(out error))
        {
            return false;
        }

        if (!serviceStateService.IsClosed)
        {
            error = "Los turnos solo pueden editarse con el restaurante Closed.";
            return false;
        }

        int today = Math.Max(1, generalGameStateService.DayIndex);
        if (dayIndex < today || dayIndex >= today + scheduleProfile.PlanningHorizonDays)
        {
            error = "El día solicitado queda fuera del horizonte de planificación.";
            return false;
        }

        if (!BistroBuilderStaffScheduleEngine.TrySetShift(
                state,
                staffService.CreateSnapshot(),
                scheduleProfile,
                employeeId,
                dayIndex,
                mealService,
                scheduled,
                out BistroBuilderStaffScheduleSnapshot candidate,
                out error))
        {
            return false;
        }

        if (candidate.revision == state.revision)
        {
            error = string.Empty;
            return true;
        }

        state = candidate;
        ScheduleChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    public bool IsScheduled(
        string employeeId,
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService)
    {
        return BistroBuilderStaffScheduleEngine.IsScheduled(
            state,
            employeeId,
            dayIndex,
            mealService);
    }

    public void CopyScheduledEmployeeIds(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        List<string> destination)
    {
        BistroBuilderStaffScheduleEngine.CopyScheduledEmployeeIds(
            state,
            dayIndex,
            mealService,
            destination);
    }

    public bool TryBuildCoverage(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        out BistroBuilderStaffScheduleCoverage coverage,
        out string error)
    {
        coverage = null;
        if (!EnsureReady(out error))
        {
            return false;
        }

        int waiters = 0;
        long salary = 0L;
        employeeBuffer.Clear();
        staffService.CopyEmployees(employeeBuffer, false);
        try
        {
            for (int index = 0; index < employeeBuffer.Count; index++)
            {
                BistroBuilderEmployeeRecord employee = employeeBuffer[index];
                if (employee == null ||
                    !IsScheduled(employee.employeeId, dayIndex, mealService) ||
                    !staffService.TryGetRoleDefinition(
                        employee.roleId,
                        out BistroBuilderStaffRoleDefinition role) ||
                    role == null ||
                    !string.Equals(
                        role.operationalAdapterId,
                        BistroBuilderStaffOperationalAdapterIds.WaiterAgent,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                waiters++;
                salary = checked(salary + employee.salaryCentsPerService);
            }
        }
        catch (OverflowException)
        {
            error = "El coste salarial proyectado desborda el rango monetario.";
            return false;
        }

        coverage = new BistroBuilderStaffScheduleCoverage
        {
            dayIndex = dayIndex,
            mealService = mealService,
            scheduledWaiters = waiters,
            minimumRecommendedWaiters = scheduleProfile.MinimumRecommendedWaiters,
            projectedSalaryCents = salary,
            isSufficient = waiters >= scheduleProfile.MinimumRecommendedWaiters
        };
        error = string.Empty;
        return true;
    }

    public BistroBuilderStaffScheduleSnapshot CreateSnapshot()
    {
        return state != null ? state.DeepClone() : null;
    }

    public bool TryRestoreSnapshot(
        BistroBuilderStaffScheduleSnapshot snapshot,
        out string error)
    {
        if (!ValidateConfiguration(out error) ||
            !BistroBuilderStaffScheduleEngine.TryValidateSnapshot(
                snapshot,
                staffService.CreateSnapshot(),
                out error))
        {
            return false;
        }

        state = snapshot.DeepClone();
        ScheduleRestored?.Invoke();
        ScheduleChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    public bool TryResetForLegacyLoad(out string error)
    {
        if (!ValidateConfiguration(out error))
        {
            return false;
        }
        state = BistroBuilderStaffScheduleEngine.CreateEmptySnapshot();
        ScheduleRestored?.Invoke();
        ScheduleChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    private void CacheDependencies()
    {
        if (staffService == null) staffService = GetComponent<BistroBuilderStaffService>();
        if (generalGameStateService == null)
            generalGameStateService = GetComponent<BistroBuilderGeneralGameStateService>();
        if (serviceStateService == null)
            serviceStateService = GetComponent<RestaurantServiceStateService>();
        if (scheduleProfile == null)
            scheduleProfile = Resources.Load<BistroBuilderStaffScheduleProfile>(
                "BistroBuilder/Staff/StaffScheduleProfile");
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
