using System;
using UnityEngine;

/// <summary>
/// Coordina una operación completa de creación de artículos.
///
/// La creación es atómica:
/// - Instancia un prefab provisional sin registrarlo.
/// - Abre una transacción espacial de tipo CreateNew.
/// - Permite previsualizar, rotar, validar y cancelar.
/// - Al confirmar registra área, huella, identidad y función.
/// - Solo entonces incorpora un comando Create al historial.
///
/// Este servicio no lee teclado, ratón ni UI.
/// No contiene reglas específicas de mesas.
/// No utiliza Update.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placeable Creation Service"
)]
public sealed class RestaurantPlaceableCreationService :
    MonoBehaviour
{
    [Header("Dependencias")]

    [SerializeField]
    private RestaurantPlaceableLifecycleService
        lifecycleService;

    [SerializeField]
    private RestaurantPlacementTransactionService
        transactionService;

    [SerializeField]
    private RestaurantPlacementHistoryService
        historyService;

    [Header("Depuración")]

    [SerializeField]
    private bool logCreationOperations = true;

    private RestaurantPlaceableObject
        activeProvisionalPlaceable;

    private bool isCancellingInternally;

    public event Action<RestaurantPlaceableObject>
        CreationStarted;

    public event Action<RestaurantPlaceableObject>
        CreationCommitted;

    public event Action<RestaurantPlaceableObject>
        CreationCancelled;

    public event Action<
        RestaurantPlaceableObject,
        RestaurantPlaceableCreationResult
    > CreationFailed;

    public bool HasActiveCreation
    {
        get
        {
            return activeProvisionalPlaceable != null;
        }
    }

    public RestaurantPlaceableObject
        ActiveProvisionalPlaceable
    {
        get
        {
            return activeProvisionalPlaceable;
        }
    }

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        SubscribeToTransactionService();
    }

    private void OnDisable()
    {
        CancelOrDestroySafely();
        UnsubscribeFromTransactionService();
    }

    /// <summary>
    /// Crea una instancia provisional y abre su transacción.
    /// </summary>
    public bool TryBeginCreation(
        RestaurantPlaceableItemDefinition definition,
        Vector3 initialWorldPosition,
        Quaternion initialWorldRotation,
        Transform intendedParent,
        out RestaurantPlaceableObject placeable,
        out RestaurantPlaceableCreationResult result
    )
    {
        placeable = null;

        if (!DependenciesAreAvailable())
        {
            result =
                RestaurantPlaceableCreationResult.Failure(
                    RestaurantPlaceableCreationFailureReason
                        .SystemUnavailable,
                    null,
                    default,
                    RestaurantPlacementTransactionFailureReason
                        .None,
                    "El sistema de creación no está disponible."
                );

            return false;
        }

        if (HasActiveCreation ||
            transactionService.HasActiveTransaction)
        {
            result =
                RestaurantPlaceableCreationResult.Failure(
                    RestaurantPlaceableCreationFailureReason
                        .OperationAlreadyActive,
                    activeProvisionalPlaceable,
                    default,
                    RestaurantPlacementTransactionFailureReason
                        .OperationAlreadyActive,
                    "Ya existe una operación de colocación activa."
                );

            return false;
        }

        bool instantiated =
            lifecycleService.TryCreateProvisionalInstance(
                definition,
                initialWorldPosition,
                initialWorldRotation,
                intendedParent,
                out RestaurantPlaceableObject provisional,
                out RestaurantPlaceableLifecycleResult
                    lifecycleResult
            );

        if (!instantiated)
        {
            result =
                RestaurantPlaceableCreationResult.Failure(
                    RestaurantPlaceableCreationFailureReason
                        .ProvisionalCreationFailed,
                    provisional,
                    default,
                    RestaurantPlacementTransactionFailureReason
                        .None,
                    lifecycleResult.Message
                );

            CreationFailed?.Invoke(
                provisional,
                result
            );

            return false;
        }

        if (!provisional.TryGetComponent(
                out RestaurantAreaMember member
            ))
        {
            lifecycleService.TryPermanentlyDestroyInstance(
                provisional,
                out _
            );

            result =
                RestaurantPlaceableCreationResult.Failure(
                    RestaurantPlaceableCreationFailureReason
                        .MemberUnavailable,
                    provisional,
                    default,
                    RestaurantPlacementTransactionFailureReason
                        .InvalidMember,
                    provisional.name +
                    " no tiene RestaurantAreaMember."
                );

            CreationFailed?.Invoke(
                provisional,
                result
            );

            return false;
        }

        bool began =
            transactionService.TryBeginPlacement(
                member,
                RestaurantPlacementTransactionKind.CreateNew,
                out RestaurantPlacementTransactionFailureReason
                    transactionFailure
            );

        if (!began)
        {
            lifecycleService.TryPermanentlyDestroyInstance(
                provisional,
                out _
            );

            result =
                RestaurantPlaceableCreationResult.Failure(
                    RestaurantPlaceableCreationFailureReason
                        .TransactionStartFailed,
                    provisional,
                    default,
                    transactionFailure,
                    "No se pudo iniciar la transacción de creación."
                );

            CreationFailed?.Invoke(
                provisional,
                result
            );

            return false;
        }

        activeProvisionalPlaceable =
            provisional;

        placeable =
            provisional;

        result =
            RestaurantPlaceableCreationResult.Success(
                provisional,
                transactionService.LastValidationResult,
                "Creación provisional iniciada."
            );

        CreationStarted?.Invoke(
            provisional
        );

        LogOperation(
            "Creación provisional iniciada para " +
            provisional.DisplayName +
            " [" +
            provisional.InstanceId +
            "]."
        );

        return true;
    }

    /// <summary>
    /// Confirma la transacción y registra la instancia de manera
    /// atómica en todos los sistemas.
    /// </summary>
    public bool TryCommitActiveCreation(
        out RestaurantPlaceableCreationResult result
    )
    {
        if (!HasActiveCreation)
        {
            result =
                RestaurantPlaceableCreationResult.Failure(
                    RestaurantPlaceableCreationFailureReason
                        .NoActiveCreation,
                    null,
                    default,
                    RestaurantPlacementTransactionFailureReason
                        .NoActiveOperation,
                    "No existe una creación activa."
                );

            return false;
        }

        RestaurantPlaceableObject placeable =
            activeProvisionalPlaceable;

        bool committed =
            transactionService.TryCommitPlacement(
                out RestaurantPlacementValidationResult
                    validationResult,
                out RestaurantPlacementTransactionFailureReason
                    transactionFailure,
                out RestaurantPlacementCommittedChange
                    committedChange
            );

        if (!committed)
        {
            result =
                RestaurantPlaceableCreationResult.Failure(
                    RestaurantPlaceableCreationFailureReason
                        .PlacementCommitFailed,
                    placeable,
                    validationResult,
                    transactionFailure,
                    "La colocación todavía no puede confirmarse."
                );

            CreationFailed?.Invoke(
                placeable,
                result
            );

            return false;
        }

        bool activated =
            lifecycleService.TryActivateInstance(
                placeable,
                committedChange.After,
                out RestaurantPlaceableLifecycleResult
                    lifecycleResult
            );

        if (!activated)
        {
            lifecycleService.TryPermanentlyDestroyInstance(
                placeable,
                out _
            );

            ClearActiveCreation();

            result =
                RestaurantPlaceableCreationResult.Failure(
                    RestaurantPlaceableCreationFailureReason
                        .ActivationFailed,
                    placeable,
                    validationResult,
                    transactionFailure,
                    lifecycleResult.Message
                );

            CreationFailed?.Invoke(
                placeable,
                result
            );

            return false;
        }

        RestaurantCreatePlaceableHistoryCommand command =
            new RestaurantCreatePlaceableHistoryCommand(
                lifecycleService,
                placeable,
                committedChange.After
            );

        bool recorded =
            historyService.TryRecordExecutedCommand(
                command
            );

        if (!recorded)
        {
            lifecycleService.TryDeactivateInstance(
                placeable,
                out _,
                out _
            );

            lifecycleService.TryPermanentlyDestroyInstance(
                placeable,
                out _
            );

            ClearActiveCreation();

            result =
                RestaurantPlaceableCreationResult.Failure(
                    RestaurantPlaceableCreationFailureReason
                        .HistoryRegistrationFailed,
                    placeable,
                    validationResult,
                    transactionFailure,
                    "La creación no pudo incorporarse al historial."
                );

            CreationFailed?.Invoke(
                placeable,
                result
            );

            return false;
        }

        ClearActiveCreation();

        result =
            RestaurantPlaceableCreationResult.Success(
                placeable,
                validationResult,
                "Artículo creado y registrado."
            );

        CreationCommitted?.Invoke(
            placeable
        );

        LogOperation(
            "Creación confirmada para " +
            placeable.DisplayName +
            " [" +
            placeable.InstanceId +
            "]."
        );

        return true;
    }

    /// <summary>
    /// Cancela la creación y destruye la instancia provisional.
    /// </summary>
    public bool TryCancelActiveCreation(
        out RestaurantPlaceableCreationResult result
    )
    {
        if (!HasActiveCreation)
        {
            result =
                RestaurantPlaceableCreationResult.Failure(
                    RestaurantPlaceableCreationFailureReason
                        .NoActiveCreation,
                    null,
                    default,
                    RestaurantPlacementTransactionFailureReason
                        .NoActiveOperation,
                    "No existe una creación activa."
                );

            return false;
        }

        RestaurantPlaceableObject placeable =
            activeProvisionalPlaceable;

        isCancellingInternally =
            true;

        bool transactionCancelled =
            !transactionService.HasActiveTransaction ||
            transactionService.CancelPlacement();

        isCancellingInternally =
            false;

        lifecycleService.TryPermanentlyDestroyInstance(
            placeable,
            out _
        );

        ClearActiveCreation();

        if (!transactionCancelled)
        {
            result =
                RestaurantPlaceableCreationResult.Failure(
                    RestaurantPlaceableCreationFailureReason
                        .CancellationFailed,
                    placeable,
                    default,
                    RestaurantPlacementTransactionFailureReason
                        .NoActiveOperation,
                    "No se pudo cancelar correctamente la transacción."
                );

            CreationFailed?.Invoke(
                placeable,
                result
            );

            return false;
        }

        result =
            RestaurantPlaceableCreationResult.Success(
                placeable,
                default,
                "Creación cancelada."
            );

        CreationCancelled?.Invoke(
            placeable
        );

        LogOperation(
            "Creación cancelada."
        );

        return true;
    }

    /// <summary>
    /// Atiende cancelaciones iniciadas por el modo edición, una regla
    /// operativa o la desactivación del sistema.
    /// </summary>
    private void HandlePlacementCancelled(
        RestaurantAreaMember member
    )
    {
        if (isCancellingInternally ||
            !HasActiveCreation)
        {
            return;
        }

        if (member != null &&
            member.gameObject !=
                activeProvisionalPlaceable.gameObject)
        {
            return;
        }

        RestaurantPlaceableObject placeable =
            activeProvisionalPlaceable;

        lifecycleService.TryPermanentlyDestroyInstance(
            placeable,
            out _
        );

        ClearActiveCreation();

        CreationCancelled?.Invoke(
            placeable
        );

        LogOperation(
            "Creación cancelada por un sistema externo."
        );
    }

    private void CancelOrDestroySafely()
    {
        if (!HasActiveCreation)
        {
            return;
        }

        if (transactionService != null &&
            transactionService.HasActiveTransaction)
        {
            TryCancelActiveCreation(
                out _
            );

            return;
        }

        RestaurantPlaceableObject placeable =
            activeProvisionalPlaceable;

        lifecycleService?.TryPermanentlyDestroyInstance(
            placeable,
            out _
        );

        ClearActiveCreation();
    }

    private void ClearActiveCreation()
    {
        activeProvisionalPlaceable =
            null;

        isCancellingInternally =
            false;
    }

    private void SubscribeToTransactionService()
    {
        if (transactionService == null)
        {
            return;
        }

        transactionService.PlacementCancelled -=
            HandlePlacementCancelled;

        transactionService.PlacementCancelled +=
            HandlePlacementCancelled;
    }

    private void UnsubscribeFromTransactionService()
    {
        if (transactionService == null)
        {
            return;
        }

        transactionService.PlacementCancelled -=
            HandlePlacementCancelled;
    }

    private bool DependenciesAreAvailable()
    {
        return lifecycleService != null &&
               transactionService != null &&
               historyService != null;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (lifecycleService == null)
        {
            TryGetComponent(
                out lifecycleService
            );
        }

        if (transactionService == null)
        {
            TryGetComponent(
                out transactionService
            );
        }

        if (historyService == null)
        {
            TryGetComponent(
                out historyService
            );
        }
    }

    private void ValidateDependencies()
    {
        if (lifecycleService == null)
        {
            Debug.LogError(
                nameof(
                    RestaurantPlaceableCreationService
                ) +
                " necesita un " +
                nameof(
                    RestaurantPlaceableLifecycleService
                ) +
                ".",
                this
            );
        }

        if (transactionService == null)
        {
            Debug.LogError(
                nameof(
                    RestaurantPlaceableCreationService
                ) +
                " necesita un " +
                nameof(
                    RestaurantPlacementTransactionService
                ) +
                ".",
                this
            );
        }

        if (historyService == null)
        {
            Debug.LogError(
                nameof(
                    RestaurantPlaceableCreationService
                ) +
                " necesita un " +
                nameof(
                    RestaurantPlacementHistoryService
                ) +
                ".",
                this
            );
        }
    }

    private void LogOperation(
        string message
    )
    {
        if (!logCreationOperations)
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
/// Resultado completo de una operación de creación.
/// </summary>
public readonly struct RestaurantPlaceableCreationResult
{
    public bool Succeeded
    {
        get;
    }

    public RestaurantPlaceableCreationFailureReason
        FailureReason
    {
        get;
    }

    public RestaurantPlaceableObject Placeable
    {
        get;
    }

    public RestaurantPlacementValidationResult
        ValidationResult
    {
        get;
    }

    public RestaurantPlacementTransactionFailureReason
        TransactionFailureReason
    {
        get;
    }

    public string Message
    {
        get;
    }

    private RestaurantPlaceableCreationResult(
        bool succeeded,
        RestaurantPlaceableCreationFailureReason failureReason,
        RestaurantPlaceableObject placeable,
        RestaurantPlacementValidationResult validationResult,
        RestaurantPlacementTransactionFailureReason
            transactionFailureReason,
        string message
    )
    {
        Succeeded =
            succeeded;

        FailureReason =
            failureReason;

        Placeable =
            placeable;

        ValidationResult =
            validationResult;

        TransactionFailureReason =
            transactionFailureReason;

        Message =
            message ?? string.Empty;
    }

    public static RestaurantPlaceableCreationResult Success(
        RestaurantPlaceableObject placeable,
        RestaurantPlacementValidationResult validationResult,
        string message
    )
    {
        return new RestaurantPlaceableCreationResult(
            true,
            RestaurantPlaceableCreationFailureReason.None,
            placeable,
            validationResult,
            RestaurantPlacementTransactionFailureReason.None,
            message
        );
    }

    public static RestaurantPlaceableCreationResult Failure(
        RestaurantPlaceableCreationFailureReason failureReason,
        RestaurantPlaceableObject placeable,
        RestaurantPlacementValidationResult validationResult,
        RestaurantPlacementTransactionFailureReason
            transactionFailureReason,
        string message
    )
    {
        return new RestaurantPlaceableCreationResult(
            false,
            failureReason,
            placeable,
            validationResult,
            transactionFailureReason,
            message
        );
    }
}

public enum RestaurantPlaceableCreationFailureReason
{
    None = 0,
    SystemUnavailable = 1,
    OperationAlreadyActive = 2,
    ProvisionalCreationFailed = 3,
    MemberUnavailable = 4,
    TransactionStartFailed = 5,
    NoActiveCreation = 6,
    PlacementCommitFailed = 7,
    ActivationFailed = 8,
    HistoryRegistrationFailed = 9,
    CancellationFailed = 10
}
