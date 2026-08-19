using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderStaffRoleDefinition
{
    public string roleId = string.Empty;
    public string displayName = string.Empty;
    public bool active = true;

    /// <summary>
    /// Adaptador operativo que puede representar este rol durante un servicio.
    /// El núcleo Employee no conoce la clase concreta del agente.
    /// </summary>
    public string operationalAdapterId = string.Empty;

    public BistroBuilderStaffRoleDefinition DeepClone()
    {
        return (BistroBuilderStaffRoleDefinition)MemberwiseClone();
    }
}

/// <summary>
/// Catálogo de roles dirigido por datos. V1 instala solo Camarero/a, pero
/// nuevos roles pueden añadirse como datos sin modificar el núcleo Employee.
/// </summary>
[CreateAssetMenu(
    menuName = "Bistro Builder/Staff/Role Catalog",
    fileName = "StaffRoleCatalog")]
public sealed class BistroBuilderStaffRoleCatalog : ScriptableObject
{
    [SerializeField]
    private List<BistroBuilderStaffRoleDefinition> roles =
        new List<BistroBuilderStaffRoleDefinition>();

    public IReadOnlyList<BistroBuilderStaffRoleDefinition> Roles => roles;

    public void InitializeV1DefaultsIfEmpty()
    {
        if (roles != null && roles.Count > 0)
        {
            return;
        }

        roles = new List<BistroBuilderStaffRoleDefinition>
        {
            new BistroBuilderStaffRoleDefinition
            {
                roleId = "waiter",
                displayName = "Camarero/a",
                active = true,
                operationalAdapterId =
                    BistroBuilderStaffOperationalAdapterIds.WaiterAgent
            }
        };
    }

    public bool TryGetRole(
        string roleId,
        out BistroBuilderStaffRoleDefinition role)
    {
        role = null;
        string normalized = BistroBuilderStaffStableIdUtility.Normalize(roleId);
        if (!BistroBuilderStaffStableIdUtility.IsValid(normalized) || roles == null)
        {
            return false;
        }

        for (int index = 0; index < roles.Count; index++)
        {
            BistroBuilderStaffRoleDefinition current = roles[index];
            if (current != null &&
                string.Equals(
                    BistroBuilderStaffStableIdUtility.Normalize(current.roleId),
                    normalized,
                    StringComparison.Ordinal))
            {
                role = current.DeepClone();
                return true;
            }
        }
        return false;
    }

    public void CopyRoles(List<BistroBuilderStaffRoleDefinition> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }
        destination.Clear();
        if (roles == null)
        {
            return;
        }

        for (int index = 0; index < roles.Count; index++)
        {
            if (roles[index] != null)
            {
                destination.Add(roles[index].DeepClone());
            }
        }
    }

    public bool TryValidate(out string error)
    {
        if (roles == null || roles.Count == 0)
        {
            error = "El catálogo de Personal necesita al menos un rol.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < roles.Count; index++)
        {
            BistroBuilderStaffRoleDefinition role = roles[index];
            if (role == null)
            {
                error = "El catálogo contiene un rol nulo.";
                return false;
            }

            string roleId = BistroBuilderStaffStableIdUtility.Normalize(role.roleId);
            string adapterId =
                BistroBuilderStaffStableIdUtility.Normalize(role.operationalAdapterId);
            if (!BistroBuilderStaffStableIdUtility.IsValid(roleId) ||
                string.IsNullOrWhiteSpace(role.displayName) ||
                !BistroBuilderStaffStableIdUtility.IsValidOptional(adapterId) ||
                !ids.Add(roleId))
            {
                error = "El rol de Personal " + index + " no es válido o está duplicado.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
