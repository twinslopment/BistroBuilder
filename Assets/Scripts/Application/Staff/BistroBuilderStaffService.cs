using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad canónica de Personal persistente.
///
/// No contiene agentes operativos, GameObjects de camarero, dinero ni lógica
/// de tareas. 4D enlazará EmployeeId con agentes de servicio y 4E persistirá
/// este estado mediante el SaveGame universal.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Staff/Staff Service")]
public sealed class BistroBuilderStaffService : MonoBehaviour
{
    [SerializeField] private BistroBuilderStaffRoleCatalog roleCatalog;

    private BistroBuilderStaffSnapshot state;

    public event Action<BistroBuilderEmployeeRecord> EmployeeCreated;
    public event Action<BistroBuilderEmployeeRecord> EmployeeUpdated;
    public event Action<BistroBuilderEmployeeRecord> EmployeeDismissed;
    public event Action<BistroBuilderEmployeeRecord> AvailabilityChanged;
    public event Action StateRestored;
    public event Action<long> StaffChanged;

    public BistroBuilderStaffRoleCatalog RoleCatalog => roleCatalog;
    public bool IsInitialized => state != null;
    public long Revision => state != null ? state.revision : 0L;
    public int EmployeeCount =>
        state != null && state.employees != null ? state.employees.Count : 0;

    public int ActiveEmployeeCount
    {
        get
        {
            if (state == null || state.employees == null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < state.employees.Count; index++)
            {
                BistroBuilderEmployeeRecord employee = state.employees[index];
                if (employee != null &&
                    employee.employmentStatus == BistroBuilderEmploymentStatus.Active)
                {
                    count++;
                }
            }
            return count;
        }
    }

    public long TotalActiveSalaryCentsPerService
    {
        get
        {
            return TryCalculateTotalActiveSalaryCentsPerService(
                out long total,
                out _)
                ? total
                : long.MaxValue;
        }
    }

    private void Awake()
    {
        if (!TryInitializeFresh(out string error))
        {
            Debug.LogError("4A no pudo inicializar Personal. " + error, this);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        if (roleCatalog == null)
        {
            error = "Personal necesita un catálogo canónico de roles.";
            return false;
        }

        if (!roleCatalog.TryValidate(out error))
        {
            return false;
        }

        if (state != null && !TryValidateExtendedSnapshot(state, out error))
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

        if (state != null)
        {
            error = string.Empty;
            return true;
        }

        return TryInitializeFresh(out error);
    }

    public bool TryInitializeFresh(out string error)
    {
        if (roleCatalog == null)
        {
            error = "Personal no puede inicializarse sin catálogo de roles.";
            return false;
        }
        if (!roleCatalog.TryValidate(out error))
        {
            return false;
        }

        state = BistroBuilderStaffEngine.CreateEmptySnapshot();
        return TryValidateExtendedSnapshot(state, out error);
    }

    public bool TryCreateEmployee(
        BistroBuilderEmployeeCreateRequest request,
        out BistroBuilderEmployeeRecord employee,
        out string error)
    {
        employee = null;
        if (!EnsureReady(out error))
        {
            return false;
        }

        for (int attempt = 0; attempt < 8; attempt++)
        {
            string employeeId = BistroBuilderEmployeeIdUtility.CreateNew();
            if (BistroBuilderStaffEngine.TryFindEmployee(state, employeeId, out _))
            {
                continue;
            }

            if (!BistroBuilderStaffEngine.TryBuildEmployee(
                    employeeId,
                    request,
                    roleCatalog,
                    out BistroBuilderEmployeeRecord built,
                    out error) ||
                !BistroBuilderStaffEngine.TryAppendEmployee(
                    state,
                    built,
                    roleCatalog,
                    out BistroBuilderStaffSnapshot candidate,
                    out error) ||
                !TryValidateExtendedSnapshot(candidate, out error))
            {
                return false;
            }

            state = candidate;
            employee = built.DeepClone();
            EmployeeCreated?.Invoke(employee.DeepClone());
            StaffChanged?.Invoke(state.revision);
            error = string.Empty;
            return true;
        }

        error = "No se pudo generar un EmployeeId único tras varios intentos.";
        return false;
    }

    public bool TryDismissEmployee(
        string employeeId,
        out BistroBuilderEmployeeRecord employee,
        out string error)
    {
        employee = null;
        if (!EnsureReady(out error))
        {
            return false;
        }

        if (!BistroBuilderStaffEmploymentEngine.TryDismissEmployee(
                state,
                employeeId,
                roleCatalog,
                out BistroBuilderStaffSnapshot candidate,
                out BistroBuilderEmployeeRecord dismissed,
                out bool availabilityChanged,
                out error) ||
            !TryValidateExtendedSnapshot(candidate, out error))
        {
            return false;
        }

        state = candidate;
        employee = dismissed.DeepClone();
        EmployeeUpdated?.Invoke(employee.DeepClone());
        if (availabilityChanged)
        {
            AvailabilityChanged?.Invoke(employee.DeepClone());
        }
        EmployeeDismissed?.Invoke(employee.DeepClone());
        StaffChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    public bool TryGetEmployee(
        string employeeId,
        out BistroBuilderEmployeeRecord employee)
    {
        employee = null;
        return state != null &&
               BistroBuilderStaffEngine.TryFindEmployee(
                   state,
                   employeeId,
                   out employee);
    }

    public void CopyEmployees(
        List<BistroBuilderEmployeeRecord> destination,
        bool includeInactive = true)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }
        destination.Clear();
        if (state == null || state.employees == null)
        {
            return;
        }

        for (int index = 0; index < state.employees.Count; index++)
        {
            BistroBuilderEmployeeRecord employee = state.employees[index];
            if (employee == null ||
                (!includeInactive &&
                 employee.employmentStatus != BistroBuilderEmploymentStatus.Active))
            {
                continue;
            }
            destination.Add(employee.DeepClone());
        }
    }

    public void CopyEmployeesByRole(
        string roleId,
        List<BistroBuilderEmployeeRecord> destination,
        bool onlyActive = true)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }
        destination.Clear();
        if (state == null || state.employees == null)
        {
            return;
        }

        string normalizedRole = BistroBuilderStaffStableIdUtility.Normalize(roleId);
        for (int index = 0; index < state.employees.Count; index++)
        {
            BistroBuilderEmployeeRecord employee = state.employees[index];
            if (employee == null ||
                !string.Equals(
                    BistroBuilderStaffStableIdUtility.Normalize(employee.roleId),
                    normalizedRole,
                    StringComparison.Ordinal) ||
                (onlyActive &&
                 employee.employmentStatus != BistroBuilderEmploymentStatus.Active))
            {
                continue;
            }
            destination.Add(employee.DeepClone());
        }
    }

    public bool TrySetAvailability(
        string employeeId,
        BistroBuilderEmployeeAvailability availability,
        out BistroBuilderEmployeeRecord employee,
        out string error)
    {
        employee = null;
        if (!EnsureReady(out error))
        {
            return false;
        }

        if (!BistroBuilderStaffEngine.TrySetAvailability(
                state,
                employeeId,
                availability,
                roleCatalog,
                out BistroBuilderStaffSnapshot candidate,
                out BistroBuilderEmployeeRecord updated,
                out bool changed,
                out error))
        {
            return false;
        }

        if (!changed)
        {
            employee = updated;
            return true;
        }

        if (!TryValidateExtendedSnapshot(candidate, out error))
        {
            return false;
        }

        state = candidate;
        employee = updated.DeepClone();
        EmployeeUpdated?.Invoke(employee.DeepClone());
        AvailabilityChanged?.Invoke(employee.DeepClone());
        StaffChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    public bool TryCalculateTotalActiveSalaryCentsPerService(
        out long totalCents,
        out string error)
    {
        totalCents = 0L;
        if (!EnsureReady(out error))
        {
            return false;
        }

        try
        {
            for (int index = 0; index < state.employees.Count; index++)
            {
                BistroBuilderEmployeeRecord employee = state.employees[index];
                if (employee != null &&
                    employee.employmentStatus == BistroBuilderEmploymentStatus.Active)
                {
                    totalCents = checked(
                        totalCents + employee.salaryCentsPerService);
                }
            }
        }
        catch (OverflowException)
        {
            totalCents = 0L;
            error = "El coste salarial de la plantilla desborda el rango monetario.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetRoleDefinition(
        string roleId,
        out BistroBuilderStaffRoleDefinition role)
    {
        role = null;
        return roleCatalog != null && roleCatalog.TryGetRole(roleId, out role);
    }

    public BistroBuilderStaffSnapshot CreateSnapshot()
    {
        return state != null ? state.DeepClone() : null;
    }

    /// <summary>
    /// Commit interno para mutaciones de dominio posteriores (4C+). Rechaza
    /// snapshots obsoletos y conserva StaffService como única autoridad del
    /// roster, sin disfrazar una mutación normal como restauración Save/Load.
    /// </summary>
    internal bool TryCommitDomainMutation(
        BistroBuilderStaffSnapshot candidate,
        BistroBuilderEmployeeRecord updatedEmployee,
        out string error)
    {
        if (!EnsureReady(out error) || candidate == null || updatedEmployee == null)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "La mutación de Personal no contiene estado/empleado válido.";
            }
            return false;
        }

        long expectedRevision;
        try
        {
            expectedRevision = checked(state.revision + 1L);
        }
        catch (OverflowException)
        {
            error = "La revisión actual de Personal no puede incrementarse.";
            return false;
        }

        if (candidate.revision != expectedRevision ||
            !TryValidateExtendedSnapshot(candidate, out error) ||
            !BistroBuilderStaffEngine.TryFindEmployee(
                candidate,
                updatedEmployee.employeeId,
                out BistroBuilderEmployeeRecord committedEmployee) ||
            committedEmployee == null ||
            committedEmployee.revision != updatedEmployee.revision)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "La mutación de Personal es obsoleta o incoherente.";
            }
            return false;
        }

        state = candidate.DeepClone();
        EmployeeUpdated?.Invoke(committedEmployee.DeepClone());
        StaffChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    public bool TryRestoreSnapshot(
        BistroBuilderStaffSnapshot candidate,
        out string error)
    {
        if (!ValidateConfiguration(out error) ||
            !TryValidateExtendedSnapshot(candidate, out error))
        {
            return false;
        }

        state = candidate.DeepClone();
        StateRestored?.Invoke();
        StaffChanged?.Invoke(state.revision);
        error = string.Empty;
        return true;
    }

    private bool TryValidateExtendedSnapshot(
        BistroBuilderStaffSnapshot snapshot,
        out string error)
    {
        if (!BistroBuilderStaffEngine.TryValidateSnapshot(
                snapshot,
                roleCatalog,
                out error))
        {
            return false;
        }

        for (int index = 0; index < snapshot.employees.Count; index++)
        {
            BistroBuilderEmployeeRecord employee = snapshot.employees[index];
            if (employee == null ||
                !BistroBuilderStaffDevelopmentEngine.TryValidateDevelopmentData(
                    employee.development,
                    out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "El empleado contiene desarrollo 4C inválido.";
                }
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        roleCatalog = Resources.Load<BistroBuilderStaffRoleCatalog>(
            "BistroBuilder/Staff/StaffRoleCatalog");
    }

    private void OnValidate()
    {
        if (roleCatalog == null)
        {
            roleCatalog = Resources.Load<BistroBuilderStaffRoleCatalog>(
                "BistroBuilder/Staff/StaffRoleCatalog");
        }
    }
#endif
}
