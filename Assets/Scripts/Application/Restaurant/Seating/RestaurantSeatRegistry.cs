using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro central de sillas operativas.
///
/// Se sincroniza con RestaurantPlaceableRegistry y no realiza
/// búsquedas continuas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Seat Registry"
)]
public sealed class RestaurantSeatRegistry :
    MonoBehaviour
{
    [SerializeField]
    private RestaurantPlaceableRegistry placeableRegistry;

    [SerializeField]
    private bool discoverSceneSeatsOnStart = true;

    [SerializeField]
    private bool logStartupSummary = true;

    private readonly HashSet<RestaurantSeat>
        registeredSeats =
            new HashSet<RestaurantSeat>();

    public event Action<RestaurantSeat>
        SeatRegistered;

    public event Action<RestaurantSeat>
        SeatUnregistered;

    public IReadOnlyCollection<RestaurantSeat> RegisteredSeats =>
        registeredSeats;

    public int RegisteredSeatCount =>
        registeredSeats.Count;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();
        SynchronizeRegisteredPlaceables();
    }

    private void Start()
    {
        if (discoverSceneSeatsOnStart)
        {
            DiscoverSceneSeats();
        }

        if (logStartupSummary)
        {
            Debug.Log(
                nameof(RestaurantSeatRegistry) +
                " ha registrado " +
                registeredSeats.Count +
                " silla(s) operativa(s).",
                this
            );
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        registeredSeats.Clear();
    }

    public bool RegisterSeat(RestaurantSeat seat)
    {
        if (seat == null ||
            registeredSeats.Contains(seat))
        {
            return false;
        }

        if (!seat.ValidateConfiguration(out string error))
        {
            Debug.LogError(error, seat);
            return false;
        }

        registeredSeats.Add(seat);
        SeatRegistered?.Invoke(seat);
        return true;
    }

    public bool UnregisterSeat(RestaurantSeat seat)
    {
        if (seat == null ||
            !registeredSeats.Remove(seat))
        {
            return false;
        }

        SeatUnregistered?.Invoke(seat);
        return true;
    }

    private void HandlePlaceableRegistered(
        RestaurantPlaceableObject placeable
    )
    {
        if (placeable != null &&
            placeable.TryGetComponent(
                out RestaurantSeat seat
            ))
        {
            RegisterSeat(seat);
        }
    }

    private void HandlePlaceableUnregistered(
        RestaurantPlaceableObject placeable
    )
    {
        if (placeable != null &&
            placeable.TryGetComponent(
                out RestaurantSeat seat
            ))
        {
            UnregisterSeat(seat);
        }
    }

    private void SynchronizeRegisteredPlaceables()
    {
        if (placeableRegistry == null)
        {
            return;
        }

        foreach (RestaurantPlaceableObject placeable
                 in placeableRegistry.RegisteredPlaceables)
        {
            HandlePlaceableRegistered(placeable);
        }
    }

    private void DiscoverSceneSeats()
    {
        RestaurantSeat[] seats =
            FindObjectsByType<RestaurantSeat>(
                FindObjectsSortMode.InstanceID
            );

        for (int index = 0;
             index < seats.Length;
             index++)
        {
            RegisterSeat(seats[index]);
        }
    }

    private void Subscribe()
    {
        if (placeableRegistry == null)
        {
            return;
        }

        placeableRegistry.PlaceableRegistered -=
            HandlePlaceableRegistered;

        placeableRegistry.PlaceableUnregistered -=
            HandlePlaceableUnregistered;

        placeableRegistry.PlaceableRegistered +=
            HandlePlaceableRegistered;

        placeableRegistry.PlaceableUnregistered +=
            HandlePlaceableUnregistered;
    }

    private void Unsubscribe()
    {
        if (placeableRegistry == null)
        {
            return;
        }

        placeableRegistry.PlaceableRegistered -=
            HandlePlaceableRegistered;

        placeableRegistry.PlaceableUnregistered -=
            HandlePlaceableUnregistered;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (placeableRegistry == null)
        {
            TryGetComponent(out placeableRegistry);
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
