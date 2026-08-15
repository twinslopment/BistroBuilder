using System;
using UnityEngine;

/// <summary>
/// Coordina la retirada reversible de artículos colocables. 3F puede enlazar
/// una regla económica sin introducir dependencias de Finanzas en Edición.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Placeable Deletion Service")]
public sealed class RestaurantPlaceableDeletionService : MonoBehaviour
{
    [SerializeField]
    private RestaurantEditModeService editModeService;

    [SerializeField]
    private RestaurantPlacementTransactionService transactionService;

    [SerializeField]
    private RestaurantPlaceableLifecycleService lifecycleService;

    [SerializeField]
    private RestaurantPlacementHistoryService historyService;

    [SerializeField]
    private bool logDeletionOperations = true;

    private IRestaurantPlaceableEconomyGate economyGate;

    public event Action<RestaurantPlaceableObject> PlaceableDeleted;
    public event Action<RestaurantPlaceableObject, RestaurantPlaceableDeletionResult>
        PlaceableDeletionRejected;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
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
            error = "Ya existe una regla económica enlazada a la eliminación.";
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

    public bool TryDelete(
        RestaurantPlaceableObject placeable,
        out RestaurantPlaceableDeletionResult result)
    {
        if (!DependenciesAreAvailable())
        {
            return RejectAndReturn(
                placeable,
                RestaurantPlaceableDeletionFailureReason.SystemUnavailable,
                "El sistema de eliminación no está disponible.",
                out result);
        }

        if (!editModeService.IsEditModeActive)
        {
            return RejectAndReturn(
                placeable,
                RestaurantPlaceableDeletionFailureReason.EditModeInactive,
                "La eliminación solo está disponible en modo edición.",
                out result);
        }

        if (transactionService.HasActiveTransaction)
        {
            return RejectAndReturn(
                placeable,
                RestaurantPlaceableDeletionFailureReason.PlacementOperationActive,
                "Confirma o cancela la colocación actual antes de eliminar un artículo.",
                out result);
        }

        if (placeable == null)
        {
            return RejectAndReturn(
                null,
                RestaurantPlaceableDeletionFailureReason.TargetUnavailable,
                "El artículo seleccionado no está disponible.",
                out result);
        }

        if (!lifecycleService.IsRegistered(placeable))
        {
            return RejectAndReturn(
                placeable,
                RestaurantPlaceableDeletionFailureReason.TargetNotRegistered,
                "El artículo no está registrado como una instancia activa.",
                out result);
        }

        if (economyGate != null &&
            !economyGate.TryAuthorizeDeletion(placeable, out string authorizationError))
        {
            return RejectAndReturn(
                placeable,
                RestaurantPlaceableDeletionFailureReason.EconomyRejected,
                authorizationError,
                out result);
        }

        if (!lifecycleService.TryDeactivateInstance(
                placeable,
                out RestaurantPlacementStateSnapshot deletedState,
                out RestaurantPlaceableLifecycleResult lifecycleResult))
        {
            return RejectAndReturn(
                placeable,
                RestaurantPlaceableDeletionFailureReason.LifecycleRejected,
                lifecycleResult.Message,
                out result);
        }

        string economyError = string.Empty;
        bool economyCommitted = economyGate == null ||
            economyGate.TryCommitDeletion(placeable, out economyError);
        if (!economyCommitted)
        {
            lifecycleService.TryActivateInstance(placeable, deletedState, out _);
            return RejectAndReturn(
                placeable,
                RestaurantPlaceableDeletionFailureReason.EconomyCommitFailed,
                economyError,
                out result);
        }

        var command = new RestaurantDeletePlaceableHistoryCommand(
            lifecycleService,
            placeable,
            deletedState);

        if (!historyService.TryRecordExecutedCommand(command))
        {
            string rollbackError = string.Empty;
            bool economyRolledBack = economyGate == null ||
                economyGate.TryRollbackDeletion(placeable, out rollbackError);
            bool restored = lifecycleService.TryActivateInstance(
                placeable,
                deletedState,
                out RestaurantPlaceableLifecycleResult restoreResult);

            string message = restored
                ? "El historial rechazó la eliminación y el artículo fue restaurado."
                : "El historial rechazó la eliminación y no se pudo restaurar el artículo. " +
                  restoreResult.Message;

            if (!economyRolledBack)
            {
                message += " Reversión financiera fallida: " + rollbackError;
                Debug.LogError(message, this);
            }

            return RejectAndReturn(
                placeable,
                RestaurantPlaceableDeletionFailureReason.HistoryRejected,
                message,
                out result);
        }

        result = RestaurantPlaceableDeletionResult.Success(
            placeable,
            "Artículo retirado y conservado para deshacer.");
        PlaceableDeleted?.Invoke(placeable);

        if (logDeletionOperations)
        {
            Debug.Log(
                "Retirado artículo " + placeable.DisplayName +
                " [" + placeable.InstanceId + "].",
                this);
        }

        return true;
    }

    private bool RejectAndReturn(
        RestaurantPlaceableObject placeable,
        RestaurantPlaceableDeletionFailureReason reason,
        string message,
        out RestaurantPlaceableDeletionResult result)
    {
        result = RestaurantPlaceableDeletionResult.Failure(
            reason,
            placeable,
            message);
        Reject(placeable, result);
        return false;
    }

    private void Reject(
        RestaurantPlaceableObject placeable,
        RestaurantPlaceableDeletionResult result)
    {
        PlaceableDeletionRejected?.Invoke(placeable, result);
        if (logDeletionOperations)
        {
            Debug.LogWarning(result.Message, this);
        }
    }

    private bool DependenciesAreAvailable()
    {
        return editModeService != null &&
               transactionService != null &&
               lifecycleService != null &&
               historyService != null;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (editModeService == null)
        {
            TryGetComponent(out editModeService);
        }

        if (transactionService == null)
        {
            TryGetComponent(out transactionService);
        }

        if (lifecycleService == null)
        {
            TryGetComponent(out lifecycleService);
        }

        if (historyService == null)
        {
            TryGetComponent(out historyService);
        }
    }

    private void ValidateDependencies()
    {
        if (editModeService == null)
        {
            Debug.LogError(nameof(RestaurantPlaceableDeletionService) +
                           " necesita un RestaurantEditModeService.", this);
        }

        if (transactionService == null)
        {
            Debug.LogError(nameof(RestaurantPlaceableDeletionService) +
                           " necesita un RestaurantPlacementTransactionService.", this);
        }

        if (lifecycleService == null)
        {
            Debug.LogError(nameof(RestaurantPlaceableDeletionService) +
                           " necesita un RestaurantPlaceableLifecycleService.", this);
        }

        if (historyService == null)
        {
            Debug.LogError(nameof(RestaurantPlaceableDeletionService) +
                           " necesita un RestaurantPlacementHistoryService.", this);
        }
    }
}

public readonly struct RestaurantPlaceableDeletionResult
{
    public bool Succeeded { get; }
    public RestaurantPlaceableDeletionFailureReason FailureReason { get; }
    public RestaurantPlaceableObject Placeable { get; }
    public string Message { get; }

    private RestaurantPlaceableDeletionResult(
        bool succeeded,
        RestaurantPlaceableDeletionFailureReason failureReason,
        RestaurantPlaceableObject placeable,
        string message)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
        Placeable = placeable;
        Message = string.IsNullOrWhiteSpace(message)
            ? string.Empty
            : message.Trim();
    }

    public static RestaurantPlaceableDeletionResult Success(
        RestaurantPlaceableObject placeable,
        string message)
    {
        return new RestaurantPlaceableDeletionResult(
            true,
            RestaurantPlaceableDeletionFailureReason.None,
            placeable,
            message);
    }

    public static RestaurantPlaceableDeletionResult Failure(
        RestaurantPlaceableDeletionFailureReason failureReason,
        RestaurantPlaceableObject placeable,
        string message)
    {
        return new RestaurantPlaceableDeletionResult(
            false,
            failureReason,
            placeable,
            message);
    }
}

public enum RestaurantPlaceableDeletionFailureReason
{
    None = 0,
    SystemUnavailable = 1,
    EditModeInactive = 2,
    PlacementOperationActive = 3,
    TargetUnavailable = 4,
    TargetNotRegistered = 5,
    LifecycleRejected = 6,
    HistoryRejected = 7,
    EconomyRejected = 8,
    EconomyCommitFailed = 9
}
