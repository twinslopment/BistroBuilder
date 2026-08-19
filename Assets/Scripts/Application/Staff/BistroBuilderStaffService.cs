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
    [SerializeField]
    private BistroBuilderStaffRoleCatalog roleCatalog;

    private BistroBuilderStaffSnapshot state;

    public event Action<BistroBuilderEmployeeRecord> EmployeeCreated;
    public event Action<BistroBuilderEmployeeRecord> EmployeeUpdated;
    public event Action<BistroBuilderEmployeeRecord> AvailabilityChanged;
    public event Action StateRestored;
    public event Action<long> StaffChanged;

    public BistroBuilderStaffRoleCatalog RoleCatalog => roleCatalog;
    public bool IsInitialized => state != null;
    public long Revision => state != null ? state.revision : 0L;
    public int EmployeeCount =>
        state != null && state.employees != null ? state.employees.Count : 0;

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
            error = "4A necesita un catálogo canónico de roles.";
            return false;
        }

        if (!roleCatalog.TryValidate(out error))
        {
            return false;
        }

        if (state != null &&
            !BistroBuilderStaffEngine.TryValidateSnapshot(
                state,
                roleCatalog,
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

        if (state != null)
        {
            error = string.Empty;
            return true;
        }

        return TryInitializeFresh(out error);
    }

    public bool TryInitializeFresh(out string error)
    {
        if (roleCatalog == null || !roleCatalog.TryValidate(out error))
        {
            if (roleCatalog == null)
            {
                error = "4A no puede inicializarse sin catálogo de roles.";
            }
            return false;
        }

        state = BistroBuilderStaffEngine.CreateEmptySnapshot();
        return BistroBuilderStaffEngine.TryValidateSnapshot(
            state,
            roleCatalog,
            out error);
    }

    /// <summary>
    /// Crea una identidad nueva en la autoridad de Personal. 4B será quien
    /// determine si una petición procede de contratación de candidato.
    /// </summary>
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
            if (BistroBuilderStaffEngine.TryFindEmployee(
                    state,
                    employeeId,
                    out _))
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
                    out error))
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

        state = candidate;
        employee = updated.DeepClone();
        EmployeeUpdated?.Invoke(employee.DeepClone());
        AvailabilityChanged?.Invoke(employee.DeepClone());
        StaffChanged?.Invoke(state.revision);
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
    /// Punto de restauración que utilizará 4E. La validación ocurre antes de
    /// sustituir el estado, de modo que un snapshot corrupto no altera roster.
    /// </summary>
    public bool TryRestoreSnapshot(
        BistroBuilderStaffSnapshot candidate,
        out string error)
    {
        if (!ValidateConfiguration(out error) ||
            !BistroBuilderStaffEngine.TryValidateSnapshot(
                candidate,
                roleCatalog,
                out error))
        {
            return false;
        }

        state = candidate.DeepClone();
        StateRestored?.Invoke();
        StaffChanged?.Invoke(state.revision);
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
