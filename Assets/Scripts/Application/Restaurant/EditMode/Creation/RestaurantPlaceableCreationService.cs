using System;
using UnityEngine;

/// <summary>
/// Coordina la creación atómica de artículos colocables. El servicio no
/// conoce Finanzas; 3F puede enlazar un IRestaurantPlaceableEconomyGate.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Placeable Creation Service")]
public sealed class RestaurantPlaceableCreationService : MonoBehaviour
{
    [SerializeField]
    private RestaurantPlaceableLifecycleService lifecycleService;

    [SerializeField]
    private RestaurantPlacementTransactionService transactionService;

    [SerializeField]
    private RestaurantPlacementHistoryService historyService;

    [SerializeField]
    private bool logCreationOperations = true;

    private RestaurantPlaceableObject activeProvisionalPlaceable;
    private IRestaurantPlaceableEconomyGate economyGate;
    private bool isCancellingInternally;

    public event Action<RestaurantPlaceableObject> CreationStarted;
    public event Action<RestaurantPlaceableObject> CreationCommitted;
    public event Action<RestaurantPlaceableObject> CreationCancelled;
    public event Action<RestaurantPlaceableObject, RestaurantPlaceableCreationResult>
        CreationFailed;

    public bool HasActiveCreation => activeProvisionalPlaceable != null;
    public RestaurantPlaceableObject ActiveProvisionalPlaceable =>
        activeProvisionalPlaceable;

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

    public bool TryBindEconomyGate(
        IRestaurantPlaceableEconomyGate gate,
        out string error)
    {
        if (gate == null)
        {
            error = "La regla económica indicada es nula.";
            return false;
        }

        if (economyGate != null && !ReferenceEquals(economyGate, gate))
        {
            error = "Ya existe una regla económica enlazada a la creación.";
            return false;
        }

        economyGate = gate;
        error = string.Empty;
        return true;
    }

    public void UnbindEconomyGate(IRestaurantPlaceableEconomyGate gate)
    {
        if (ReferenceEquals(economyGate, gate))
        {
            economyGate = null;
        }
    }

    public bool TryBeginCreation(
        RestaurantPlaceableItemDefinition definition,
        Vector3 initialWorldPosition,
        Quaternion initialWorldRotation,
        Transform intendedParent,
        out RestaurantPlaceableObject placeable,
        out RestaurantPlaceableCreationResult result)
    {
        placeable = null;

        if (!DependenciesAreAvailable())
        {
            result = Failure(
                RestaurantPlaceableCreationFailureReason.SystemUnavailable,
                null,
                "El sistema de creación no está disponible.");
            return false;
        }

        if (HasActiveCreation || transactionService.HasActiveTransaction)
        {
            result = Failure(
                RestaurantPlaceableCreationFailureReason.OperationAlreadyActive,
                activeProvisionalPlaceable,
                "Ya existe una operación de colocación activa.",
                RestaurantPlacementTransactionFailureReason.OperationAlreadyActive);
            return false;
        }

        if (!lifecycleService.TryCreateProvisionalInstance(
                definition,
                initialWorldPosition,
                initialWorldRotation,
                intendedParent,
                out RestaurantPlaceableObject provisional,
                out RestaurantPlaceableLifecycleResult lifecycleResult))
        {
            result = Failure(
                RestaurantPlaceableCreationFailureReason.ProvisionalCreationFailed,
                provisional,
                lifecycleResult.Message);
            CreationFailed?.Invoke(provisional, result);
            return false;
        }

        if (!provisional.TryGetComponent(out RestaurantAreaMember member))
        {
            lifecycleService.TryPermanentlyDestroyInstance(provisional, out _);
            result = Failure(
                RestaurantPlaceableCreationFailureReason.MemberUnavailable,
                provisional,
                provisional.name + " no tiene RestaurantAreaMember.",
                RestaurantPlacementTransactionFailureReason.InvalidMember);
            CreationFailed?.Invoke(provisional, result);
            return false;
        }

        if (!transactionService.TryBeginPlacement(
                member,
                RestaurantPlacementTransactionKind.CreateNew,
                out RestaurantPlacementTransactionFailureReason transactionFailure))
        {
            lifecycleService.TryPermanentlyDestroyInstance(provisional, out _);
            result = Failure(
                RestaurantPlaceableCreationFailureReason.TransactionStartFailed,
                provisional,
                "No se pudo iniciar la transacción de creación.",
                transactionFailure);
            CreationFailed?.Invoke(provisional, result);
            return false;
        }

        activeProvisionalPlaceable = provisional;
        placeable = provisional;
        result = RestaurantPlaceableCreationResult.Success(
            provisional,
            transactionService.LastValidationResult,
            "Creación provisional iniciada.");

        CreationStarted?.Invoke(provisional);
        LogOperation(
            "Creación provisional iniciada para " + provisional.DisplayName + ".");
        return true;
    }

    public bool TryCommitActiveCreation(
        out RestaurantPlaceableCreationResult result)
    {
        if (!HasActiveCreation)
        {
            result = Failure(
                RestaurantPlaceableCreationFailureReason.NoActiveCreation,
                null,
                "No existe una creación activa.",
                RestaurantPlacementTransactionFailureReason.NoActiveOperation);
            return false;
        }

        RestaurantPlaceableObject placeable = activeProvisionalPlaceable;

        if (economyGate != null &&
            !economyGate.TryAuthorizeCreation(placeable, out string authorizationError))
        {
            result = Failure(
                RestaurantPlaceableCreationFailureReason.EconomyRejected,
                placeable,
                authorizationError);
            CreationFailed?.Invoke(placeable, result);
            return false;
        }

        if (!transactionService.TryCommitPlacement(
                out RestaurantPlacementValidationResult validationResult,
                out RestaurantPlacementTransactionFailureReason transactionFailure,
                out RestaurantPlacementCommittedChange committedChange))
        {
            result = RestaurantPlaceableCreationResult.Failure(
                RestaurantPlaceableCreationFailureReason.PlacementCommitFailed,
                placeable,
                validationResult,
                transactionFailure,
                "La colocación todavía no puede confirmarse.");
            CreationFailed?.Invoke(placeable, result);
            return false;
        }

        if (!lifecycleService.TryActivateInstance(
                placeable,
                committedChange.After,
                out RestaurantPlaceableLifecycleResult lifecycleResult))
        {
            lifecycleService.TryPermanentlyDestroyInstance(placeable, out _);
            ClearActiveCreation();
            result = RestaurantPlaceableCreationResult.Failure(
                RestaurantPlaceableCreationFailureReason.ActivationFailed,
                placeable,
                validationResult,
                transactionFailure,
                lifecycleResult.Message);
            CreationFailed?.Invoke(placeable, result);
            return false;
        }

        string economyError = string.Empty;
        bool economyCommitted = economyGate == null ||
            economyGate.TryCommitCreation(placeable, out economyError);
        if (!economyCommitted)
        {
            lifecycleService.TryDeactivateInstance(placeable, out _, out _);
            lifecycleService.TryPermanentlyDestroyInstance(placeable, out _);
            ClearActiveCreation();
            result = RestaurantPlaceableCreationResult.Failure(
                RestaurantPlaceableCreationFailureReason.EconomyCommitFailed,
                placeable,
                validationResult,
                transactionFailure,
                economyError);
            CreationFailed?.Invoke(placeable, result);
            return false;
        }

        var command = new RestaurantCreatePlaceableHistoryCommand(
            lifecycleService,
            placeable,
            committedChange.After);

        if (!historyService.TryRecordExecutedCommand(command))
        {
            string rollbackError = string.Empty;
            bool economyRolledBack = economyGate == null ||
                economyGate.TryRollbackCreation(placeable, out rollbackError);

            lifecycleService.TryDeactivateInstance(placeable, out _, out _);
            lifecycleService.TryPermanentlyDestroyInstance(placeable, out _);
            ClearActiveCreation();

            string message = "La creación no pudo incorporarse al historial.";
            if (!economyRolledBack)
            {
                message += " Además falló la reversión financiera: " + rollbackError;
                Debug.LogError(message, this);
            }

            result = RestaurantPlaceableCreationResult.Failure(
                RestaurantPlaceableCreationFailureReason.HistoryRegistrationFailed,
                placeable,
                validationResult,
                transactionFailure,
                message);
            CreationFailed?.Invoke(placeable, result);
            return false;
        }

        ClearActiveCreation();
        result = RestaurantPlaceableCreationResult.Success(
            placeable,
            validationResult,
            "Artículo creado y registrado.");
        CreationCommitted?.Invoke(placeable);
        LogOperation(
            "Creación confirmada para " + placeable.DisplayName +
            " [" + placeable.InstanceId + "].");
        return true;
    }

    public bool TryCancelActiveCreation(
        out RestaurantPlaceableCreationResult result)
    {
        if (!HasActiveCreation)
        {
            result = Failure(
                RestaurantPlaceableCreationFailureReason.NoActiveCreation,
                null,
                "No existe una creación activa.",
                RestaurantPlacementTransactionFailureReason.NoActiveOperation);
            return false;
        }

        RestaurantPlaceableObject placeable = activeProvisionalPlaceable;
        isCancellingInternally = true;
        bool cancelled = !transactionService.HasActiveTransaction ||
                         transactionService.CancelPlacement();
        isCancellingInternally = false;

        lifecycleService.TryPermanentlyDestroyInstance(placeable, out _);
        ClearActiveCreation();

        if (!cancelled)
        {
            result = Failure(
                RestaurantPlaceableCreationFailureReason.CancellationFailed,
                placeable,
                "No se pudo cancelar correctamente la transacción.",
                RestaurantPlacementTransactionFailureReason.NoActiveOperation);
            CreationFailed?.Invoke(placeable, result);
            return false;
        }

        result = RestaurantPlaceableCreationResult.Success(
            placeable,
            default,
            "Creación cancelada.");
        CreationCancelled?.Invoke(placeable);
        LogOperation("Creación cancelada.");
        return true;
    }

    private void HandlePlacementCancelled(RestaurantAreaMember member)
    {
        if (isCancellingInternally || !HasActiveCreation)
        {
            return;
        }

        if (member != null &&
            member.gameObject != activeProvisionalPlaceable.gameObject)
        {
            return;
        }

        RestaurantPlaceableObject placeable = activeProvisionalPlaceable;
        lifecycleService.TryPermanentlyDestroyInstance(placeable, out _);
        ClearActiveCreation();
        CreationCancelled?.Invoke(placeable);
        LogOperation("Creación cancelada por un sistema externo.");
    }

    private void CancelOrDestroySafely()
    {
        if (!HasActiveCreation)
        {
            return;
        }

        if (transactionService != null && transactionService.HasActiveTransaction)
        {
            TryCancelActiveCreation(out _);
            return;
        }

        RestaurantPlaceableObject placeable = activeProvisionalPlaceable;
        lifecycleService?.TryPermanentlyDestroyInstance(placeable, out _);
        ClearActiveCreation();
    }

    private void ClearActiveCreation()
    {
        activeProvisionalPlaceable = null;
        isCancellingInternally = false;
    }

    private void SubscribeToTransactionService()
    {
        if (transactionService == null)
        {
            return;
        }

        transactionService.PlacementCancelled -= HandlePlacementCancelled;
        transactionService.PlacementCancelled += HandlePlacementCancelled;
    }

    private void UnsubscribeFromTransactionService()
    {
        if (transactionService != null)
        {
            transactionService.PlacementCancelled -= HandlePlacementCancelled;
        }
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
            TryGetComponent(out lifecycleService);
        }

        if (transactionService == null)
        {
            TryGetComponent(out transactionService);
        }

        if (historyService == null)
        {
            TryGetComponent(out historyService);
        }
    }

    private void ValidateDependencies()
    {
        if (lifecycleService == null)
        {
            Debug.LogError(nameof(RestaurantPlaceableCreationService) +
                           " necesita un " + nameof(RestaurantPlaceableLifecycleService) + ".", this);
        }

        if (transactionService == null)
        {
            Debug.LogError(nameof(RestaurantPlaceableCreationService) +
                           " necesita un " + nameof(RestaurantPlacementTransactionService) + ".", this);
        }

        if (historyService == null)
        {
            Debug.LogError(nameof(RestaurantPlaceableCreationService) +
                           " necesita un " + nameof(RestaurantPlacementHistoryService) + ".", this);
        }
    }

    private void LogOperation(string message)
    {
        if (logCreationOperations)
        {
            Debug.Log(message, this);
        }
    }

    private static RestaurantPlaceableCreationResult Failure(
        RestaurantPlaceableCreationFailureReason reason,
        RestaurantPlaceableObject placeable,
        string message,
        RestaurantPlacementTransactionFailureReason transactionFailure =
            RestaurantPlacementTransactionFailureReason.None)
    {
        return RestaurantPlaceableCreationResult.Failure(
            reason,
            placeable,
            default,
            transactionFailure,
            message);
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

public readonly struct RestaurantPlaceableCreationResult
{
    public bool Succeeded { get; }
    public RestaurantPlaceableCreationFailureReason FailureReason { get; }
    public RestaurantPlaceableObject Placeable { get; }
    public RestaurantPlacementValidationResult ValidationResult { get; }
    public RestaurantPlacementTransactionFailureReason TransactionFailureReason { get; }
    public string Message { get; }

    private RestaurantPlaceableCreationResult(
        bool succeeded,
        RestaurantPlaceableCreationFailureReason failureReason,
        RestaurantPlaceableObject placeable,
        RestaurantPlacementValidationResult validationResult,
        RestaurantPlacementTransactionFailureReason transactionFailureReason,
        string message)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
        Placeable = placeable;
        ValidationResult = validationResult;
        TransactionFailureReason = transactionFailureReason;
        Message = message ?? string.Empty;
    }

    public static RestaurantPlaceableCreationResult Success(
        RestaurantPlaceableObject placeable,
        RestaurantPlacementValidationResult validationResult,
        string message)
    {
        return new RestaurantPlaceableCreationResult(
            true,
            RestaurantPlaceableCreationFailureReason.None,
            placeable,
            validationResult,
            RestaurantPlacementTransactionFailureReason.None,
            message);
    }

    public static RestaurantPlaceableCreationResult Failure(
        RestaurantPlaceableCreationFailureReason failureReason,
        RestaurantPlaceableObject placeable,
        RestaurantPlacementValidationResult validationResult,
        RestaurantPlacementTransactionFailureReason transactionFailureReason,
        string message)
    {
        return new RestaurantPlaceableCreationResult(
            false,
            failureReason,
            placeable,
            validationResult,
            transactionFailureReason,
            message);
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
    CancellationFailed = 10,
    EconomyRejected = 11,
    EconomyCommitFailed = 12
}
