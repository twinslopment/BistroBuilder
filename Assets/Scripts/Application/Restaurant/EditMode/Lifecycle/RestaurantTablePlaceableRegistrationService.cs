using UnityEngine;

/// <summary>
/// Adaptador entre el registro genérico de artículos colocables y el
/// registro funcional de mesas.
///
/// Gracias a este componente, RestaurantPlaceableLifecycleService no
/// necesita conocer RestaurantTable. Una lámpara, planta u horno
/// utilizarán adaptadores equivalentes para sus propios sistemas.
///
/// No utiliza Update.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Table Placeable Registration Service"
)]
public sealed class RestaurantTablePlaceableRegistrationService :
    MonoBehaviour
{
    [Header("Dependencias")]

    [SerializeField]
    private RestaurantPlaceableRegistry
        placeableRegistry;

    [SerializeField]
    private RestaurantTableRegistry
        tableRegistry;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        SubscribeToPlaceableRegistry();
        SynchronizeExistingPlaceables();
    }

    private void Start()
    {
        SynchronizeExistingPlaceables();
    }

    private void OnDisable()
    {
        UnsubscribeFromPlaceableRegistry();
    }

    private void HandlePlaceableRegistered(
        RestaurantPlaceableObject placeable
    )
    {
        if (placeable == null ||
            tableRegistry == null)
        {
            return;
        }

        if (placeable.TryGetComponent(
                out RestaurantTable table
            ))
        {
            tableRegistry.RegisterTable(
                table
            );
        }
    }

    private void HandlePlaceableUnregistered(
        RestaurantPlaceableObject placeable
    )
    {
        if (placeable == null ||
            tableRegistry == null)
        {
            return;
        }

        if (placeable.TryGetComponent(
                out RestaurantTable table
            ))
        {
            tableRegistry.UnregisterTable(
                table
            );
        }
    }

    private void SynchronizeExistingPlaceables()
    {
        if (placeableRegistry == null ||
            tableRegistry == null)
        {
            return;
        }

        foreach (RestaurantPlaceableObject placeable
                 in placeableRegistry.RegisteredPlaceables)
        {
            HandlePlaceableRegistered(
                placeable
            );
        }
    }

    private void SubscribeToPlaceableRegistry()
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

    private void UnsubscribeFromPlaceableRegistry()
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
            TryGetComponent(
                out placeableRegistry
            );
        }

        if (tableRegistry == null)
        {
            TryGetComponent(
                out tableRegistry
            );
        }
    }

    private void ValidateDependencies()
    {
        if (placeableRegistry == null)
        {
            Debug.LogError(
                nameof(
                    RestaurantTablePlaceableRegistrationService
                ) +
                " necesita un " +
                nameof(RestaurantPlaceableRegistry) +
                ".",
                this
            );
        }

        if (tableRegistry == null)
        {
            Debug.LogError(
                nameof(
                    RestaurantTablePlaceableRegistrationService
                ) +
                " necesita un " +
                nameof(RestaurantTableRegistry) +
                ".",
                this
            );
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
