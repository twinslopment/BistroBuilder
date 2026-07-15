using UnityEngine;

/// <summary>
/// Adaptador entre RestaurantTableRegistry y los sistemas operativos
/// que ya disponen de métodos públicos de alta y baja.
///
/// Actualmente conecta las mesas con WaiterTaskCoordinator.
/// TableAssignmentSystem se conecta directamente al registro.
///
/// Esta clase evita que RestaurantTableRegistry conozca detalles
/// de camareros, tareas o flujos de servicio.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Table Operational Registration Service"
)]
public sealed class RestaurantTableOperationalRegistrationService :
    MonoBehaviour
{
    [Header("Dependencias")]

    [SerializeField]
    private RestaurantTableRegistry tableRegistry;

    [SerializeField]
    private WaiterTaskCoordinator waiterTaskCoordinator;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        SubscribeToTableRegistry();
        SynchronizeRegisteredTables();
    }

    private void Start()
    {
        SynchronizeRegisteredTables();
    }

    private void OnDisable()
    {
        UnsubscribeFromTableRegistry();
    }

    private void HandleTableRegistered(
        RestaurantTable table
    )
    {
        if (waiterTaskCoordinator == null)
        {
            return;
        }

        waiterTaskCoordinator.RegisterTable(
            table
        );
    }

    private void HandleTableUnregistered(
        RestaurantTable table
    )
    {
        if (waiterTaskCoordinator == null)
        {
            return;
        }

        waiterTaskCoordinator.UnregisterTable(
            table
        );
    }

    private void SynchronizeRegisteredTables()
    {
        if (tableRegistry == null ||
            waiterTaskCoordinator == null)
        {
            return;
        }

        foreach (RestaurantTable table
                 in tableRegistry.RegisteredTables)
        {
            waiterTaskCoordinator.RegisterTable(
                table
            );
        }
    }

    private void SubscribeToTableRegistry()
    {
        if (tableRegistry == null)
        {
            return;
        }

        tableRegistry.TableRegistered -=
            HandleTableRegistered;

        tableRegistry.TableUnregistered -=
            HandleTableUnregistered;

        tableRegistry.TableRegistered +=
            HandleTableRegistered;

        tableRegistry.TableUnregistered +=
            HandleTableUnregistered;
    }

    private void UnsubscribeFromTableRegistry()
    {
        if (tableRegistry == null)
        {
            return;
        }

        tableRegistry.TableRegistered -=
            HandleTableRegistered;

        tableRegistry.TableUnregistered -=
            HandleTableUnregistered;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (tableRegistry == null)
        {
            TryGetComponent(
                out tableRegistry
            );
        }

        if (waiterTaskCoordinator == null)
        {
            TryGetComponent(
                out waiterTaskCoordinator
            );
        }
    }

    private void ValidateDependencies()
    {
        if (tableRegistry == null)
        {
            Debug.LogError(
                nameof(
                    RestaurantTableOperationalRegistrationService
                ) +
                " necesita un " +
                nameof(RestaurantTableRegistry) +
                ".",
                this
            );
        }

        if (waiterTaskCoordinator == null)
        {
            Debug.LogError(
                nameof(
                    RestaurantTableOperationalRegistrationService
                ) +
                " necesita un " +
                nameof(WaiterTaskCoordinator) +
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
