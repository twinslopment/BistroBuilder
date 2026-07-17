using System;
using UnityEngine;

/// <summary>
/// Coordina la retirada reversible de cualquier artículo colocable.
///
/// La operación es genérica y no conoce mesas, lámparas, sillas,
/// plantas ni equipamiento concreto. Se apoya en el ciclo de vida y
/// registra un comando Delete en el historial.
///
/// Una eliminación confirmada desactiva y desregistra la instancia,
/// pero no la destruye mientras pueda recuperarse con Ctrl+Z.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placeable Deletion Service"
)]
public sealed class RestaurantPlaceableDeletionService :
    MonoBehaviour
{
    [Header("Dependencias")]

    [SerializeField]
    private RestaurantEditModeService editModeService;

    [SerializeField]
    private RestaurantPlacementTransactionService
        transactionService;

    [SerializeField]
    private RestaurantPlaceableLifecycleService
        lifecycleService;

    [SerializeField]
    private RestaurantPlacementHistoryService
        historyService;

    [Header("Depuración")]

    [SerializeField]
    private bool logDeletionOperations = true;

    public event Action<RestaurantPlaceableObject>
        PlaceableDeleted;

    public event Action<
        RestaurantPlaceableObject,
        RestaurantPlaceableDeletionResult
    > PlaceableDeletionRejected;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
        ValidateDependencies();
    }

    /// <summary>
    /// Retira una instancia activa y registra la operación en el
    /// historial. Si el historial rechaza el comando, restaura
    /// inmediatamente la instancia para mantener atomicidad.
    /// </summary>
    public bool TryDelete(
        RestaurantPlaceableObject placeable,
        out RestaurantPlaceableDeletionResult result
    )
    {
        if (!DependenciesAreAvailable())
        {
            result =
                RestaurantPlaceableDeletionResult.Failure(
                    RestaurantPlaceableDeletionFailureReason
                        .SystemUnavailable,
                    placeable,
                    "El sistema de eliminación no está disponible."
                );

            Reject(placeable, result);

            return false;
        }

        if (!editModeService.IsEditModeActive)
        {
            result =
                RestaurantPlaceableDeletionResult.Failure(
                    RestaurantPlaceableDeletionFailureReason
                        .EditModeInactive,
                    placeable,
                    "La eliminación solo está disponible en modo edición."
                );

            Reject(placeable, result);

            return false;
        }

        if (transactionService.HasActiveTransaction)
        {
            result =
                RestaurantPlaceableDeletionResult.Failure(
                    RestaurantPlaceableDeletionFailureReason
                        .PlacementOperationActive,
                    placeable,
                    "Confirma o cancela la colocación actual antes " +
                    "de eliminar un artículo."
                );

            Reject(placeable, result);

            return false;
        }

        if (placeable == null)
        {
            result =
                RestaurantPlaceableDeletionResult.Failure(
                    RestaurantPlaceableDeletionFailureReason
                        .TargetUnavailable,
                    null,
                    "El artículo seleccionado no está disponible."
                );

            Reject(null, result);

            return false;
        }

        if (!lifecycleService.IsRegistered(placeable))
        {
            result =
                RestaurantPlaceableDeletionResult.Failure(
                    RestaurantPlaceableDeletionFailureReason
                        .TargetNotRegistered,
                    placeable,
                    "El artículo no está registrado como una " +
                    "instancia activa."
                );

            Reject(placeable, result);

            return false;
        }

        bool deactivated =
            lifecycleService.TryDeactivateInstance(
                placeable,
                out RestaurantPlacementStateSnapshot deletedState,
                out RestaurantPlaceableLifecycleResult
                    lifecycleResult
            );

        if (!deactivated)
        {
            result =
                RestaurantPlaceableDeletionResult.Failure(
                    RestaurantPlaceableDeletionFailureReason
                        .LifecycleRejected,
                    placeable,
                    lifecycleResult.Message
                );

            Reject(placeable, result);

            return false;
        }

        RestaurantDeletePlaceableHistoryCommand command =
            new RestaurantDeletePlaceableHistoryCommand(
                lifecycleService,
                placeable,
                deletedState
            );

        if (!historyService.TryRecordExecutedCommand(command))
        {
            bool restored =
                lifecycleService.TryActivateInstance(
                    placeable,
                    deletedState,
                    out RestaurantPlaceableLifecycleResult
                        restoreResult
                );

            string message =
                restored
                    ? "El historial rechazó la eliminación y el " +
                      "artículo fue restaurado."
                    : "El historial rechazó la eliminación y tampoco " +
                      "se pudo restaurar el artículo. " +
                      restoreResult.Message;

            result =
                RestaurantPlaceableDeletionResult.Failure(
                    RestaurantPlaceableDeletionFailureReason
                        .HistoryRejected,
                    placeable,
                    message
                );

            Reject(placeable, result);

            return false;
        }

        result =
            RestaurantPlaceableDeletionResult.Success(
                placeable,
                "Artículo eliminado y conservado para deshacer."
            );

        PlaceableDeleted?.Invoke(
            placeable
        );

        if (logDeletionOperations)
        {
            Debug.Log(
                "Eliminado artículo " +
                placeable.DisplayName +
                " [" +
                placeable.InstanceId +
                "].",
                this
            );
        }

        return true;
    }

    private void Reject(
        RestaurantPlaceableObject placeable,
        RestaurantPlaceableDeletionResult result
    )
    {
        PlaceableDeletionRejected?.Invoke(
            placeable,
            result
        );

        if (logDeletionOperations)
        {
            Debug.LogWarning(
                result.Message,
                this
            );
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
            TryGetComponent(
                out editModeService
            );
        }

        if (transactionService == null)
        {
            TryGetComponent(
                out transactionService
            );
        }

        if (lifecycleService == null)
        {
            TryGetComponent(
                out lifecycleService
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
        if (editModeService == null)
        {
            Debug.LogError(
                "RestaurantPlaceableDeletionService necesita un " +
                "RestaurantEditModeService.",
                this
            );
        }

        if (transactionService == null)
        {
            Debug.LogError(
                "RestaurantPlaceableDeletionService necesita un " +
                "RestaurantPlacementTransactionService.",
                this
            );
        }

        if (lifecycleService == null)
        {
            Debug.LogError(
                "RestaurantPlaceableDeletionService necesita un " +
                "RestaurantPlaceableLifecycleService.",
                this
            );
        }

        if (historyService == null)
        {
            Debug.LogError(
                "RestaurantPlaceableDeletionService necesita un " +
                "RestaurantPlacementHistoryService.",
                this
            );
        }
    }
}

/// <summary>
/// Resultado estructurado de una petición de eliminación.
/// </summary>
public readonly struct RestaurantPlaceableDeletionResult
{
    public bool Succeeded
    {
        get;
    }

    public RestaurantPlaceableDeletionFailureReason FailureReason
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

    private RestaurantPlaceableDeletionResult(
        bool succeeded,
        RestaurantPlaceableDeletionFailureReason failureReason,
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
            string.IsNullOrWhiteSpace(message)
                ? string.Empty
                : message.Trim();
    }

    public static RestaurantPlaceableDeletionResult Success(
        RestaurantPlaceableObject placeable,
        string message
    )
    {
        return new RestaurantPlaceableDeletionResult(
            true,
            RestaurantPlaceableDeletionFailureReason.None,
            placeable,
            message
        );
    }

    public static RestaurantPlaceableDeletionResult Failure(
        RestaurantPlaceableDeletionFailureReason failureReason,
        RestaurantPlaceableObject placeable,
        string message
    )
    {
        return new RestaurantPlaceableDeletionResult(
            false,
            failureReason,
            placeable,
            message
        );
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
    HistoryRejected = 7
}
