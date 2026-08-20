using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fachada Presentation 5E. Todas las mutaciones se delegan en
/// StaffScheduleService; no persiste ni decide gameplay.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Staff/Staff Schedule Player Facade")]
public sealed class BistroBuilderStaffSchedulePlayerFacade : MonoBehaviour
{
    [SerializeField] private BistroBuilderStaffScheduleService scheduleService;
    [SerializeField] private BistroBuilderStaffService staffService;

    private readonly List<BistroBuilderEmployeeRecord> employees =
        new List<BistroBuilderEmployeeRecord>();

    public event Action ViewInvalidated;

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
    }

    private void OnDisable() => Unsubscribe();

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (scheduleService == null || staffService == null)
        {
            error = "5E necesita ScheduleService y StaffService.";
            return false;
        }
        if (!scheduleService.ValidateConfiguration(out error) ||
            !staffService.ValidateConfiguration(out error))
            return false;
        error = string.Empty;
        return true;
    }

    public bool TryBuildSnapshot(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        out BistroBuilderStaffSchedulePlayerSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        if (!ValidateConfiguration(out error) ||
            !scheduleService.TryBuildCoverage(dayIndex, mealService,
                out BistroBuilderStaffScheduleCoverage coverage, out error))
            return false;

        var built = new BistroBuilderStaffSchedulePlayerSnapshot
        {
            dayIndex = dayIndex,
            mealService = mealService,
            horizonDays = scheduleService.ScheduleProfile.PlanningHorizonDays,
            coverage = coverage
        };

        employees.Clear();
        staffService.CopyEmployees(employees, false);
        employees.Sort((left, right) => string.Compare(
            left?.FullName,
            right?.FullName,
            StringComparison.OrdinalIgnoreCase));

        for (int index = 0; index < employees.Count; index++)
        {
            BistroBuilderEmployeeRecord employee = employees[index];
            if (employee == null ||
                !staffService.TryGetRoleDefinition(
                    employee.roleId,
                    out BistroBuilderStaffRoleDefinition role) ||
                role == null ||
                !string.Equals(
                    role.operationalAdapterId,
                    BistroBuilderStaffOperationalAdapterIds.WaiterAgent,
                    StringComparison.Ordinal))
                continue;

            built.employees.Add(new BistroBuilderStaffSchedulePlayerRow
            {
                employeeId = employee.employeeId,
                displayName = employee.FullName,
                roleName = role.displayName,
                salaryCentsPerService = employee.salaryCentsPerService,
                available = employee.availability == BistroBuilderEmployeeAvailability.Available,
                scheduled = scheduleService.IsScheduled(
                    employee.employeeId, dayIndex, mealService)
            });
        }

        snapshot = built;
        error = string.Empty;
        return true;
    }

    public bool TryToggleEmployee(
        string employeeId,
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        out string error)
    {
        if (!ValidateConfiguration(out error)) return false;
        bool next = !scheduleService.IsScheduled(employeeId, dayIndex, mealService);
        return scheduleService.TrySetScheduled(employeeId, dayIndex, mealService, next, out error);
    }

    public bool TryAutoFill(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        out string error)
    {
        if (!ValidateConfiguration(out error)) return false;
        return scheduleService.TryAutoFillMinimumWaiters(dayIndex, mealService, out error);
    }

    public bool TryCopyPreviousDay(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        out string error)
    {
        if (!ValidateConfiguration(out error)) return false;
        if (dayIndex <= scheduleService.CurrentDayIndex)
        {
            error = "El primer día del horizonte no tiene un día anterior editable para copiar.";
            return false;
        }
        return scheduleService.TryCopyServicePlan(
            dayIndex - 1, mealService, dayIndex, mealService, out error);
    }

    public int CurrentDayIndex => scheduleService != null ? scheduleService.CurrentDayIndex : 1;
    public int PlanningHorizonDays => scheduleService != null && scheduleService.ScheduleProfile != null
        ? scheduleService.ScheduleProfile.PlanningHorizonDays
        : 1;

    private void Subscribe()
    {
        Unsubscribe();
        if (scheduleService != null) scheduleService.ScheduleChanged += HandleChanged;
        if (staffService != null) staffService.StaffChanged += HandleChanged;
    }

    private void Unsubscribe()
    {
        if (scheduleService != null) scheduleService.ScheduleChanged -= HandleChanged;
        if (staffService != null) staffService.StaffChanged -= HandleChanged;
    }

    private void HandleChanged(long _) => ViewInvalidated?.Invoke();

    private void CacheDependencies()
    {
        if (scheduleService == null) TryGetComponent(out scheduleService);
        if (staffService == null) TryGetComponent(out staffService);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
