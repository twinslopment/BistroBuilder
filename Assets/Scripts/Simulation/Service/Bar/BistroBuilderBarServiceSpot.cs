using System;
using UnityEngine;

/// <summary>
/// Plaza operativa de barra. No es una mesa ni hereda de RestaurantTable.
/// Conserva su propia identidad, puntos de cliente/camarero y ocupación.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Service/Bar Service Spot")]
public sealed class BistroBuilderBarServiceSpot : MonoBehaviour
{
    [Header("Identidad estable")]
    [SerializeField]
    private string barSpotId = "bar_spot_01";

    [Header("Puntos operativos")]
    [SerializeField]
    private Transform customerPoint;

    [SerializeField]
    private Transform waiterServicePoint;

    [Header("Capacidad")]
    [SerializeField, Min(1)]
    private int capacity = 1;

    [SerializeField]
    private bool allowsStandingService;

    [Header("Ocupación runtime")]
    [SerializeField]
    private CustomerGroup assignedCustomerGroup;

    public event Action<
        BistroBuilderBarServiceSpot,
        CustomerGroup,
        CustomerGroup
    > OccupancyChanged;

    public string BarSpotId =>
        BistroBuilderOrderIdUtility.Normalize(barSpotId);

    public Transform CustomerPoint =>
        customerPoint != null ? customerPoint : transform;

    public Transform WaiterServicePoint =>
        waiterServicePoint != null ? waiterServicePoint : transform;

    public int Capacity => Mathf.Max(1, capacity);
    public bool AllowsStandingService => allowsStandingService;
    public CustomerGroup AssignedCustomerGroup => assignedCustomerGroup;
    public bool IsFree => assignedCustomerGroup == null;

    /// <summary>
    /// Indica si esta plaza puede reservar una parte de un grupo. La
    /// capacidad total necesaria se comprueba de forma atómica en el registro,
    /// porque un grupo puede ocupar varias plazas contiguas.
    /// </summary>
    public bool CanHost(CustomerGroup group)
    {
        return group != null && IsFree && Capacity > 0;
    }

    public bool TryOccupy(CustomerGroup group)
    {
        if (!CanHost(group))
        {
            return false;
        }

        CustomerGroup previous = assignedCustomerGroup;
        assignedCustomerGroup = group;
        OccupancyChanged?.Invoke(this, previous, assignedCustomerGroup);

        Debug.Log(
            "Plaza " + BarSpotId + " ocupada por el grupo " +
            group.GroupId + ".",
            this
        );

        return true;
    }

    public bool TryRelease(CustomerGroup group)
    {
        if (assignedCustomerGroup == null ||
            !ReferenceEquals(assignedCustomerGroup, group))
        {
            return false;
        }

        CustomerGroup previous = assignedCustomerGroup;
        assignedCustomerGroup = null;
        OccupancyChanged?.Invoke(this, previous, null);

        Debug.Log(
            "Plaza " + BarSpotId + " liberada.",
            this
        );

        return true;
    }

    public bool ValidateConfiguration(out string error)
    {
        if (!BistroBuilderOrderIdUtility.IsValid(BarSpotId))
        {
            error = name + " no tiene un BarSpotId estable válido.";
            return false;
        }

        if (customerPoint == null)
        {
            error = BarSpotId + " no tiene CustomerPoint.";
            return false;
        }

        if (waiterServicePoint == null)
        {
            error = BarSpotId + " no tiene WaiterServicePoint.";
            return false;
        }

        if (capacity < 1)
        {
            error = BarSpotId + " declara una capacidad inválida.";
            return false;
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        barSpotId = BistroBuilderMenuIdUtility.NormalizeStableId(barSpotId);
        capacity = Mathf.Max(1, capacity);
    }
#endif
}
