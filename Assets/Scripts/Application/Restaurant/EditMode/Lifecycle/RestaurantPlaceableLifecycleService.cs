using System;
using UnityEngine;

/// <summary>
/// Servicio central del ciclo de vida de los artículos colocables.
///
/// Responsabilidades:
/// - Crear una instancia provisional desde una definición de catálogo.
/// - Asignarle una identidad estable.
/// - Activarla y registrarla de forma transaccional.
/// - Retirarla de todos los registros sin destruirla.
/// - Restaurarla conservando identidad, jerarquía, área y pose.
/// - Destruir definitivamente instancias que ya no pueden rehacerse.
///
/// No conoce funciones específicas de mesas, luces, plantas u hornos.
/// Las integraciones funcionales reaccionan a los eventos de
/// RestaurantPlaceableRegistry mediante adaptadores independientes.
///
/// No utiliza Update.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placeable Lifecycle Service"
)]
public sealed class RestaurantPlaceableLifecycleService :
    MonoBehaviour
{
    [Header("Dependencias")]

    [SerializeField]
    private RestaurantPlaceableRegistry
        placeableRegistry;

    [SerializeField]
    private RestaurantAreaMemberRegistry
        memberRegistry;

    [Header("Almacenamiento de instancias retiradas")]

    [Tooltip(
        "Raíz utilizada para conservar instancias inactivas mientras " +
        "todavía puedan recuperarse mediante el historial."
    )]
    [SerializeField]
    private Transform inactivePlaceablesRoot;

    [Tooltip(
        "Crea automáticamente una raíz de almacenamiento si no se " +
        "ha asignado una en el Inspector."
    )]
    [SerializeField]
    private bool createInactiveRootAutomatically = true;

    [Header("Depuración")]

    [SerializeField]
    private bool logLifecycleOperations = true;

    public event Action<RestaurantPlaceableObject>
        ProvisionalInstanceCreated;

    public event Action<RestaurantPlaceableObject>
        InstanceActivated;

    public event Action<RestaurantPlaceableObject>
        InstanceDeactivated;

    public event Action<RestaurantPlaceableObject>
        InstancePermanentlyDestroyed;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    /// <summary>
    /// Crea una instancia visible pero todavía no registrada.
    ///
    /// Este estado provisional permite moverla, rotarla y validarla
    /// antes de que afecte a los sistemas operativos del restaurante.
    /// </summary>
    public bool TryCreateProvisionalInstance(
        RestaurantPlaceableItemDefinition definition,
        Vector3 worldPosition,
        Quaternion worldRotation,
        Transform intendedParent,
        out RestaurantPlaceableObject placeable,
        out RestaurantPlaceableLifecycleResult result
    )
    {
        placeable = null;

        if (definition == null)
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .DefinitionUnavailable,
                    null,
                    "No se ha indicado una definición de artículo."
                );

            return false;
        }

        if (!definition.HasValidPrefab ||
            definition.Prefab == null)
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .PrefabUnavailable,
                    null,
                    definition.DisplayName +
                    " no tiene un prefab válido."
                );

            return false;
        }

        RestaurantPlaceableObject instance =
            Instantiate(
                definition.Prefab,
                worldPosition,
                worldRotation,
                intendedParent
            );

        if (instance == null)
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .InstantiationFailed,
                    null,
                    "Unity no pudo crear la instancia del artículo."
                );

            return false;
        }

        instance.SetItemDefinition(
            definition
        );

        if (!instance.ValidateConfiguration(
                out string configurationError
            ))
        {
            DestroyInstanceObject(
                instance
            );

            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .ConfigurationInvalid,
                    null,
                    configurationError
                );

            return false;
        }

        if (!instance.HasInstanceId)
        {
            instance.AssignInstanceId(
                GenerateUniqueInstanceId()
            );
        }

        if (instance.TryGetComponent(
                out RestaurantAreaMember member
            ))
        {
            member.ClearArea();
        }

        instance.name =
            BuildRuntimeInstanceName(
                definition,
                instance.InstanceId
            );

        Physics.SyncTransforms();

        placeable =
            instance;

        result =
            RestaurantPlaceableLifecycleResult.Success(
                instance,
                "Instancia provisional creada."
            );

        ProvisionalInstanceCreated?.Invoke(
            instance
        );

        LogOperation(
            "Creada instancia provisional " +
            instance.name +
            "."
        );

        return true;
    }

    /// <summary>
    /// Registra una instancia provisional o restaura una retirada.
    ///
    /// El orden es deliberado:
    /// 1. Restaurar pose y jerarquía.
    /// 2. Activar GameObject.
    /// 3. Registrar miembro espacial.
    /// 4. Registrar identidad genérica.
    ///
    /// RestaurantPlacementRegistry reaccionará al alta del miembro.
    /// Los adaptadores funcionales reaccionarán al alta del artículo.
    /// </summary>
    public bool TryActivateInstance(
        RestaurantPlaceableObject placeable,
        RestaurantPlacementStateSnapshot state,
        out RestaurantPlaceableLifecycleResult result
    )
    {
        if (!DependenciesAreAvailable())
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .RegistryUnavailable,
                    placeable,
                    "Los registros del ciclo de vida no están disponibles."
                );

            return false;
        }

        if (placeable == null)
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .InstanceUnavailable,
                    null,
                    "La instancia no está disponible."
                );

            return false;
        }

        if (!placeable.ValidateConfiguration(
                out string configurationError
            ))
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .ConfigurationInvalid,
                    placeable,
                    configurationError
                );

            return false;
        }

        if (!placeable.TryGetComponent(
                out RestaurantAreaMember member
            ))
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .MemberUnavailable,
                    placeable,
                    placeable.name +
                    " no tiene RestaurantAreaMember."
                );

            return false;
        }

        if (!CanActivate(
                placeable,
                out string guardMessage
            ))
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .OperationRejected,
                    placeable,
                    guardMessage
                );

            return false;
        }

        if (!EnsureStableIdentity(
                placeable,
                out string identityError
            ))
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .IdentityConflict,
                    placeable,
                    identityError
                );

            return false;
        }

        if (state.IsValid &&
            !state.Restore(member))
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .StateRestoreFailed,
                    placeable,
                    "No se pudo restaurar el estado espacial."
                );

            return false;
        }

        placeable.gameObject.SetActive(
            true
        );

        bool memberWasAlreadyRegistered =
            memberRegistry.ContainsMember(
                member
            );

        bool placeableWasAlreadyRegistered =
            placeableRegistry.ContainsPlaceable(
                placeable
            );

        if (!memberWasAlreadyRegistered &&
            !memberRegistry.RegisterMember(
                member
            ))
        {
            placeable.gameObject.SetActive(
                false
            );

            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .MemberRegistrationFailed,
                    placeable,
                    "No se pudo registrar el miembro espacial."
                );

            return false;
        }

        if (!placeableWasAlreadyRegistered &&
            !placeableRegistry.RegisterPlaceable(
                placeable
            ))
        {
            if (!memberWasAlreadyRegistered)
            {
                memberRegistry.UnregisterMember(
                    member
                );
            }

            placeable.gameObject.SetActive(
                false
            );

            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .PlaceableRegistrationFailed,
                    placeable,
                    "No se pudo registrar la identidad colocable."
                );

            return false;
        }

        Physics.SyncTransforms();

        result =
            RestaurantPlaceableLifecycleResult.Success(
                placeable,
                "Instancia activada y registrada."
            );

        InstanceActivated?.Invoke(
            placeable
        );

        LogOperation(
            "Activada instancia " +
            placeable.name +
            " [" +
            placeable.InstanceId +
            "]."
        );

        return true;
    }

    /// <summary>
    /// Retira una instancia de todos los registros sin destruirla.
    ///
    /// La captura devuelta permite restaurarla exactamente al deshacer.
    /// </summary>
    public bool TryDeactivateInstance(
        RestaurantPlaceableObject placeable,
        out RestaurantPlacementStateSnapshot state,
        out RestaurantPlaceableLifecycleResult result
    )
    {
        state =
            default;

        if (!DependenciesAreAvailable())
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .RegistryUnavailable,
                    placeable,
                    "Los registros del ciclo de vida no están disponibles."
                );

            return false;
        }

        if (placeable == null)
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .InstanceUnavailable,
                    null,
                    "La instancia no está disponible."
                );

            return false;
        }

        if (!placeable.TryGetComponent(
                out RestaurantAreaMember member
            ))
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .MemberUnavailable,
                    placeable,
                    placeable.name +
                    " no tiene RestaurantAreaMember."
                );

            return false;
        }

        if (!CanDeactivate(
                placeable,
                out string guardMessage
            ))
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .OperationRejected,
                    placeable,
                    guardMessage
                );

            return false;
        }

        state =
            RestaurantPlacementStateSnapshot.Capture(
                member
            );

        bool placeableWasRegistered =
            placeableRegistry.ContainsPlaceable(
                placeable
            );

        bool memberWasRegistered =
            memberRegistry.ContainsMember(
                member
            );

        if (placeableWasRegistered &&
            !placeableRegistry.UnregisterPlaceable(
                placeable
            ))
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .PlaceableUnregistrationFailed,
                    placeable,
                    "No se pudo retirar el artículo del registro."
                );

            return false;
        }

        if (memberWasRegistered &&
            !memberRegistry.UnregisterMember(
                member
            ))
        {
            if (placeableWasRegistered)
            {
                placeableRegistry.RegisterPlaceable(
                    placeable
                );
            }

            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .MemberUnregistrationFailed,
                    placeable,
                    "No se pudo retirar el miembro espacial."
                );

            return false;
        }

        Transform storageRoot =
            GetOrCreateInactiveRoot();

        if (storageRoot != null)
        {
            placeable.transform.SetParent(
                storageRoot,
                true
            );
        }

        placeable.gameObject.SetActive(
            false
        );

        Physics.SyncTransforms();

        result =
            RestaurantPlaceableLifecycleResult.Success(
                placeable,
                "Instancia retirada y conservada para el historial."
            );

        InstanceDeactivated?.Invoke(
            placeable
        );

        LogOperation(
            "Retirada instancia " +
            placeable.name +
            " [" +
            placeable.InstanceId +
            "]."
        );

        return true;
    }

    /// <summary>
    /// Destruye definitivamente una instancia.
    ///
    /// Se utiliza al cancelar una creación provisional o cuando el
    /// comando que retenía una instancia inactiva sale del historial.
    /// </summary>
    public bool TryPermanentlyDestroyInstance(
        RestaurantPlaceableObject placeable,
        out RestaurantPlaceableLifecycleResult result
    )
    {
        if (placeable == null)
        {
            result =
                RestaurantPlaceableLifecycleResult.Failure(
                    RestaurantPlaceableLifecycleFailureReason
                        .InstanceUnavailable,
                    null,
                    "La instancia ya no está disponible."
                );

            return false;
        }

        if (placeableRegistry != null &&
            placeableRegistry.ContainsPlaceable(
                placeable
            ))
        {
            placeableRegistry.UnregisterPlaceable(
                placeable
            );
        }

        if (memberRegistry != null &&
            placeable.TryGetComponent(
                out RestaurantAreaMember member
            ) &&
            memberRegistry.ContainsMember(
                member
            ))
        {
            memberRegistry.UnregisterMember(
                member
            );
        }

        RestaurantPlaceableObject destroyedPlaceable =
            placeable;

        string destroyedName =
            placeable.name;

        DestroyInstanceObject(
            placeable
        );

        result =
            RestaurantPlaceableLifecycleResult.Success(
                destroyedPlaceable,
                "Instancia destruida definitivamente."
            );

        InstancePermanentlyDestroyed?.Invoke(
            destroyedPlaceable
        );

        LogOperation(
            "Destruida definitivamente la instancia " +
            destroyedName +
            "."
        );

        return true;
    }

    public bool IsRegistered(
        RestaurantPlaceableObject placeable
    )
    {
        return placeable != null &&
               placeableRegistry != null &&
               placeableRegistry.ContainsPlaceable(
                   placeable
               );
    }

    private bool EnsureStableIdentity(
        RestaurantPlaceableObject placeable,
        out string errorMessage
    )
    {
        if (placeable == null)
        {
            errorMessage =
                "La instancia no está disponible.";

            return false;
        }

        if (!placeable.HasInstanceId)
        {
            placeable.AssignInstanceId(
                GenerateUniqueInstanceId()
            );
        }

        if (placeableRegistry.TryGetByInstanceId(
                placeable.InstanceId,
                out RestaurantPlaceableObject existing
            ) &&
            !ReferenceEquals(
                existing,
                placeable
            ))
        {
            errorMessage =
                "La identidad " +
                placeable.InstanceId +
                " ya pertenece a otra instancia.";

            return false;
        }

        errorMessage =
            string.Empty;

        return true;
    }

    private string GenerateUniqueInstanceId()
    {
        string candidate;

        do
        {
            candidate =
                Guid.NewGuid()
                    .ToString("N")
                    .ToLowerInvariant();
        }
        while (
            placeableRegistry != null &&
            placeableRegistry.TryGetByInstanceId(
                candidate,
                out _
            )
        );

        return candidate;
    }

    private bool CanActivate(
        RestaurantPlaceableObject placeable,
        out string rejectionMessage
    )
    {
        return EvaluateLifecycleGuards(
            placeable,
            true,
            out rejectionMessage
        );
    }

    private bool CanDeactivate(
        RestaurantPlaceableObject placeable,
        out string rejectionMessage
    )
    {
        return EvaluateLifecycleGuards(
            placeable,
            false,
            out rejectionMessage
        );
    }

    /// <summary>
    /// Permite que componentes funcionales futuros impidan una
    /// activación o retirada insegura sin acoplar este servicio a ellos.
    /// </summary>
    private static bool EvaluateLifecycleGuards(
        RestaurantPlaceableObject placeable,
        bool activating,
        out string rejectionMessage
    )
    {
        rejectionMessage =
            string.Empty;

        if (placeable == null)
        {
            rejectionMessage =
                "La instancia no está disponible.";

            return false;
        }

        MonoBehaviour[] behaviours =
            placeable.GetComponents<MonoBehaviour>();

        for (int index = 0;
             index < behaviours.Length;
             index++)
        {
            MonoBehaviour behaviour =
                behaviours[index];

            if (!(behaviour is
                  IRestaurantPlaceableLifecycleGuard guard))
            {
                continue;
            }

            bool allowed =
                activating
                    ? guard.CanActivate(
                        out rejectionMessage
                    )
                    : guard.CanDeactivate(
                        out rejectionMessage
                    );

            if (!allowed)
            {
                if (string.IsNullOrWhiteSpace(
                        rejectionMessage
                    ))
                {
                    rejectionMessage =
                        placeable.name +
                        " ha rechazado la operación de ciclo de vida.";
                }

                return false;
            }
        }

        return true;
    }

    private Transform GetOrCreateInactiveRoot()
    {
        if (inactivePlaceablesRoot != null)
        {
            return inactivePlaceablesRoot;
        }

        if (!createInactiveRootAutomatically)
        {
            return null;
        }

        GameObject rootObject =
            new GameObject(
                "_InactivePlaceables"
            );

        inactivePlaceablesRoot =
            rootObject.transform;

        inactivePlaceablesRoot.SetParent(
            transform,
            false
        );

        return inactivePlaceablesRoot;
    }

    private static string BuildRuntimeInstanceName(
        RestaurantPlaceableItemDefinition definition,
        string instanceId
    )
    {
        string safeName =
            definition != null
                ? definition.DisplayName
                : "Artículo";

        if (string.IsNullOrWhiteSpace(
                instanceId
            ))
        {
            return safeName;
        }

        int suffixLength =
            Mathf.Min(
                8,
                instanceId.Length
            );

        return
            safeName +
            "_" +
            instanceId.Substring(
                0,
                suffixLength
            );
    }

    private static void DestroyInstanceObject(
        RestaurantPlaceableObject placeable
    )
    {
        if (placeable == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(
                placeable.gameObject
            );
        }
        else
        {
            DestroyImmediate(
                placeable.gameObject
            );
        }
    }

    private bool DependenciesAreAvailable()
    {
        return placeableRegistry != null &&
               memberRegistry != null;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (placeableRegistry == null)
        {
            TryGetComponent(
                out placeableRegistry
            );
        }

        if (memberRegistry == null)
        {
            TryGetComponent(
                out memberRegistry
            );
        }
    }

    private void ValidateDependencies()
    {
        if (placeableRegistry == null)
        {
            Debug.LogError(
                nameof(
                    RestaurantPlaceableLifecycleService
                ) +
                " necesita un " +
                nameof(RestaurantPlaceableRegistry) +
                ".",
                this
            );
        }

        if (memberRegistry == null)
        {
            Debug.LogError(
                nameof(
                    RestaurantPlaceableLifecycleService
                ) +
                " necesita un " +
                nameof(RestaurantAreaMemberRegistry) +
                ".",
                this
            );
        }
    }

    private void LogOperation(
        string message
    )
    {
        if (!logLifecycleOperations)
        {
            return;
        }

        Debug.Log(
            message,
            this
        );
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

/// <summary>
/// Extensión opcional para componentes funcionales que necesiten
/// autorizar o rechazar altas y bajas del modo edición.
/// </summary>
public interface IRestaurantPlaceableLifecycleGuard
{
    bool CanActivate(
        out string rejectionMessage
    );

    bool CanDeactivate(
        out string rejectionMessage
    );
}

/// <summary>
/// Resultado de una operación del ciclo de vida.
/// </summary>
public readonly struct RestaurantPlaceableLifecycleResult
{
    public bool Succeeded
    {
        get;
    }

    public RestaurantPlaceableLifecycleFailureReason
        FailureReason
    {
        get;
    }

    public RestaurantPlaceableObject Placeable
    {
        get;
    }

    public string Message
    {
        get;
    }

    private RestaurantPlaceableLifecycleResult(
        bool succeeded,
        RestaurantPlaceableLifecycleFailureReason failureReason,
        RestaurantPlaceableObject placeable,
        string message
    )
    {
        Succeeded =
            succeeded;

        FailureReason =
            failureReason;

        Placeable =
            placeable;

        Message =
            message ?? string.Empty;
    }

    public static RestaurantPlaceableLifecycleResult Success(
        RestaurantPlaceableObject placeable,
        string message
    )
    {
        return new RestaurantPlaceableLifecycleResult(
            true,
            RestaurantPlaceableLifecycleFailureReason.None,
            placeable,
            message
        );
    }

    public static RestaurantPlaceableLifecycleResult Failure(
        RestaurantPlaceableLifecycleFailureReason failureReason,
        RestaurantPlaceableObject placeable,
        string message
    )
    {
        return new RestaurantPlaceableLifecycleResult(
            false,
            failureReason,
            placeable,
            message
        );
    }
}

/// <summary>
/// Motivos de fallo del ciclo de vida.
/// </summary>
public enum RestaurantPlaceableLifecycleFailureReason
{
    None = 0,
    DefinitionUnavailable = 1,
    PrefabUnavailable = 2,
    InstantiationFailed = 3,
    InstanceUnavailable = 4,
    ConfigurationInvalid = 5,
    RegistryUnavailable = 6,
    MemberUnavailable = 7,
    IdentityConflict = 8,
    StateRestoreFailed = 9,
    MemberRegistrationFailed = 10,
    PlaceableRegistrationFailed = 11,
    MemberUnregistrationFailed = 12,
    PlaceableUnregistrationFailed = 13,
    OperationRejected = 14
}
