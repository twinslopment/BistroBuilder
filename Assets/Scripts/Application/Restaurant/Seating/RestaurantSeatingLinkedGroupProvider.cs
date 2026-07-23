using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adaptador de la relación mesa-sillas para el sistema universal de
/// grupos enlazados.
///
/// No modifica padres ni jerarquías. La relación procede exclusivamente
/// de la topología confirmada mesa-plaza-silla, por lo que eliminar,
/// seleccionar, guardar o registrar artículos sigue siendo independiente.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Seating Linked Group Provider"
)]
public sealed class RestaurantSeatingLinkedGroupProvider :
    MonoBehaviour,
    IRestaurantPlacementLinkedGroupProvider
{
    [Header("Dependencias")]

    [SerializeField]
    private RestaurantSeatRegistry seatRegistry;

    [SerializeField]
    private RestaurantSeatingTopologyService topologyService;

    [Header("Proveedor")]

    [SerializeField]
    private bool linkEnabled = true;

    [SerializeField]
    private int priority = 100;

    public int Priority => priority;

    public bool IsLinkEnabled => linkEnabled;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;

        if (seatRegistry == null)
        {
            error =
                name +
                " necesita RestaurantSeatRegistry.";

            return false;
        }

        if (topologyService == null)
        {
            error =
                name +
                " necesita RestaurantSeatingTopologyService.";

            return false;
        }

        return true;
    }

    public void CollectLinkedMembers(
        RestaurantAreaMember rootMember,
        List<RestaurantAreaMember> results
    )
    {
        if (!linkEnabled ||
            rootMember == null ||
            results == null ||
            seatRegistry == null ||
            !rootMember.TryGetComponent(
                out RestaurantTableSeatingConfiguration table
            ))
        {
            return;
        }

        foreach (RestaurantSeat seat
                 in seatRegistry.RegisteredSeats)
        {
            if (seat == null ||
                !seat.gameObject.activeSelf ||
                !seat.IsAssociated ||
                !ReferenceEquals(
                    seat.AssociatedTable,
                    table
                ) ||
                !seat.TryGetComponent(
                    out RestaurantAreaMember member
                ))
            {
                continue;
            }

            results.Add(member);
        }
    }

    public void NotifyLinkedGroupPoseApplied(
        RestaurantAreaMember rootMember,
        IReadOnlyList<RestaurantAreaMember> linkedMembers
    )
    {
        if (!linkEnabled ||
            topologyService == null)
        {
            return;
        }

        /*
         * La relación debería conservarse porque las poses relativas se
         * desplazan como una unidad. Se solicita una reconstrucción para
         * que cualquier consumidor derivado quede sincronizado también
         * después de cancelación, Undo o Redo.
         */
        topologyService.RequestRebuild();
    }

    private void CacheDependenciesIfNeeded()
    {
        if (seatRegistry == null)
        {
            TryGetComponent(out seatRegistry);
        }

        if (topologyService == null)
        {
            TryGetComponent(out topologyService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
