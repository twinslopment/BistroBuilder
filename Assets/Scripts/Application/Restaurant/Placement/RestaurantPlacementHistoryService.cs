using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mantiene el historial de movimientos confirmados del modo edición.
///
/// Características:
/// - Registra únicamente confirmaciones reales.
/// - Deshace desde el estado final al estado anterior.
/// - Rehace desde el estado anterior al estado final.
/// - Limpia la rama de rehacer al confirmar un cambio nuevo.
/// - Revalida el destino antes de restaurarlo.
/// - No utiliza Update.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Placement History Service"
)]
public sealed class RestaurantPlacementHistoryService :
    MonoBehaviour
{
    [Header("Dependencias")]

    [Tooltip(
        "Servicio que publica las colocaciones confirmadas."
    )]
    [SerializeField]
    private RestaurantPlacementTransactionService
        transactionService;

    [Tooltip(
        "Servicio utilizado para validar el destino de deshacer " +
        "y rehacer antes de aplicarlo."
    )]
    [SerializeField]
    private RestaurantPlacementValidationService
        validationService;

    [Header("Historial")]

    [Tooltip(
        "Número máximo de operaciones conservadas."
    )]
    [SerializeField]
    [Min(1)]
    private int maximumHistoryEntries = 50;

    [Tooltip(
        "Valida la posición de destino antes de deshacer o rehacer."
    )]
    [SerializeField]
    private bool validateDestinationBeforeApplying = true;

    [Header("Depuración")]

    [Tooltip(
        "Escribe las operaciones de historial en la Console."
    )]
    [SerializeField]
    private bool logHistoryOperations = true;

    private readonly List<
        RestaurantPlacementCommittedChange
    > undoStack =
        new List<RestaurantPlacementCommittedChange>(50);

    private readonly List<
        RestaurantPlacementCommittedChange
    > redoStack =
        new List<RestaurantPlacementCommittedChange>(50);

    /// <summary>
    /// Se ejecuta cuando cambia la disponibilidad de deshacer o rehacer.
    /// </summary>
    public event Action HistoryChanged;

    /// <summary>
    /// Se ejecuta después de deshacer correctamente.
    /// </summary>
    public event Action<RestaurantAreaMember>
        UndoPerformed;

    /// <summary>
    /// Se ejecuta después de rehacer correctamente.
    /// </summary>
    public event Action<RestaurantAreaMember>
        RedoPerformed;

    /// <summary>
    /// Se ejecuta cuando una operación de historial es rechazada.
    /// </summary>
    public event Action<
        RestaurantPlacementHistoryFailureReason,
        RestaurantPlacementValidationResult
    > HistoryOperationRejected;

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
    /// Intenta deshacer la última colocación confirmada.
    /// </summary>
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
            Reject(
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

            Reject(
                failureReason,
                validationResult
            );

            return false;
        }

        int lastIndex =
            undoStack.Count - 1;

        RestaurantPlacementCommittedChange change =
            undoStack[lastIndex];

        affectedMember =
            change.Member;

        if (!TryApplySnapshot(
                affectedMember,
                change.Before,
                out failureReason,
                out validationResult
            ))
        {
            Reject(
                failureReason,
                validationResult
            );

            return false;
        }

        undoStack.RemoveAt(
            lastIndex
        );

        redoStack.Add(
            change
        );

        TrimStackIfNeeded(
            redoStack
        );

        HistoryChanged?.Invoke();

        UndoPerformed?.Invoke(
            affectedMember
        );

        if (logHistoryOperations)
        {
            Debug.Log(
                "Deshecha la última colocación de " +
                affectedMember.name +
                ".",
                this
            );
        }

        return true;
    }

    /// <summary>
    /// Intenta rehacer la última colocación deshecha.
    /// </summary>
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
            Reject(
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

            Reject(
                failureReason,
                validationResult
            );

            return false;
        }

        int lastIndex =
            redoStack.Count - 1;

        RestaurantPlacementCommittedChange change =
            redoStack[lastIndex];

        affectedMember =
            change.Member;

        if (!TryApplySnapshot(
                affectedMember,
                change.After,
                out failureReason,
                out validationResult
            ))
        {
            Reject(
                failureReason,
                validationResult
            );

            return false;
        }

        redoStack.RemoveAt(
            lastIndex
        );

        undoStack.Add(
            change
        );

        TrimStackIfNeeded(
            undoStack
        );

        HistoryChanged?.Invoke();

        RedoPerformed?.Invoke(
            affectedMember
        );

        if (logHistoryOperations)
        {
            Debug.Log(
                "Rehecha la última colocación de " +
                affectedMember.name +
                ".",
                this
            );
        }

        return true;
    }

    /// <summary>
    /// Vacía completamente ambas ramas del historial.
    /// </summary>
    public void ClearHistory()
    {
        bool hadEntries =
            undoStack.Count > 0 ||
            redoStack.Count > 0;

        undoStack.Clear();
        redoStack.Clear();

        if (hadEntries)
        {
            HistoryChanged?.Invoke();
        }
    }

    /// <summary>
    /// Registra una colocación confirmada.
    /// </summary>
    private void HandlePlacementCommitted(
        RestaurantPlacementCommittedChange change
    )
    {
        if (change.Member == null ||
            !change.HasMeaningfulChange)
        {
            return;
        }

        undoStack.Add(
            change
        );

        TrimStackIfNeeded(
            undoStack
        );

        /*
         * Un cambio nuevo crea una rama distinta. Las operaciones
         * deshechas anteriormente ya no pueden rehacerse.
         */
        redoStack.Clear();

        HistoryChanged?.Invoke();

        if (logHistoryOperations)
        {
            Debug.Log(
                "Registrada colocación de " +
                change.Member.name +
                " en el historial. Deshacer: " +
                undoStack.Count +
                ", rehacer: " +
                redoStack.Count +
                ".",
                this
            );
        }
    }

    /// <summary>
    /// Comprueba las condiciones globales para operar el historial.
    /// </summary>
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

        if (validateDestinationBeforeApplying &&
            validationService == null)
        {
            failureReason =
                RestaurantPlacementHistoryFailureReason
                    .ValidationSystemUnavailable;

            return false;
        }

        return true;
    }

    /// <summary>
    /// Valida y restaura una captura concreta.
    /// </summary>
    private bool TryApplySnapshot(
        RestaurantAreaMember member,
        RestaurantPlacementStateSnapshot snapshot,
        out RestaurantPlacementHistoryFailureReason
            failureReason,
        out RestaurantPlacementValidationResult
            validationResult
    )
    {
        failureReason =
            RestaurantPlacementHistoryFailureReason.None;

        validationResult = default;

        if (member == null)
        {
            failureReason =
                RestaurantPlacementHistoryFailureReason
                    .MemberUnavailable;

            return false;
        }

        if (!snapshot.IsValid)
        {
            failureReason =
                RestaurantPlacementHistoryFailureReason
                    .SnapshotInvalid;

            return false;
        }

        if (validateDestinationBeforeApplying)
        {
            Vector3 destinationPosition;
            Quaternion destinationRotation;

            snapshot.GetWorldPose(
                out destinationPosition,
                out destinationRotation
            );

            validationResult =
                validationService.ValidatePlacement(
                    member,
                    destinationPosition,
                    destinationRotation
                );

            if (!validationResult.IsValid)
            {
                failureReason =
                    RestaurantPlacementHistoryFailureReason
                        .DestinationInvalid;

                return false;
            }
        }

        bool restored =
            snapshot.Restore(
                member
            );

        if (!restored)
        {
            failureReason =
                RestaurantPlacementHistoryFailureReason
                    .RestoreFailed;

            return false;
        }

        return true;
    }

    /// <summary>
    /// Elimina las entradas más antiguas si se supera el límite.
    /// </summary>
    private void TrimStackIfNeeded(
        List<RestaurantPlacementCommittedChange> stack
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

        stack.RemoveRange(
            0,
            overflow
        );
    }

    private void Reject(
        RestaurantPlacementHistoryFailureReason
            failureReason,
        RestaurantPlacementValidationResult
            validationResult
    )
    {
        HistoryOperationRejected?.Invoke(
            failureReason,
            validationResult
        );
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

        if (validationService == null)
        {
            Debug.LogError(
                nameof(RestaurantPlacementHistoryService) +
                " necesita un " +
                nameof(
                    RestaurantPlacementValidationService
                ) +
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

        maximumHistoryEntries =
            Mathf.Max(
                1,
                maximumHistoryEntries
            );
    }
#endif
}

/// <summary>
/// Motivo por el que deshacer o rehacer no puede completarse.
/// </summary>
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
    RestoreFailed = 9
}
