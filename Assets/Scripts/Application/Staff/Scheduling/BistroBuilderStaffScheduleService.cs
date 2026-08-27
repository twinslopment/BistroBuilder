using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad de planificación de horarios y turnos.
/// No modifica StaffService, no crea agentes y no abre/cierra servicios.
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
    public int ShiftCount => state != null && state.shifts != null ? state.shifts.Count : 0;
    public BistroBuilderStaffScheduleProfile ScheduleProfile => scheduleProfile;
    public int CurrentDayIndex => generalGameStateService != null
        ? Math.Max(1, generalGameStateService.DayIndex)
        : 1;

    private void Awake()
    {
        EnsureStateInitialized();
    }

    private void EnsureStateInitialized()
    {
        if (state == null)
            state = BistroBuilderStaffScheduleEngine.CreateEmptySnapshot();
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
            !scheduleProfile.TryValidate(out error) || generalGameStateService.DayIndex < 1)
        {
            if (generalGameStateService.DayIndex < 1 && string.IsNullOrWhiteSpace(error))
                error = "El calendario no expone un DayIndex válido.";
            return false;
        }

        // En Edit Mode StaffService todavía puede no haber ejecutado Awake y,
        // por tanto, no exponer staff.state. La validación de configuración
        // debe seguir siendo estructural y no inicializar otra autoridad.
        if (state != null &&
            !BistroBuilderStaffScheduleEngine.TryValidateStructure(state, out error))
            return false;

        error = string.Empty;
        return true;
    }

    public bool EnsureReady(out string error)
    {
        if (!ValidateConfiguration(out error) ||
            !staffService.EnsureReady(out error))
            return false;

        EnsureStateInitialized();
        BistroBuilderStaffSnapshot staffSnapshot = staffService.CreateSnapshot();
        if (!BistroBuilderStaffScheduleEngine.TryValidateSnapshot(
                state, staffSnapshot, out error))
            return false;

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
        if (!EnsureReady(out error) || !CanEditTarget(dayIndex, mealService, out error))
            return false;

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
            return false;

        return CommitIfChanged(candidate, out error);
    }

    public bool TryReplaceServiceAssignments(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        IReadOnlyList<string> employeeIds,
        out string error)
    {
        if (!EnsureReady(out error) || !CanEditTarget(dayIndex, mealService, out error))
            return false;

        if (!BistroBuilderStaffSchedulePlanner.TryReplaceServiceAssignments(
                state,
                staffService.CreateSnapshot(),
                scheduleProfile,
                dayIndex,
                mealService,
                employeeIds,
                out BistroBuilderStaffScheduleSnapshot candidate,
                out error))
            return false;

        return CommitIfChanged(candidate, out error);
    }

    public bool TryCopyServicePlan(
        int sourceDay,
        BistroBuilderMealServiceAvailability sourceService,
        int targetDay,
        BistroBuilderMealServiceAvailability targetService,
        out string error)
    {
        if (!EnsureReady(out error) ||
            !CanEditTarget(targetDay, targetService, out error))
            return false;

        if (!BistroBuilderStaffSchedulePlanner.TryCopyServicePlan(
                state,
                staffService.CreateSnapshot(),
                scheduleProfile,
                sourceDay,
                sourceService,
                targetDay,
                targetService,
                out BistroBuilderStaffScheduleSnapshot candidate,
                out error))
            return false;

        return CommitIfChanged(candidate, out error);
    }

    public bool TryAutoFillMinimumWaiters(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        out string error)
    {
        if (!EnsureReady(out error) || !CanEditTarget(dayIndex, mealService, out error))
            return false;

        if (!BistroBuilderStaffSchedulePlanner.TryBuildMinimumWaiterPlan(
                state,
                staffService.CreateSnapshot(),
                staffService.RoleCatalog,
                scheduleProfile,
                dayIndex,
                mealService,
                out BistroBuilderStaffScheduleSnapshot candidate,
                out error))
            return false;

        return CommitIfChanged(candidate, out error);
    }

    public bool IsScheduled(
        string employeeId,
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService)
    {
        return BistroBuilderStaffScheduleEngine.IsScheduled(
            state, employeeId, dayIndex, mealService);
    }

    public void CopyScheduledEmployeeIds(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        List<string> destination)
    {
        BistroBuilderStaffScheduleEngine.CopyScheduledEmployeeIds(
            state, dayIndex, mealService, destination);
    }

    public bool TryBuildCoverage(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        out BistroBuilderStaffScheduleCoverage coverage,
        out string error)
    {
        coverage = null;
        if (!EnsureReady(out error)) return false;

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
                    employee.availability != BistroBuilderEmployeeAvailability.Available ||
                    !IsScheduled(employee.employeeId, dayIndex, mealService) ||
                    !staffService.TryGetRoleDefinition(
                        employee.roleId, out BistroBuilderStaffRoleDefinition role) ||
                    role == null ||
                    !string.Equals(
                        role.operationalAdapterId,
                        BistroBuilderStaffOperationalAdapterIds.WaiterAgent,
                        StringComparison.Ordinal))
                    continue;

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
        EnsureStateInitialized();
        return state.DeepClone();
    }

    public bool TryRestoreSnapshot(BistroBuilderStaffScheduleSnapshot snapshot, out string error)
    {
        if (!ValidateConfiguration(out error) ||
            !BistroBuilderStaffScheduleEngine.TryValidateSnapshot(
                snapshot, staffService.CreateSnapshot(), out error))
            return false;

        state = snapshot.DeepClone();
        ScheduleRestored?.Invoke();
        ScheduleChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    public bool TryResetForLegacyLoad(out string error)
    {
        if (!ValidateConfiguration(out error)) return false;
        state = BistroBuilderStaffScheduleEngine.CreateEmptySnapshot();
        ScheduleRestored?.Invoke();
        ScheduleChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    private bool CanEditTarget(
        int dayIndex,
        BistroBuilderMealServiceAvailability mealService,
        out string error)
    {
        if (!serviceStateService.IsClosed)
        {
            error = "Los turnos solo pueden editarse con el restaurante Closed.";
            return false;
        }

        int today = CurrentDayIndex;
        if (dayIndex < today || dayIndex >= today + scheduleProfile.PlanningHorizonDays)
        {
            error = "El día solicitado queda fuera del horizonte de planificación.";
            return false;
        }

        if (!scheduleProfile.TryGetDefaultWindow(mealService, out _, out _))
        {
            error = "El servicio no tiene ventana horaria V1 configurable.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool CommitIfChanged(
        BistroBuilderStaffScheduleSnapshot candidate,
        out string error)
    {
        if (candidate == null)
        {
            error = "La planificación no produjo un snapshot válido.";
            return false;
        }
        if (state != null && candidate.revision == state.revision)
        {
            error = string.Empty;
            return true;
        }
        if (!BistroBuilderStaffScheduleEngine.TryValidateSnapshot(
                candidate, staffService.CreateSnapshot(), out error))
            return false;

        state = candidate.DeepClone();
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
