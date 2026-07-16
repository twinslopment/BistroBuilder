using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Historial central de operaciones confirmadas del modo edición.
///
/// Conserva el nombre del componente existente para mantener todas las
/// referencias serializadas de la escena, pero almacena comandos
/// genéricos.
///
/// Cuando un comando se elimina definitivamente del historial se le
/// solicita liberar sus recursos. Esto evita conservar GameObjects
/// inactivos después de perder la posibilidad de rehacerlos.
///
/// No utiliza Update.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placement History Service"
)]
public sealed class RestaurantPlacementHistoryService :
    MonoBehaviour
{
    [Header("Dependencias")]

    [SerializeField]
    private RestaurantPlacementTransactionService
        transactionService;

    [SerializeField]
    private RestaurantPlacementValidationService
        validationService;

    [Header("Historial")]

    [SerializeField]
    [Min(1)]
    private int maximumHistoryEntries = 50;

    [SerializeField]
    private bool validateDestinationBeforeApplying = true;

    [Header("Depuración")]

    [SerializeField]
    private bool logHistoryOperations = true;

    private readonly List<IRestaurantEditHistoryCommand>
        undoStack =
            new List<IRestaurantEditHistoryCommand>(50);

    private readonly List<IRestaurantEditHistoryCommand>
        redoStack =
            new List<IRestaurantEditHistoryCommand>(50);

    public event Action HistoryChanged;

    public event Action<RestaurantAreaMember>
        UndoPerformed;

    public event Action<RestaurantAreaMember>
        RedoPerformed;

    public event Action<
        RestaurantPlacementHistoryFailureReason,
        RestaurantPlacementValidationResult
    > HistoryOperationRejected;

    public event Action<IRestaurantEditHistoryCommand>
        CommandRecorded;

    public event Action<
        IRestaurantEditHistoryCommand,
        RestaurantEditHistoryCommandResult
    > CommandUndone;

    public event Action<
        IRestaurantEditHistoryCommand,
        RestaurantEditHistoryCommandResult
    > CommandRedone;

    public event Action<
        IRestaurantEditHistoryCommand,
        RestaurantEditHistoryCommandResult
    > CommandRejected;

    public bool CanUndo
    {
        get
        {
            return undoStack.Count > 0;
        }
    }

    public bool CanRedo
    {
        get
        {
            return redoStack.Count > 0;
        }
    }

    public int UndoCount
    {
        get
        {
            return undoStack.Count;
        }
    }

    public int RedoCount
    {
        get
        {
            return redoStack.Count;
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
        UnsubscribeFromTransactionService();
    }

    private void OnDestroy()
    {
        UnsubscribeFromTransactionService();
        ClearHistory();
    }

    /// <summary>
    /// Registra una operación que ya se ha ejecutado y confirmado.
    /// </summary>
    public bool TryRecordExecutedCommand(
        IRestaurantEditHistoryCommand command
    )
    {
        if (command == null ||
            !command.IsValid)
        {
            RestaurantEditHistoryCommandResult rejection =
                RestaurantEditHistoryCommandResult.Failure(
                    RestaurantEditHistoryCommandFailureReason
                        .CommandInvalid,
                    command != null
                        ? command.PrimaryTarget
                        : null,
                    ResolveAreaMember(command),
                    default,
                    "El comando no contiene un cambio válido."
                );

            CommandRejected?.Invoke(
                command,
                rejection
            );

            return false;
        }

        undoStack.Add(
            command
        );

        TrimStackIfNeeded(
            undoStack
        );

        /*
         * Una operación nueva descarta toda la rama de rehacer.
         * Antes de vaciarla se liberan las instancias inactivas que
         * solo continuaban existiendo por esos comandos.
         */
        ReleaseStackResources(
            redoStack
        );

        redoStack.Clear();

        HistoryChanged?.Invoke();

        CommandRecorded?.Invoke(
            command
        );

        if (logHistoryOperations)
        {
            Debug.Log(
                "Registrado comando '" +
                command.Description +
                "'. Tipo: " +
                command.CommandType +
                ". Deshacer: " +
                undoStack.Count +
                ", rehacer: " +
                redoStack.Count +
                ".",
                this
            );
        }

        return true;
    }

    public bool TryUndo(
        out RestaurantAreaMember affectedMember,
        out RestaurantPlacementHistoryFailureReason
            failureReason,
        out RestaurantPlacementValidationResult
            validationResult
    )
    {
        affectedMember = null;
        validationResult = default;

        failureReason =
            RestaurantPlacementHistoryFailureReason.None;

        if (!CanOperate(
                out failureReason
            ))
        {
            RejectLegacy(
                failureReason,
                validationResult
            );

            return false;
        }

        if (undoStack.Count == 0)
        {
            failureReason =
                RestaurantPlacementHistoryFailureReason
                    .NothingToUndo;

            RejectLegacy(
                failureReason,
                validationResult
            );

            return false;
        }

        int lastIndex =
            undoStack.Count - 1;

        IRestaurantEditHistoryCommand command =
            undoStack[lastIndex];

        if (command == null)
        {
            failureReason =
                RestaurantPlacementHistoryFailureReason
                    .CommandInvalid;

            undoStack.RemoveAt(
                lastIndex
            );

            HistoryChanged?.Invoke();

            RejectLegacy(
                failureReason,
                validationResult
            );

            return false;
        }

        bool undone =
            command.TryUndo(
                out RestaurantEditHistoryCommandResult result
            );

        affectedMember =
            result.AffectedMember ??
            ResolveAreaMember(command);

        validationResult =
            result.ValidationResult;

        if (!undone)
        {
            failureReason =
                MapFailureReason(
                    result.FailureReason
                );

            CommandRejected?.Invoke(
                command,
                result
            );

            RejectLegacy(
                failureReason,
                validationResult
            );

            return false;
        }

        undoStack.RemoveAt(
            lastIndex
        );

        redoStack.Add(
            command
        );

        TrimStackIfNeeded(
            redoStack
        );

        HistoryChanged?.Invoke();

        UndoPerformed?.Invoke(
            affectedMember
        );

        CommandUndone?.Invoke(
            command,
            result
        );

        if (logHistoryOperations)
        {
            Debug.Log(
                "Deshecho comando '" +
                command.Description +
                "'.",
                this
            );
        }

        return true;
    }

    public bool TryRedo(
        out RestaurantAreaMember affectedMember,
        out RestaurantPlacementHistoryFailureReason
            failureReason,
        out RestaurantPlacementValidationResult
            validationResult
    )
    {
        affectedMember = null;
        validationResult = default;

        failureReason =
            RestaurantPlacementHistoryFailureReason.None;

        if (!CanOperate(
                out failureReason
            ))
        {
            RejectLegacy(
                failureReason,
                validationResult
            );

            return false;
        }

        if (redoStack.Count == 0)
        {
            failureReason =
                RestaurantPlacementHistoryFailureReason
                    .NothingToRedo;

            RejectLegacy(
                failureReason,
                validationResult
            );

            return false;
        }

        int lastIndex =
            redoStack.Count - 1;

        IRestaurantEditHistoryCommand command =
            redoStack[lastIndex];

        if (command == null)
        {
            failureReason =
                RestaurantPlacementHistoryFailureReason
                    .CommandInvalid;

            redoStack.RemoveAt(
                lastIndex
            );

            HistoryChanged?.Invoke();

            RejectLegacy(
                failureReason,
                validationResult
            );

            return false;
        }

        bool redone =
            command.TryRedo(
                out RestaurantEditHistoryCommandResult result
            );

        affectedMember =
            result.AffectedMember ??
            ResolveAreaMember(command);

        validationResult =
            result.ValidationResult;

        if (!redone)
        {
            failureReason =
                MapFailureReason(
                    result.FailureReason
                );

            CommandRejected?.Invoke(
                command,
                result
            );

            RejectLegacy(
                failureReason,
                validationResult
            );

            return false;
        }

        redoStack.RemoveAt(
            lastIndex
        );

        undoStack.Add(
            command
        );

        TrimStackIfNeeded(
            undoStack
        );

        HistoryChanged?.Invoke();

        RedoPerformed?.Invoke(
            affectedMember
        );

        CommandRedone?.Invoke(
            command,
            result
        );

        if (logHistoryOperations)
        {
            Debug.Log(
                "Rehecho comando '" +
                command.Description +
                "'.",
                this
            );
        }

        return true;
    }

    /// <summary>
    /// Vacía ambas ramas y libera los recursos que solo pertenecían
    /// al historial.
    /// </summary>
    public void ClearHistory()
    {
        bool hadEntries =
            undoStack.Count > 0 ||
            redoStack.Count > 0;

        ReleaseStackResources(
            undoStack
        );

        ReleaseStackResources(
            redoStack
        );

        undoStack.Clear();
        redoStack.Clear();

        if (hadEntries)
        {
            HistoryChanged?.Invoke();
        }
    }

    private void HandlePlacementCommitted(
        RestaurantPlacementCommittedChange change
    )
    {
        if (change.Member == null ||
            !change.HasMeaningfulChange ||
            change.TransactionKind !=
                RestaurantPlacementTransactionKind.MoveExisting)
        {
            return;
        }

        RestaurantMovePlaceableHistoryCommand command =
            new RestaurantMovePlaceableHistoryCommand(
                change.Member,
                change.Before,
                change.After,
                validationService,
                validateDestinationBeforeApplying
            );

        TryRecordExecutedCommand(
            command
        );
    }

    private bool CanOperate(
        out RestaurantPlacementHistoryFailureReason
            failureReason
    )
    {
        failureReason =
            RestaurantPlacementHistoryFailureReason.None;

        if (transactionService == null)
        {
            failureReason =
                RestaurantPlacementHistoryFailureReason
                    .TransactionSystemUnavailable;

            return false;
        }

        if (transactionService.HasActiveTransaction)
        {
            failureReason =
                RestaurantPlacementHistoryFailureReason
                    .PlacementOperationActive;

            return false;
        }

        return true;
    }

    private void TrimStackIfNeeded(
        List<IRestaurantEditHistoryCommand> stack
    )
    {
        int safeMaximum =
            Mathf.Max(
                1,
                maximumHistoryEntries
            );

        int overflow =
            stack.Count -
            safeMaximum;

        if (overflow <= 0)
        {
            return;
        }

        for (int index = 0;
             index < overflow;
             index++)
        {
            IRestaurantEditHistoryCommand command =
                stack[index];

            command?.ReleaseResources();
        }

        stack.RemoveRange(
            0,
            overflow
        );
    }

    private static void ReleaseStackResources(
        List<IRestaurantEditHistoryCommand> stack
    )
    {
        if (stack == null)
        {
            return;
        }

        for (int index = 0;
             index < stack.Count;
             index++)
        {
            IRestaurantEditHistoryCommand command =
                stack[index];

            command?.ReleaseResources();
        }
    }

    private void RejectLegacy(
        RestaurantPlacementHistoryFailureReason failureReason,
        RestaurantPlacementValidationResult validationResult
    )
    {
        HistoryOperationRejected?.Invoke(
            failureReason,
            validationResult
        );
    }

    private static RestaurantAreaMember ResolveAreaMember(
        IRestaurantEditHistoryCommand command
    )
    {
        if (command == null ||
            command.PrimaryTarget == null)
        {
            return null;
        }

        if (command.PrimaryTarget is RestaurantAreaMember member)
        {
            return member;
        }

        if (command.PrimaryTarget is Component component)
        {
            component.TryGetComponent(
                out RestaurantAreaMember componentMember
            );

            return componentMember;
        }

        if (command.PrimaryTarget is GameObject gameObject)
        {
            gameObject.TryGetComponent(
                out RestaurantAreaMember gameObjectMember
            );

            return gameObjectMember;
        }

        return null;
    }

    private static RestaurantPlacementHistoryFailureReason
        MapFailureReason(
            RestaurantEditHistoryCommandFailureReason reason
        )
    {
        switch (reason)
        {
            case RestaurantEditHistoryCommandFailureReason.None:

                return
                    RestaurantPlacementHistoryFailureReason.None;

            case RestaurantEditHistoryCommandFailureReason
                .ValidationSystemUnavailable:

                return
                    RestaurantPlacementHistoryFailureReason
                        .ValidationSystemUnavailable;

            case RestaurantEditHistoryCommandFailureReason
                .DestinationInvalid:

                return
                    RestaurantPlacementHistoryFailureReason
                        .DestinationInvalid;

            case RestaurantEditHistoryCommandFailureReason
                .TargetUnavailable:

                return
                    RestaurantPlacementHistoryFailureReason
                        .MemberUnavailable;

            case RestaurantEditHistoryCommandFailureReason
                .StateInvalid:

                return
                    RestaurantPlacementHistoryFailureReason
                        .SnapshotInvalid;

            case RestaurantEditHistoryCommandFailureReason
                .CommandInvalid:

            case RestaurantEditHistoryCommandFailureReason
                .CommandUnavailable:

                return
                    RestaurantPlacementHistoryFailureReason
                        .CommandInvalid;

            case RestaurantEditHistoryCommandFailureReason
                .LifecycleSystemUnavailable:

                return
                    RestaurantPlacementHistoryFailureReason
                        .LifecycleSystemUnavailable;

            case RestaurantEditHistoryCommandFailureReason
                .IdentityConflict:

                return
                    RestaurantPlacementHistoryFailureReason
                        .IdentityConflict;

            case RestaurantEditHistoryCommandFailureReason
                .RegistrationFailed:

                return
                    RestaurantPlacementHistoryFailureReason
                        .RegistrationFailed;

            default:

                return
                    RestaurantPlacementHistoryFailureReason
                        .RestoreFailed;
        }
    }

    private void SubscribeToTransactionService()
    {
        if (transactionService == null)
        {
            return;
        }

        transactionService.PlacementCommittedWithHistory -=
            HandlePlacementCommitted;

        transactionService.PlacementCommittedWithHistory +=
            HandlePlacementCommitted;
    }

    private void UnsubscribeFromTransactionService()
    {
        if (transactionService == null)
        {
            return;
        }

        transactionService.PlacementCommittedWithHistory -=
            HandlePlacementCommitted;
    }

    private void CacheDependenciesIfNeeded()
    {
        if (transactionService == null)
        {
            TryGetComponent(
                out transactionService
            );
        }

        if (validationService == null)
        {
            TryGetComponent(
                out validationService
            );
        }
    }

    private void ValidateDependencies()
    {
        if (transactionService == null)
        {
            Debug.LogError(
                nameof(RestaurantPlacementHistoryService) +
                " necesita un " +
                nameof(
                    RestaurantPlacementTransactionService
                ) +
                ".",
                this
            );
        }

        if (validateDestinationBeforeApplying &&
            validationService == null)
        {
            Debug.LogError(
                nameof(RestaurantPlacementHistoryService) +
                " necesita un " +
                nameof(
                    RestaurantPlacementValidationService
                ) +
                " para revalidar movimientos históricos.",
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

        maximumHistoryEntries =
            Mathf.Max(
                1,
                maximumHistoryEntries
            );
    }
#endif
}

public enum RestaurantPlacementHistoryFailureReason
{
    None = 0,
    NothingToUndo = 1,
    NothingToRedo = 2,
    PlacementOperationActive = 3,
    TransactionSystemUnavailable = 4,
    ValidationSystemUnavailable = 5,
    MemberUnavailable = 6,
    SnapshotInvalid = 7,
    DestinationInvalid = 8,
    RestoreFailed = 9,
    CommandInvalid = 10,
    LifecycleSystemUnavailable = 11,
    IdentityConflict = 12,
    RegistrationFailed = 13
}
